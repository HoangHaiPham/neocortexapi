using Azure.Storage.Blobs;
using Cloud_Common;
using Invariant.Entities;
using InvariantLearning_Utilities;
using NeoCortexApi.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private string experimentTime = DateTime.UtcNow.ToLongDateString().Replace(", ", " ") + "_" + DateTime.UtcNow.ToLongTimeString().Replace(":", "-");
        private string MnistFolderFromBlobStorage = "MnistDataset";
        private string outputFolderBlobStorage = "Output";
        
        public string experimentFolder = null;

        public async Task Semantic_InvariantRepresentation(MyConfig config, ExerimentRequestMessage msg, BlobContainerClient blobStorageName)
        {
            IMAGE_WIDTH = msg.IMAGE_WIDTH;
            IMAGE_HEIGHT = msg.IMAGE_HEIGHT;
            FRAME_WIDTH = msg.FRAME_WIDTH;
            FRAME_HEIGHT = msg.FRAME_HEIGHT;
            PIXEL_SHIFTED = msg.PIXEL_SHIFTED;
            MAX_CYCLE = msg.MAX_CYCLE;
            NUM_IMAGES_PER_LABEL = msg.NUM_IMAGES_PER_LABEL;
            PER_TESTSET = msg.PER_TESTSET;
            experimentFolder = $"InvariantRepresentation_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL * 10}-{PER_TESTSET}%_Cycle{MAX_CYCLE}_{experimentTime}";

            await AzureStorageProvider.GetMnistDatasetFromBlobStorage(blobStorageName, MnistFolderFromBlobStorage);


            List<Sample> trainingSamples, testingSamples;
            DataSet sourceSetBigScale, testSetBigScale;
            trainingSamples = new List<Sample>();
            testingSamples = new List<Sample>();
            List<Sample> sourceSamples = new List<Sample>();

            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, MnistFolderFromBlobStorage);
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen(MnistFolderFromBlobStorage, sourceMNIST, NUM_IMAGES_PER_LABEL);

            // get images from MNIST set
            DataSet sourceSet = new DataSet(sourceMNIST);

            // scale the original datasource according IMAGE_WIDTH, IMAGE_HEIGHT
            DataSet sourceSet_scale = DataSet.ScaleSet(experimentFolder, IMAGE_WIDTH, IMAGE_HEIGHT, sourceSet, "sourceSet");
            // put source image into 100x100 image size
            sourceSetBigScale = DataSet.CreateTestSet(sourceSet_scale, 100, 100, Path.Combine(experimentFolder, "sourceSetBigScale"));

            // get % of sourceSet_scale to be testSet
            DataSet testSet_scale = sourceSet_scale.GetTestData(PER_TESTSET);

            // put test image into 100x100 image size
            testSetBigScale = DataSet.CreateTestSet(testSet_scale, 100, 100, Path.Combine(experimentFolder, "testSetBigScale"));
            Console.WriteLine("Generating dataset ... ");

            // Creating the testing images from big scale image.
            var trainingImageFolderName = "TraingImageFolder";
            var listOfTrainingImage = Frame.GetConvFramesbyPixel(100, 100, IMAGE_WIDTH, IMAGE_HEIGHT, PIXEL_SHIFTED);

            DataSet trainingImage = new DataSet(new List<Image>());

            foreach (var img in sourceSetBigScale.Images)
            {
                string trainingImageFolder = Path.Combine(experimentFolder, trainingImageFolderName, $"{img.Label}");

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

            // TODO upload trainingImageFolder to Blob Storage
            await AzureStorageProvider.UploadFolderToBlogStorage(blobStorageName, outputFolderBlobStorage, Path.Combine(experimentFolder, trainingImageFolderName));


            // write extracted/filtered frame from original dataset into frames for SP to learn all pattern (EX: 32x32 -> quadrants 16x16)
            var listOfFrame = Frame.GetConvFramesbyPixel(IMAGE_WIDTH, IMAGE_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT, PIXEL_SHIFTED);
            string trainingFolderName = "TrainingExtractedFrame";
            string testingFolderName = "TestingExtractedFrame";

            // Creating the training frames for each images and put them in folders.
            //foreach (var image in sourceSet_scale.Images)
            foreach (var image in trainingImage.Images)
            {
                string extractedFrameFolder = Path.Combine(experimentFolder, trainingFolderName, $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");

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

            // Create training samples from the extracted frames.
            foreach (var classFolder in Directory.GetDirectories(Path.Combine(experimentFolder, trainingFolderName)))
            {
                string label = Path.GetFileName(classFolder);
                foreach (var imageFolder in Directory.GetDirectories(classFolder))
                {
                    foreach (var imagePath in Directory.GetFiles(imageFolder))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(imagePath);
                        var coordinatesString = fileName.Split('_').ToList();
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

            // Creating the testing images from big scale image.
            var testSetBigScaleFolder = "testSetBigScale";
            var listOfFrameBigScale = Frame.GetConvFramesbyPixel(100, 100, IMAGE_WIDTH, IMAGE_HEIGHT, PIXEL_SHIFTED);

            DataSet testSetExtractedFromBigScale = new DataSet(new List<Image>());

            foreach (var testBigScaleImage in testSetBigScale.Images)
            {
                string testExtractedFrameBigScaleFolder = Path.Combine(experimentFolder, testSetBigScaleFolder, $"{testBigScaleImage.Label}", $"Label_{testBigScaleImage.Label}_{Path.GetFileNameWithoutExtension(testBigScaleImage.ImagePath)}");

                Utility.CreateFolderIfNotExist(testExtractedFrameBigScaleFolder);

                //testImage.SaveTo(Path.Combine(testExtractedFrameBigScaleFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));

                Dictionary<Frame, double> whitePixelDensity = new Dictionary<Frame, double>();
                foreach (var frameBigScale in listOfFrameBigScale)
                {
                    double whitePixelsCount = testBigScaleImage.HAI_FrameDensity(frameBigScale);
                    whitePixelDensity.Add(frameBigScale, whitePixelsCount);
                }

                // get max whitedensity
                var maxWhiteDensity_Value = whitePixelDensity.MaxBy(entry => entry.Value).Value;
                var maxWhiteDensity_Pic = whitePixelDensity.Where(entry => entry.Value == maxWhiteDensity_Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //foreach (var frameWithMaxWhiteDensity in maxWhiteDensity_Pic.Keys)
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

            // TODO upload testSetBigScaleFolder to Blob Storage
            await AzureStorageProvider.UploadFolderToBlogStorage(blobStorageName, outputFolderBlobStorage, Path.Combine(experimentFolder, testSetBigScaleFolder));



            // Creating the testing frames for each images and put them in folders.
            //string testFolder = Path.Combine(experimentFolder, testinggFolderName);
            //listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, pixelShifted);
            foreach (var testImage in testSetExtractedFromBigScale.Images)
            {
                string testExtractedFrameFolder = Path.Combine(experimentFolder, testingFolderName, $"{testImage.Label}", $"{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");

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


            // Create testing samples from the extracted frames.
            foreach (var testClassFolder in Directory.GetDirectories(Path.Combine(experimentFolder, testingFolderName)))
            {
                string label = Path.GetFileName(testClassFolder);
                foreach (var imageFolder in Directory.GetDirectories(testClassFolder))
                {
                    foreach (var imagePath in Directory.GetFiles(imageFolder))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(imagePath);

                        if (!fileName.Contains("_origin"))
                        {
                            var coordinatesString = fileName.Split('_').ToList();
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

            DataSet trainingSet = new DataSet(Path.Combine(experimentFolder, trainingFolderName), true);

            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(Semantic_InvariantRepresentation)}");

            HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();
            int numColumns = IMAGE_WIDTH * IMAGE_HEIGHT;
            LearningUnit learningUnit1 = new LearningUnit(FRAME_WIDTH, FRAME_HEIGHT, numColumns, "placeholder");
            learningUnit1.TrainingNewbornCycle(trainingSet, MAX_CYCLE);


            // Generate SDR for training samples.
            foreach (var trainingSample in trainingSamples)
            {
                var activeColumns = learningUnit1.Predict(trainingSample.FramePath);
                if (activeColumns != null && activeColumns.Length != 0)
                {
                    trainingSample.PixelIndicies = new int[activeColumns.Length];
                    trainingSample.PixelIndicies = activeColumns;
                }
            }

            // Semantic array for each ImageName in Training Set (combine all the SDR frames to be the unique SDR)
            // var trainImageName_SDRFrames = trainingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath).Split('\\').Last()).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());
            var trainImageName_SDRFrames = trainingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath)).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());

            List<Sample> trainLabel_SDRListIndexes = new List<Sample>();
            // Loop through each image
            foreach (var imageName in trainImageName_SDRFrames)
            {
                string label = imageName.Key.Split('\\').Last().Split('_')[1];

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



            // Generate SDR for testing samples.
            foreach (var testingSample in testingSamples)
            {
                var activeColumns = learningUnit1.Predict(testingSample.FramePath);
                if (activeColumns != null)
                {
                    testingSample.PixelIndicies = new int[activeColumns.Length];
                    testingSample.PixelIndicies = activeColumns;
                }
            }

            // Semantic array for each ImageName in Testing Set (combine all the SDR frames to be the unique SDR)
            //var testImageName_SDRFrames = testingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath).Split('\\').Last()).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());
            var testImageName_SDRFrames = testingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath)).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());

            List<Sample> testLabel_SDRListIndexes = new List<Sample>();
            // Loop through each image
            foreach (var imageName in testImageName_SDRFrames)
            {
                string label = imageName.Key.Split('\\').Last().Split('_')[1];
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

            double loose_match = 0;
            //Dictionary<string, List<string>> finalPredict = new Dictionary<string, List<string>>();
            Dictionary<string, Dictionary<string, double>> finalPredict = new Dictionary<string, Dictionary<string, double>>();

            Dictionary<string, double> percentageForEachDigit = new Dictionary<string, double>();
            string prev_image_name = "";

            foreach (var item in testLabel_SDRListIndexes)
            {
                string key_label = Path.GetFileNameWithoutExtension(item.FramePath).Substring(0, 9);
                //key_label = key_label.Remove(key_label.Length - 2);

                //string key_label = item.Object;

                //if (!finalPredict.ContainsKey(key_label))
                //{
                //    finalPredict.Add(key_label, new List<string>());
                //}

                if (prev_image_name != key_label)
                {
                    if (prev_image_name != "")
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
                TextWriterTraceListener myTextListener = new TextWriterTraceListener(logFileName);
                Trace.Listeners.Add(myTextListener);
                Trace.WriteLine($"Actual label: {item.Object}");
                Trace.WriteLine($"{(item.FramePath.Split('\\').Last())}");
                Trace.WriteLine("=======================================");
                string predictedObj = cls.HAI_PredictObj(item);
                Trace.Flush();
                Trace.Close();

                if (!percentageForEachDigit.ContainsKey(predictedObj))
                {
                    percentageForEachDigit.Add(predictedObj, 0);
                }
                percentageForEachDigit[predictedObj] += 1;




                if (predictedObj.Equals(item.Object))
                {
                    loose_match++;
                }

                //Debug.WriteLine($"Actual {item.Object} - Predicted {predictedObj}");
            }

            //TODO upload testingFolderName to Blob Storage
            await AzureStorageProvider.UploadFolderToBlogStorage(blobStorageName, outputFolderBlobStorage, Path.Combine(experimentFolder, testingFolderName));


            Debug.WriteLine("HIHIHIHI");

            // get percentage of each digit predicted for the last testing image when get outside of the loop
            finalPredict[prev_image_name] = percentageForEachDigit.GroupBy(x => x.Key).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value) / percentageForEachDigit.Sum(x => x.Value), 2));

            // Calculate Accuracy loose match
            double numOfItems = testLabel_SDRListIndexes.Count();
            var loose_accuracy = (loose_match / numOfItems) * 100;
            testingSamples.Clear();

            string logResult = Path.Combine(experimentFolder, $"Prediction_Result.log");
            TextWriterTraceListener resultFile = new TextWriterTraceListener(logResult);
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
                string predictedDigit = "";
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

                Trace.WriteLine($"Actual {p.Key}: Predicted {predictedDigit}");

                foreach (var w in p.Value)
                {
                    Trace.WriteLine($"----Actual {p.Key}: Predicted {w}");
                }

                Trace.WriteLine($"==========");
            }

            // Calculate Accuracy loose match
            double numOfSample = finalPredict.Count();
            var accuracy = (match / numOfSample) * 100;
            testingSamples.Clear();

            Trace.WriteLine($"loose_accuracy: {loose_match}/{numOfItems} = {loose_accuracy}%");
            Trace.WriteLine($"accuracy: {match}/{numOfSample} = {accuracy}%");
            Trace.Flush();
            Trace.Close();
        }
    }
}
