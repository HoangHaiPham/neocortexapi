using Cloud_Common;
using Invariant.Entities;
using InvariantLearning_Utilities;
using NeoCortexApi.Entities;
using System.Diagnostics;

namespace Cloud_Experiment
{
    public class RunInvariantRepresentation
    {
        private int IMAGE_WIDTH;
        private int IMAGE_HEIGHT;
        private int FRAME_WIDTH;
        private int FRAME_HEIGHT;
        private int PIXEL_SHIFTED;
        private int MAX_CYCLE;
        private int NUM_IMAGES_PER_LABEL;
        private int PER_TESTSET;
        private string experimentTime = DateTime.UtcNow.ToShortDateString().Replace(", ", " ").Replace("/", "-") + "_" + DateTime.UtcNow.ToShortTimeString().Replace(":", "-");
        private string sourceSet_FolderName = "SourceSet";
        private string sourceSetBigScale_FolderName = "SourceSetBigScale";
        private string trainingImage_FolderName = "Images_Training";
        private string testSetBigScale_FolderName = "Images_Testing";
        private string trainingExtractedFrame_FolderName = "ExtractedFrame_Training";
        private string testingExtractedFrame_FolderName = "ExtractedFrame_Testing";
        private string logResult_FileName = "Prediction_Result.log";
        private string finalResult;
        private List<Sample> trainingSamples = new List<Sample>();
        private List<Sample> testingSamples = new List<Sample>();
        private DataSet trainingImage = new DataSet(new List<Image>());
        private DataSet testSetExtractedFromBigScale = new DataSet(new List<Image>());

        public string experimentFolder;

        public Dictionary<string, string> Semantic_InvariantRepresentation(ExerimentRequestMessage msg, string MnistFolderFromBlobStorage, string outputFolderBlobStorage)
        {
            IMAGE_WIDTH = msg.IMAGE_WIDTH;
            IMAGE_HEIGHT = msg.IMAGE_HEIGHT;
            FRAME_WIDTH = msg.FRAME_WIDTH;
            FRAME_HEIGHT = msg.FRAME_HEIGHT;
            PIXEL_SHIFTED = msg.PIXEL_SHIFTED;
            MAX_CYCLE = msg.MAX_CYCLE;
            NUM_IMAGES_PER_LABEL = msg.NUM_IMAGES_PER_LABEL;
            PER_TESTSET = msg.PER_TESTSET;
            experimentFolder = $"{experimentTime}_InvariantRepresentation_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL * 10}-{PER_TESTSET}%_Cycle{MAX_CYCLE}";
            logResult_FileName = Path.Combine(experimentFolder, logResult_FileName);

            /// <summary>
            /// Generate dataset MNIST dataset
            /// </summary>
            Console.WriteLine("-------------- GENERATING DATASET ---------------");
            GenerateDataset(MnistFolderFromBlobStorage);

            /// <summary>
            /// Training process
            /// Construct Semantic array from SDR training samples and SDR testing samples
            /// </summary>
            Console.WriteLine($"-------------- TRAINING PROCESS ---------------");
            HtmClassifier<string, ComputeCycle> cls;
            List<Sample> testLabel_SDRListIndexes;
            TrainingProcess(out cls, out testLabel_SDRListIndexes);

            /// <summary>
            /// Predicting process
            /// Write result to log file
            /// </summary>
            Console.WriteLine($"-------------- PREDICTING PROCESS ---------------");
            PredictingProcess(cls, testLabel_SDRListIndexes);

            /// <summary>
            /// remove unnecessary folders
            /// </summary>
            //Directory.Delete(Path.Combine(experimentFolder, sourceSet_FolderName), true);
            //Directory.Delete(Path.Combine(experimentFolder, sourceSetBigScale_FolderName), true);
            //Directory.Delete(Path.Combine(experimentFolder, trainingExtractedFrame_FolderName), true);
            //Directory.Delete(Path.Combine(experimentFolder, testingExtractedFrame_FolderName), true);
            //Directory.Delete(Path.Combine(experimentFolder, MnistFolderFromBlobStorage), true);
            //foreach (var folder in Directory.GetDirectories(Path.Combine(experimentFolder, testSetBigScale_FolderName)))
            //{
            //    foreach (var sub_folder in Directory.GetDirectories(folder))
            //    {
            //        Directory.Delete(sub_folder, true);
            //    }
            //}

            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            keyValues.Add("outputFolderBlobStorage", $"{outputFolderBlobStorage}");
            keyValues.Add("trainingImage_FolderName", $"{Path.Combine(experimentFolder, trainingImage_FolderName)}");
            keyValues.Add("testSetBigScale_FolderName", $"{Path.Combine(experimentFolder, testSetBigScale_FolderName)}");
            keyValues.Add("logResult_FileName", $"{logResult_FileName}");
            keyValues.Add("accuracy", $"{finalResult}");

            return keyValues;
        }


        /// <summary>
        /// Prepare dataset from MNIST data
        /// </summary>
        private void GenerateDataset(string MnistFolderFromBlobStorage)
        {
            Utility.CreateFolderIfNotExist(experimentFolder);
            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, MnistFolderFromBlobStorage);
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen(MnistFolderFromBlobStorage, sourceMNIST, NUM_IMAGES_PER_LABEL);
            // get images from MNIST set
            DataSet sourceSet = new DataSet(sourceMNIST);
            // scale the original datasource according IMAGE_WIDTH, IMAGE_HEIGHT
            DataSet sourceSet_scale = DataSet.ScaleSet(experimentFolder, IMAGE_WIDTH, IMAGE_HEIGHT, sourceSet, sourceSet_FolderName);
            // put source image into 100x100 image size
            DataSet sourceSetBigScale = DataSet.CreateTestSet(sourceSet_scale, 100, 100, Path.Combine(experimentFolder, sourceSetBigScale_FolderName));
            // get % of sourceSet_scale to be testSet
            DataSet testSet_scale = sourceSet_scale.GetTestData(PER_TESTSET);
            // put test image into 100x100 image size
            DataSet testSetBigScale = DataSet.CreateTestSet(testSet_scale, 100, 100, Path.Combine(experimentFolder, this.testSetBigScale_FolderName));

            /// <summary>
            /// Create training images from sourceSetBigScale.
            /// </summary>
            var listOfTrainingImage = Frame.GetConvFramesbyPixel(100, 100, IMAGE_WIDTH, IMAGE_HEIGHT, PIXEL_SHIFTED);
            foreach (var img in sourceSetBigScale.Images)
            {
                string trainingImageFolder = Path.Combine(experimentFolder, trainingImage_FolderName, $"{img.Label}");
                Utility.CreateFolderIfNotExist(trainingImageFolder);
                Dictionary<Frame, double> whitePixelDensity = new Dictionary<Frame, double>();
                foreach (var trainImg in listOfTrainingImage)
                {
                    double whitePixelsCount = img.HAI_FrameDensity(trainImg);
                    whitePixelDensity.Add(trainImg, whitePixelsCount);
                }
                // get max whitedensity
                var maxWhiteDensity_Value = whitePixelDensity.MaxBy(entry => entry.Value).Value;
                var maxWhiteDensity_Pic = whitePixelDensity.Where(entry => entry.Value == maxWhiteDensity_Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var (frameWithMaxWhiteDensity, i) in maxWhiteDensity_Pic.Select((x, i) => (x, i)))
                {
                    {
                        if (!DataSet.ExistImageInDataSet(img, trainingImageFolder, frameWithMaxWhiteDensity.Key))
                        {
                            string savePath = Path.Combine(trainingImageFolder, $"Label_{img.Label}_{Path.GetFileNameWithoutExtension(img.ImagePath)}_{i}.png");
                            // binarize image with threshold 255/2
                            img.SaveTo(savePath, frameWithMaxWhiteDensity.Key, true);
                            trainingImage.Add(new Image(savePath, img.Label));
                        }
                    }
                }
            }


            /// <summary>
            /// Write extracted/filtered frame from original dataset into frames for SP to learn all pattern (EX: 32x32 -> quadrants 16x16)
            /// Create the training frames for each image and save them in folders.
            /// </summary>
            var listOfFrame = Frame.GetConvFramesbyPixel(IMAGE_WIDTH, IMAGE_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT, PIXEL_SHIFTED);
            foreach (var image in trainingImage.Images)
            {
                string extractedFrameFolder = Path.Combine(experimentFolder, trainingExtractedFrame_FolderName, $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");
                Utility.CreateFolderIfNotExist(extractedFrameFolder);
                foreach (var frame in listOfFrame)
                {
                    if (image.IsRegionInDensityRange(frame, 0, 100))
                    {
                        if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(extractedFrameFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
                            // binarize image with threshold 255/2
                            image.SaveTo(savePath, frame, true);
                        }
                    }
                }
            }

            /// <summary>
            /// Create training samples from the extracted frames.
            /// </summary>
            foreach (var classFolder in Directory.GetDirectories(Path.Combine(experimentFolder, trainingExtractedFrame_FolderName)))
            {
                string label = Path.GetFileName(classFolder);
                foreach (var imageFolder in Directory.GetDirectories(classFolder))
                {
                    foreach (var imagePath in Directory.GetFiles(imageFolder))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(imagePath);
                        var coordinatesString = fileName.Split("_").ToList();
                        List<int> coorOffsetList = new List<int>();
                        foreach (var coordinates in coordinatesString)
                        {
                            coorOffsetList.Add(int.Parse(coordinates));
                        }
                        // Calculate offset coordinates.
                        var tlX = coorOffsetList[0] = 0 - coorOffsetList[0];
                        var tlY = coorOffsetList[1] = 0 - coorOffsetList[1];
                        var brX = coorOffsetList[2] = IMAGE_WIDTH - coorOffsetList[2] - 1;
                        var brY = coorOffsetList[3] = IMAGE_HEIGHT - coorOffsetList[3] - 1;
                        Sample sample = new Sample();
                        sample.Object = label;
                        sample.FramePath = imagePath;
                        sample.Position = new Frame(tlX, tlY, brX, brY);
                        trainingSamples.Add(sample);
                    }
                }
            }

            /// <summary>
            /// Create testing images from testSetBigScale.
            /// </summary>
            var listOfFrameBigScale = Frame.GetConvFramesbyPixel(100, 100, IMAGE_WIDTH, IMAGE_HEIGHT, PIXEL_SHIFTED);
            foreach (var testBigScaleImage in testSetBigScale.Images)
            {
                string testExtractedFrameBigScaleFolder = Path.Combine(experimentFolder, testSetBigScale_FolderName, $"{testBigScaleImage.Label}", $"Label_{testBigScaleImage.Label}_{Path.GetFileNameWithoutExtension(testBigScaleImage.ImagePath)}");
                Utility.CreateFolderIfNotExist(testExtractedFrameBigScaleFolder);
                Dictionary<Frame, double> whitePixelDensity = new Dictionary<Frame, double>();
                foreach (var frameBigScale in listOfFrameBigScale)
                {
                    double whitePixelsCount = testBigScaleImage.HAI_FrameDensity(frameBigScale);
                    whitePixelDensity.Add(frameBigScale, whitePixelsCount);
                }
                // get max whitedensity
                var maxWhiteDensity_Value = whitePixelDensity.MaxBy(entry => entry.Value).Value;
                var maxWhiteDensity_Pic = whitePixelDensity.Where(entry => entry.Value == maxWhiteDensity_Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var (frameWithMaxWhiteDensity, i) in maxWhiteDensity_Pic.Select((x, i) => (x, i)))
                {
                    {
                        if (!DataSet.ExistImageInDataSet(testBigScaleImage, testExtractedFrameBigScaleFolder, frameWithMaxWhiteDensity.Key))
                        {
                            string savePath = Path.Combine(testExtractedFrameBigScaleFolder, $"{Path.GetFileNameWithoutExtension(testExtractedFrameBigScaleFolder)}_{i}.png");
                            // binarize image with threshold 255/2
                            testBigScaleImage.SaveTo(savePath, frameWithMaxWhiteDensity.Key, true);
                            testSetExtractedFromBigScale.Add(new Image(savePath, testBigScaleImage.Label));
                        }
                    }
                }
            }

            /// <summary>
            /// Create the testing frames for each image and put them in folders.
            /// </summary>
            foreach (var testImage in testSetExtractedFromBigScale.Images)
            {
                string testExtractedFrameFolder = Path.Combine(experimentFolder, testingExtractedFrame_FolderName, $"{testImage.Label}", $"{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");
                Utility.CreateFolderIfNotExist(testExtractedFrameFolder);
                testImage.SaveTo(Path.Combine(testExtractedFrameFolder, $"{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));
                foreach (var frame in listOfFrame)
                {
                    if (testImage.IsRegionInDensityRange(frame, 0, 100))
                    {
                        if (!DataSet.ExistImageInDataSet(testImage, testExtractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(testExtractedFrameFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
                            // binarize image with threshold 255/2
                            testImage.SaveTo(savePath, frame, true);
                        }
                    }
                }
            }

            /// <summary>
            /// Create testing samples from the testExtractedFrameFolder
            /// </summary>
            foreach (var testClassFolder in Directory.GetDirectories(Path.Combine(experimentFolder, testingExtractedFrame_FolderName)))
            {
                string label = Path.GetFileName(testClassFolder);
                foreach (var imageFolder in Directory.GetDirectories(testClassFolder))
                {
                    foreach (var imagePath in Directory.GetFiles(imageFolder))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(imagePath);
                        if (!fileName.Contains("_origin"))
                        {
                            var coordinatesString = fileName.Split("_").ToList();
                            List<int> coorOffsetList = new List<int>();
                            foreach (var coordinates in coordinatesString)
                            {
                                coorOffsetList.Add(int.Parse(coordinates));
                            }
                            // Calculate offset coordinates.
                            var tlX = coorOffsetList[0];
                            var tlY = coorOffsetList[1];
                            var brX = coorOffsetList[2];
                            var brY = coorOffsetList[3];
                            Sample sample = new Sample();
                            sample.Object = label;
                            sample.FramePath = imagePath;
                            sample.Position = new Frame(tlX, tlY, brX, brY);
                            testingSamples.Add(sample);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Training process
        /// Construct Semantic array from SDR training samples and SDR testing samples
        /// </summary>
        private void TrainingProcess(out HtmClassifier<string, ComputeCycle> cls, out List<Sample> testLabel_SDRListIndexes)
        {
            DataSet trainingSet = new DataSet(Path.Combine(experimentFolder, trainingExtractedFrame_FolderName), true);
            cls = new HtmClassifier<string, ComputeCycle>();
            int numColumns = IMAGE_WIDTH * IMAGE_HEIGHT;
            LearningUnit learning = new LearningUnit(FRAME_WIDTH, FRAME_HEIGHT, numColumns, "placeholder");
            learning.TrainingNewbornCycle(trainingSet, MAX_CYCLE);


            /// <summary>
            /// Generate SDR for training samples
            /// </summary>
            foreach (var trainingSample in trainingSamples)
            {
                var activeColumns = learning.Predict(trainingSample.FramePath);
                if (activeColumns != null && activeColumns.Length != 0)
                {
                    trainingSample.PixelIndicies = new int[activeColumns.Length];
                    trainingSample.PixelIndicies = activeColumns;
                }
            }

            /// <summary>
            /// Semantic array for each ImageName in Training Set (combine all the SDR frames to be the unique SDR)
            /// </summary>
            var trainImageName_SDRFrames = trainingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath)).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());

            List<Sample> trainLabel_SDRListIndexes = new List<Sample>();
            // Loop through each image
            foreach (var imageName in trainImageName_SDRFrames)
            {
                string label = imageName.Key.Split("\\").Last().Split("_")[1];
                Sample sample = new Sample();
                sample.Object = label;
                sample.FramePath = imageName.Key;

                // Combine all SDR frames to form an unique SDR
                List<int> combineSDRFrames = new List<int>();
                foreach (int[] i in imageName.Value)
                {
                    combineSDRFrames.AddRange(i);
                }

                // Compression SDR to store indexes of on bit
                int[] unique_sdr = combineSDRFrames.ToArray();
                List<int> SDRIndexes = new List<int>();
                for (int i = 0; i < unique_sdr.Length; i += 1)
                {
                    if (unique_sdr[i] > 0)
                    {
                        SDRIndexes.Add(i);
                    }
                }
                sample.PixelIndicies = SDRIndexes.ToArray();
                trainLabel_SDRListIndexes.Add(sample);
            }

            cls.LearnObj(trainLabel_SDRListIndexes);


            /// <summary>
            /// Generate SDR for testing samples.
            /// </summary>
            foreach (var testingSample in testingSamples)
            {
                var activeColumns = learning.Predict(testingSample.FramePath);
                if (activeColumns != null)
                {
                    testingSample.PixelIndicies = new int[activeColumns.Length];
                    testingSample.PixelIndicies = activeColumns;
                }
            }

            /// <summary>
            /// Semantic array for each ImageName in Testing Set (combine all the SDR frames to be the unique SDR)
            /// </summary>
            var testImageName_SDRFrames = testingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath)).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());

            testLabel_SDRListIndexes = new List<Sample>();
            // Loop through each image
            foreach (var imageName in testImageName_SDRFrames)
            {
                string label = imageName.Key.Split("\\").Last().Split("_")[1];
                Console.WriteLine(imageName.Key);

                Sample sample = new Sample();
                sample.Object = label;
                sample.FramePath = imageName.Key;

                // Combine all SDR frames to form an unique SDR
                List<int> combineSDRFrames = new List<int>();
                foreach (int[] i in imageName.Value)
                {
                    combineSDRFrames.AddRange(i);
                }

                // Compression SDR to store indexes of on bit
                int[] unique_sdr = combineSDRFrames.ToArray();
                List<int> SDRIndexes = new List<int>();
                for (int i = 0; i < unique_sdr.Length; i += 1)
                {
                    if (unique_sdr[i] > 0)
                    {
                        SDRIndexes.Add(i);
                    }
                }
                sample.PixelIndicies = SDRIndexes.ToArray();
                testLabel_SDRListIndexes.Add(sample);
            }
        }

        /// <summary>
        /// Predicting process
        /// Write result to log file
        /// </summary>
        private void PredictingProcess(HtmClassifier<string, ComputeCycle> cls, List<Sample> testLabel_SDRListIndexes)
        {
            //double loose_match = 0;
            Dictionary<string, Dictionary<string, double>> finalPredict = new Dictionary<string, Dictionary<string, double>>();
            Dictionary<string, double> percentageForEachDigit = new Dictionary<string, double>();
            string prev_image_name = null;
            foreach (var item in testLabel_SDRListIndexes)
            {
                string key_label = Path.GetFileNameWithoutExtension(item.FramePath).Substring(0, 9);
                if (prev_image_name != key_label)
                {
                    if (prev_image_name != null)
                    {
                        // get percentage of each digit predicted for each image
                        finalPredict[prev_image_name] = percentageForEachDigit.GroupBy(x => x.Key).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value) / percentageForEachDigit.Sum(x => x.Value), 2));
                    }
                    if (!finalPredict.ContainsKey(key_label))
                    {
                        percentageForEachDigit = new Dictionary<string, double>();
                        finalPredict.Add(key_label, percentageForEachDigit);
                    }
                }
                prev_image_name = key_label;

                string logFileName = Path.Combine(item.FramePath, @"..\", $"{item.FramePath.Split('\\').Last()}.log");
                Console.WriteLine(Path.Combine(item.FramePath, @"..\"));
                Console.WriteLine(logFileName);


                TextWriterTraceListener myTextListener = new TextWriterTraceListener(logFileName);
                Trace.Listeners.Add(myTextListener);
                Trace.WriteLine($"Actual label: {item.Object}");
                Trace.WriteLine($"{(item.FramePath.Split('\\').Last())}");
                Trace.WriteLine("=======================================");
                string predictedObj = cls.PredictObj(item);
                Trace.Flush();
                Trace.Close();

                //string predictedObj = cls.PredictObj(item);

                if (!percentageForEachDigit.ContainsKey(predictedObj))
                {
                    percentageForEachDigit.Add(predictedObj, 0);
                }
                percentageForEachDigit[predictedObj] += 1;

                //if (predictedObj.Equals(item.Object))
                //{
                //    loose_match++;
                //}
            }


            /// <summary>
            /// get percentage of each digit predicted for the last testing image
            /// </summary>
            finalPredict[prev_image_name] = percentageForEachDigit.GroupBy(x => x.Key).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value) / percentageForEachDigit.Sum(x => x.Value), 2));

            //// Calculate Accuracy loose match
            //double numOfItems = testLabel_SDRListIndexes.Count();
            //var loose_accuracy = (loose_match / numOfItems) * 100;
            //testingSamples.Clear();

            /// <summary>
            /// Write result to log file
            /// </summary>
            TextWriterTraceListener resultFile = new TextWriterTraceListener(logResult_FileName);
            Trace.Listeners.Add(resultFile);

            double match = 0;
            Dictionary<string, string> results = new Dictionary<string, string>();
            foreach (var p in finalPredict)
            {
                if (!results.ContainsKey(p.Key))
                {
                    results.Add(p.Key, string.Empty);
                }

                var perMaxDigitPredicted = p.Value.Where(x => x.Value == p.Value.MaxBy(x => x.Value).Value).ToList();

                var checkDigitIsMatched = perMaxDigitPredicted.Where(x => x.Key == p.Key.Split("_")[1]).ToList();
                string predictedDigit;
                if (checkDigitIsMatched.Count > 0)
                {
                    predictedDigit = checkDigitIsMatched.FirstOrDefault().Key.ToString();
                    match += 1;
                }
                else
                {
                    predictedDigit = perMaxDigitPredicted.FirstOrDefault().Key.ToString();
                }

                results[p.Key] = predictedDigit;

                Console.WriteLine($"Actual {p.Key}: Predicted {predictedDigit}");
                Trace.WriteLine($"Actual {p.Key}: Predicted {predictedDigit}");
                foreach (var w in p.Value)
                {
                    Trace.WriteLine($"-- Predicted {w.Key} with {(double)(w.Value) * 100}% similarity score");
                }
                Trace.WriteLine($"============================");
            }

            /// <summary>
            /// Calculate Accuracy
            /// </summary>
            double numOfSample = finalPredict.Count();
            var accuracy = (match / numOfSample) * 100;
            testingSamples.Clear();

            finalResult = $"{match}/{numOfSample} = {accuracy}%";

            //Trace.WriteLine($"loose_accuracy: {loose_match}/{numOfItems} = {loose_accuracy}%");
            Console.WriteLine($"Accuracy: {finalResult}");
            Trace.WriteLine($"Accuracy: {finalResult}");
            Trace.Flush();
            Trace.Close();
        }
    }
}
