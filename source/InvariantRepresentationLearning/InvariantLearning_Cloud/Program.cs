using NeoCortexApi;
using NeoCortexApi.Entities;
using System.Diagnostics;
using Invariant.Entities;
using NeoCortexApi.Encoders;
using NeoCortexApi.Classifiers;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading;
using Cloud_Common;
using Cloud_Experiment;


//using Experiment;

namespace InvariantLearning_FrameCheck
{
    public class InvariantLearning
    {

        // global variable
        // generate 32x32 source MNISTDataSet
        public static int IMAGE_WIDTH = 28;
        public static int IMAGE_HEIGHT = 28;
        public static int FRAME_WIDTH = 14;
        public static int FRAME_HEIGHT = 14;
        public static int PIXEL_SHIFTED = 14;
        public static int MAX_CYCLE = 5;
        public static int NUM_IMAGES_PER_LABEL = 10;
        public static int PER_TESTSET = 20;
        private static string projectName = "ML19/20-5.8 - HoangHaiPham - Validating Memorizing Capabilities of Spatial Pooler";
        private static string experimentTime = DateTime.UtcNow.ToLongDateString().Replace(", ", " ") + "_" + DateTime.UtcNow.ToLongTimeString().Replace(":", "-");

        //public static void Main()
        static void Main(string[] args)
        {
            ////*** CLOUD ***
            CancellationTokenSource tokeSrc = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tokeSrc.Cancel();
            };

            Console.WriteLine($"Cloud_HtmInvariantLearning_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL * 10}-{PER_TESTSET}%_Cycle{MAX_CYCLE}_{experimentTime}");

            Console.WriteLine($"Started experiment: {projectName}");

            //init configuration
            var cfgRoot = Cloud_Common.InitHelpers.InitConfiguration(args);

            var cfgSec = cfgRoot.GetSection("MyConfig");

            // InitLogging
            var logFactory = InitHelpers.InitLogging(cfgRoot);
            var logger = logFactory.CreateLogger("Train.Console");

            logger?.LogInformation($"{DateTime.Now} -  Started experiment: {projectName}");

            IStorageProvider storageProvider = new AzureStorageProvider(cfgSec);

            Experiment experiment = new Experiment(cfgSec, storageProvider, logger/* put some additional config here */);

            experiment.RunQueueListener(tokeSrc.Token).Wait();

            logger?.LogInformation($"{DateTime.Now} -  Experiment exit: {projectName}");

            //*** CLOUD ***


            //string experimentTime = DateTime.UtcNow.ToShortDateString().ToString().Replace('/', '-');
            //Console.WriteLine($"Cloud_HtmInvariantLearning_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL * 10}-{PER_TESTSET}%_Cycle{MAX_CYCLE}_{experimentTime}");
            //CloudSemantic100x100_InvariantRepresentation($"Cloud_HtmInvariantLearning_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL * 10}-{PER_TESTSET}%_Cycle {MAX_CYCLE}_{experimentTime}");
        
        }


        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void CloudSemantic100x100_InvariantRepresentation(string experimentFolder)
        {
            List<Sample> trainingSamples = new List<Sample>();
            //List<Sample> trainingBigSamples = new List<Sample>();
            List<Sample> testingSamples = new List<Sample>();
            List<Sample> sourceSamples = new List<Sample>();

            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, NUM_IMAGES_PER_LABEL);

            // get images from MNIST set
            DataSet sourceSet = new DataSet(sourceMNIST);

            // scale the original datasource according IMAGE_WIDTH, IMAGE_HEIGHT
            DataSet sourceSet_scale = DataSet.ScaleSet(experimentFolder, IMAGE_WIDTH, IMAGE_HEIGHT, sourceSet, "sourceSet");
            // put source image into 100x100 image size
            DataSet sourceSetBigScale = DataSet.CreateTestSet(sourceSet_scale, 100, 100, Path.Combine(experimentFolder, "sourceSetBigScale"));


            // get % of sourceSet_scale to be testSet
            DataSet testSet_scale = sourceSet_scale.GetTestData(PER_TESTSET);

            // put test image into 100x100 image size
            DataSet testSetBigScale = DataSet.CreateTestSet(testSet_scale, 100, 100, Path.Combine(experimentFolder, "testSetBigScale"));


            Debug.WriteLine("Generating dataset ... ");


            // Creating the testing images from big scale image.
            var trainingImageFolderName = "TraingImageFolder";
            var listOfTrainingImage = Frame.GetConvFramesbyPixel(100, 100, IMAGE_WIDTH, IMAGE_HEIGHT, PIXEL_SHIFTED);

            DataSet trainingImage = new DataSet(new List<Image>());

            foreach (var img in sourceSetBigScale.Images)
            {
                string trainingImageFolder = Path.Combine(experimentFolder, trainingImageFolderName, $"{img.Label}");

                Utility.CreateFolderIfNotExist(trainingImageFolder);

                //testImage.SaveTo(Path.Combine(testExtractedFrameBigScaleFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));

                Dictionary<Frame, double> whitePixelDensity = new Dictionary<Frame, double>();
                foreach (var trainImg in listOfTrainingImage)
                {
                    double whitePixelsCount = img.HAI_FrameDensity(trainImg);
                    whitePixelDensity.Add(trainImg, whitePixelsCount);
                }

                // get max whitedensity
                var maxWhiteDensity_Value = whitePixelDensity.MaxBy(entry => entry.Value).Value;
                var maxWhiteDensity_Pic = whitePixelDensity.Where(entry => entry.Value == maxWhiteDensity_Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //foreach (var frameWithMaxWhiteDensity in maxWhiteDensity_Pic.Keys)
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
                //Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");


            }




            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");



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

            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");

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

            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");

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


            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");


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

            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");

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

            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(CloudSemantic100x100_InvariantRepresentation)}");

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



        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void TestSemantic100x100_InvariantRepresentation(string experimentFolder)
        {
            List<Sample> trainingSamples = new List<Sample>();
            //List<Sample> trainingBigSamples = new List<Sample>();
            List<Sample> testingSamples = new List<Sample>();
            List<Sample> sourceSamples = new List<Sample>();

            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, NUM_IMAGES_PER_LABEL);

            // get images from MNIST set
            DataSet sourceSet = new DataSet(sourceMNIST);

            // scale the original datasource according IMAGE_WIDTH, IMAGE_HEIGHT
            DataSet sourceSet_scale = DataSet.ScaleSet(experimentFolder, IMAGE_WIDTH, IMAGE_HEIGHT, sourceSet, "sourceSet");
            // put source image into 100x100 image size
            DataSet sourceSetBigScale = DataSet.CreateTestSet(sourceSet_scale, 100, 100, Path.Combine(experimentFolder, "sourceSetBigScale"));


            // get % of sourceSet_scale to be testSet
            DataSet testSet_scale = sourceSet_scale.GetTestData(PER_TESTSET);

            // put test image into 100x100 image size
            DataSet testSetBigScale = DataSet.CreateTestSet(testSet_scale, 100, 100, Path.Combine(experimentFolder, "testSetBigScale"));


            Debug.WriteLine("Generating dataset ... ");


            // Creating the testing images from big scale image.
            var trainingImageFolderName = "TraingImageFolder";
            var listOfTrainingImage = Frame.GetConvFramesbyPixel(100, 100, IMAGE_WIDTH, IMAGE_HEIGHT, PIXEL_SHIFTED);

            DataSet trainingImage = new DataSet(new List<Image>());

            foreach (var img in sourceSetBigScale.Images)
            {
                string trainingImageFolder = Path.Combine(experimentFolder, trainingImageFolderName, $"{img.Label}");

                Utility.CreateFolderIfNotExist(trainingImageFolder);

                //testImage.SaveTo(Path.Combine(testExtractedFrameBigScaleFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));

                Dictionary<Frame, double> whitePixelDensity = new Dictionary<Frame, double>();
                foreach (var trainImg in listOfTrainingImage)
                {
                    double whitePixelsCount = img.HAI_FrameDensity(trainImg);
                    whitePixelDensity.Add(trainImg, whitePixelsCount);
                }

                // get max whitedensity
                var maxWhiteDensity_Value = whitePixelDensity.MaxBy(entry => entry.Value).Value;
                var maxWhiteDensity_Pic = whitePixelDensity.Where(entry => entry.Value == maxWhiteDensity_Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                //foreach (var frameWithMaxWhiteDensity in maxWhiteDensity_Pic.Keys)
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
                //Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");


            }




            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");



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

            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");

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

            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");

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


            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");


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

            Debug.WriteLine("aaaaaaaaaaaaaaaaaaa");

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

            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(TestSemantic100x100_InvariantRepresentation)}");

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


        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void TestSemanticOrigin_InvariantRepresentation(string experimentFolder)
        {
            List<Sample> trainingSamples = new List<Sample>();
            //List<Sample> trainingBigSamples = new List<Sample>();
            List<Sample> testingSamples = new List<Sample>();
            List<Sample> sourceSamples = new List<Sample>();

            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, NUM_IMAGES_PER_LABEL);

            // get images from MNIST set
            DataSet sourceSet = new DataSet(sourceMNIST);

            // scale the original datasource according IMAGE_WIDTH, IMAGE_HEIGHT
            DataSet sourceSet_scale = DataSet.ScaleSet(experimentFolder, IMAGE_WIDTH, IMAGE_HEIGHT, sourceSet, "sourceSet");


            //var listOfFrame_shift = Frame.GetConvFramesbyPixel(IMAGE_WIDTH, IMAGE_HEIGHT, 20, 20, 2);
            //string shift_trainingFolderName = "SHIFT_TrainingExtractedFrame";
            //List<Image> shift_Images = new List<Image>();

            //// Creating the training frames for each images and put them in folders.
            //foreach (var image in sourceSet_scale.Images)
            //{
            //    string extractedFrameFolder = Path.Combine(experimentFolder, shift_trainingFolderName, $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");


            //    Utility.CreateFolderIfNotExist(extractedFrameFolder);

            //    foreach (var frame in listOfFrame_shift)
            //    {
            //        if (image.IsRegionInDensityRange(frame, 20, 100))
            //        {
            //            if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
            //            {
            //                string savePath = Path.Combine(extractedFrameFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
            //                // binarize image with threshold 255/2
            //                image.SaveTo(savePath, frame, true);
            //                shift_Images.Add(new Image(savePath, image.Label));
            //                //frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
            //                //index += 1;
            //            }
            //        }
            //    }
            //}

            //Console.WriteLine("haha");




            // get % of sourceSet_scale to be testSet
            DataSet testSet_scale = sourceSet_scale.GetTestData(PER_TESTSET);

            //DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder, "testSet_100x100"));
            //DataSet scaledTrainSet = DataSet.CreateTestSet(sourceSet_32x32, 100, 100, Path.Combine(experimentFolder, "trainSet_100x100"));

            Debug.WriteLine("Generating dataset ... ");

            // write extracted/filtered frame from original dataset into frames for SP to learn all pattern (EX: 32x32 -> quadrants 16x16)
            var listOfFrame = Frame.GetConvFramesbyPixel(IMAGE_WIDTH, IMAGE_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT, PIXEL_SHIFTED);

            //int index = 0;
            //List<string> frameDensityList = new List<string>();
            string trainingFolderName = "TrainingExtractedFrame";
            string testinggFolderName = "TestingExtractedFrame";

            // Creating the training frames for each images and put them in folders.
            foreach (var image in sourceSet_scale.Images)
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
                            //frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
                            //index += 1;
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


            // Creating the testing frames for each images and put them in folders.
            //string testFolder = Path.Combine(experimentFolder, testinggFolderName);
            //listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, pixelShifted);
            foreach (var testImage in testSet_scale.Images)
            {
                string testExtractedFrameFolder = Path.Combine(experimentFolder, testinggFolderName, $"{testImage.Label}", $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");

                Utility.CreateFolderIfNotExist(testExtractedFrameFolder);

                testImage.SaveTo(Path.Combine(testExtractedFrameFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));

                foreach (var frame in listOfFrame)
                {
                    if (testImage.IsRegionInDensityRange(frame, 0, 100))
                    {
                        if (!DataSet.ExistImageInDataSet(testImage, testExtractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(testExtractedFrameFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
                            // binarize image with threshold 255/2
                            testImage.SaveTo(savePath, frame, true);
                            //frameDensityList.Add($"pattern {index}, Pixel Density {testImage.FrameDensity(frame, 255 / 2) * 100}");
                            //index += 1;
                        }
                    }
                }
            }

            // Create testing samples from the extracted frames.
            foreach (var testClassFolder in Directory.GetDirectories(Path.Combine(experimentFolder, testinggFolderName)))
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

            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(TestSemanticOrigin_InvariantRepresentation)}");

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


            double match = 0;
            Dictionary<string, List<string>> finalPredict = new Dictionary<string, List<string>>();

            foreach (var item in testLabel_SDRListIndexes)
            {
                if (!finalPredict.ContainsKey(item.Object))
                {
                    finalPredict.Add(item.Object, new List<string>());
                }

                string logFileName = Path.Combine(item.FramePath, @"..\", $"{item.FramePath.Split('\\').Last()}.log");
                TextWriterTraceListener myTextListener = new TextWriterTraceListener(logFileName);
                Trace.Listeners.Add(myTextListener);
                Trace.WriteLine($"Actual label: {item.Object}");
                Trace.WriteLine($"{(item.FramePath.Split('\\').Last())}");
                Trace.WriteLine("=======================================");
                string predictedObj = cls.HAI_PredictObj(item);
                Trace.Flush();
                Trace.Close();

                if (predictedObj.Equals(item.Object))
                {
                    match++;
                }

                finalPredict[item.Object].Add(predictedObj);

                //Debug.WriteLine($"Actual {item.Object} - Predicted {predictedObj}");
            }

            // Calculate Accuracy
            double numOfItems = testLabel_SDRListIndexes.Count();
            var accuracy = (match / numOfItems) * 100;
            testingSamples.Clear();

            string logResult = Path.Combine(experimentFolder, $"Prediction_Result.log");
            TextWriterTraceListener resultFile = new TextWriterTraceListener(logResult);
            Trace.Listeners.Add(resultFile);
            foreach (var r in finalPredict)
            {
                foreach (var p in r.Value)
                {
                    Trace.WriteLine($"Actual {r.Key}: Predicted {p}");
                }
            }
            Trace.WriteLine($"accuracy: {match}/{numOfItems} = {accuracy}%");
            Trace.Flush();
            Trace.Close();

        }





        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void Test_InvariantRepresentation(string experimentFolder)
        {
            #region Samples taking
            List<Sample> trainingSamples = new List<Sample>();
            List<Sample> trainingBigSamples = new List<Sample>();
            List<Sample> testingSamples = new List<Sample>();
            List<Sample> sourceSamples = new List<Sample>();

            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, 200);

            // generate 32x32 source MNISTDataSet
            int imageWidth = 32; int imageHeight = 32;
            int frameWidth = 16; int frameHeight = 16;
            int pixelShifted = 16;

            // use 28x28
            DataSet sourceSet = new DataSet(sourceMNIST);
            //DataSet sourceSet_32x32 = sourceSet;

            DataSet sourceSet_32x32 = DataSet.ScaleSet(experimentFolder, imageWidth, imageHeight, sourceSet, "sourceSet");
            DataSet testSet_32x32 = sourceSet_32x32.GetTestData(10);

            //DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder, "testSet_100x100"));
            //DataSet scaledTrainSet = DataSet.CreateTestSet(sourceSet_32x32, 100, 100, Path.Combine(experimentFolder, "trainSet_100x100"));

            Debug.WriteLine("Generating dataset ... ");

            // write extracted/filtered frame from 32x32 dataset into 4x4 for SP to learn all pattern
            var listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, pixelShifted);

            string extractedFrameFolder = "unknow";
            //string extractedFrameFolderBinarized = "unknow";

            //string extractedFrameFolder = Path.Combine(experimentFolder, "extractedFrameTraining");
            //string extractedBigFrameFolder = Path.Combine(experimentFolder, "extractedBigFrameTraining");
            //string extractedFrameFolderBinarized = Path.Combine(experimentFolder, "extractedFrameBinarized");

            int index = 0;
            List<string> frameDensityList = new List<string>();
            //string extractedFrameFolderTest = Path.Combine(experimentFolder, "extractedFrameTesting");

            // Creating the training frames for each images and put them in folders.
            foreach (var image in sourceSet_32x32.Images)
            {
                extractedFrameFolder = Path.Combine(experimentFolder, "TrainingExtractedFrame", $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");
                //extractedFrameFolderBinarized = Path.Combine(experimentFolder, "TrainingExtractedFrameBinarized", $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");

                Utility.CreateFolderIfNotExist(extractedFrameFolder);

                //Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolder, $"{image.Label}"));
                ////Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderBinarized, $"{image.Label}"));
                foreach (var frame in listOfFrame)
                {
                    if (image.IsRegionInDensityRange(frame, 5, 100))
                    {
                        if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(extractedFrameFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
                            //string savePathBinarized = Path.Combine(extractedFrameFolderBinarized, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}_binarize.png");
                            image.SaveTo(savePath, frame, true);
                            //image.SaveTo(savePathBinarized, frame);
                            frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
                            index += 1;
                        }
                    }
                }
            }

            // Create training samples from the extracted frames.
            foreach (var classFolder in Directory.GetDirectories(Path.Combine(experimentFolder, "TrainingExtractedFrame")))
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

                        //
                        // Calculate offset coordinates.
                        var tlX = coorOffsetList[0] = 0 - coorOffsetList[0];
                        var tlY = coorOffsetList[1] = 0 - coorOffsetList[1];
                        var brX = coorOffsetList[2] = imageWidth - coorOffsetList[2] - 1;
                        var brY = coorOffsetList[3] = imageHeight - coorOffsetList[3] - 1;

                        Sample sample = new Sample();
                        sample.Object = label;
                        sample.FramePath = imagePath;
                        sample.Position = new Frame(tlX, tlY, brX, brY);
                        trainingSamples.Add(sample);
                    }
                }
            }


            // Creating the testing frames for each images and put them in folders.
            string testFolder = Path.Combine(experimentFolder, "TestingExtractedFrame");
            listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, pixelShifted);
            foreach (var testImage in testSet_32x32.Images)
            {
                string testImageFolder = Path.Combine(testFolder, $"{testImage.Label}", $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");
                Utility.CreateFolderIfNotExist(testImageFolder);
                testImage.SaveTo(Path.Combine(testImageFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));

                foreach (var frame in listOfFrame)
                {
                    if (testImage.IsRegionInDensityRange(frame, 5, 100))
                    {
                        if (!DataSet.ExistImageInDataSet(testImage, extractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(testImageFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
                            testImage.SaveTo(savePath, frame, true);
                            frameDensityList.Add($"pattern {index}, Pixel Density {testImage.FrameDensity(frame, 255 / 2) * 100}");
                            index += 1;
                        }
                    }
                }
            }

            // Create testing samples from the extracted frames.
            foreach (var testClassFolder in Directory.GetDirectories(testFolder))
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

            #region creat testing frame for 100x100 image
            //// Creating the testing frames for each images and put them in folders.
            //Utility.CreateFolderIfNotExist(extractedFrameFolderTest);
            ////listOfFrame = Frame.GetConvFrames(80, 80, frameWidth, frameHeight, 10, 10);
            //listOfFrame = Frame.GetConvFramesbyPixel(100, 100, frameWidth, frameHeight, 4);
            //index = 0;
            //foreach (var testImage in scaledTestSet.Images)
            //{
            //    Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderTest, $"{testImage.ImagePath.Substring(testImage.ImagePath.Length - 5, 1)}", $"{testImage.Label}"));
            //    foreach (var frame in listOfFrame)
            //    {
            //        if (testImage.IsRegionInDensityRange(frame, 25, 80))
            //        {
            //            if (!DataSet.ExistImageInDataSet(testImage, extractedFrameFolderTest, frame))
            //            {
            //                string savePath = Path.Combine(extractedFrameFolderTest, $"{testImage.ImagePath.Substring(testImage.ImagePath.Length - 5, 1)}", $"{testImage.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

            //                testImage.SaveTo(savePath, frame, true);

            //                frameDensityList.Add($"pattern {index}, Pixel Density {testImage.FrameDensity(frame, 255 / 2) * 100}");
            //                index += 1;
            //            }
            //        }
            //    }
            //    index = 0;
            //}
            #endregion

            #region create traning frame for 100x100 image
            //// Creating the big training frames for each images and put them in folders.
            ////listOfFrame = Frame.GetConvFrames(80, 80, frameWidth, frameHeight, 10, 10);
            //var listOfBigFrame = Frame.GetConvFramesbyPixel(100, 100, imageWidth, imageHeight, 4);
            //index = 0;
            //foreach (var image in scaledTrainSet.Images)
            //{
            //    Utility.CreateFolderIfNotExist(Path.Combine(extractedBigFrameFolder, $"{image.Label}"));
            //    double minDensity = 25;
            //    string savePath = "";
            //restart:
            //    foreach (var frame in listOfBigFrame)
            //    {
            //        if (image.IsRegionInDensityRange(frame, minDensity, 80))
            //        {
            //            if (!DataSet.ExistImageInDataSet(image, extractedBigFrameFolder, frame))
            //            {

            //                savePath = Path.Combine(extractedBigFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

            //                image.SaveTo(savePath, frame, true);

            //                frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
            //                index += 1;
            //            }
            //        }
            //        if ((frame == listOfBigFrame.Last()) && string.IsNullOrEmpty(savePath))
            //        {
            //            minDensity -= 1;
            //            if (minDensity < 10)
            //            {
            //                break;
            //            }
            //            goto restart;
            //        }
            //    }
            //    index = 0;
            //}
            ////listOfBigFrame = Frame.GetConvFramesbyPixel(32, 32, 32, 32, 4);
            ////foreach (var image in sourceSet_32x32.Images)
            ////{
            ////    foreach (var frame in listOfBigFrame)
            ////    {
            ////        var savePath = Path.Combine(extractedBigFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}_{sourceSet_32x32.Images.IndexOf(image)}.png");
            ////        image.SaveTo(savePath, frame, true);
            ////    }
            ////}
            #endregion

            #region create big training samples
            //// Create big training samples from the extracted frames.
            //foreach (var classFolder in Directory.GetDirectories(extractedBigFrameFolder))
            //{
            //    string label = Path.GetFileName(classFolder);
            //    foreach (var imagePath in Directory.GetFiles(classFolder))
            //    {
            //        var fileName = Path.GetFileNameWithoutExtension(imagePath);
            //        var coordinatesString = fileName.Split('_').ToList();
            //        List<int> coorOffsetList = new List<int>();
            //        foreach (var coordinates in coordinatesString)
            //        {
            //            coorOffsetList.Add(int.Parse(coordinates));
            //        }

            //        //
            //        // Calculate offset coordinates.
            //        var tlX = coorOffsetList[0];
            //        var tlY = coorOffsetList[1];
            //        var brX = coorOffsetList[2];
            //        var brY = coorOffsetList[3];

            //        Sample sample = new Sample();
            //        sample.Object = label;
            //        sample.FramePath = imagePath;
            //        sample.Position = new Frame(tlX, tlY, brX, brY);
            //        trainingBigSamples.Add(sample);
            //    }
            //}
            #endregion

            DataSet trainingSet = new DataSet(Path.Combine(experimentFolder, "TrainingExtractedFrame"), true);
            //DataSet trainingBigSet = new DataSet(extractedBigFrameFolder);

            //a
            // Create the big testing frame
            //string[] digits = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            //foreach (var digit in digits)
            //{
            //    //
            //    // training images.
            //    string digitTrainingFolder = Path.Combine(experimentFolder, "sourceSet_32x32", digit);
            //    var trainingImages = Directory.GetFiles(digitTrainingFolder);

            //    foreach (string image in trainingImages)
            //    {
            //        Sample sample = new Sample();
            //        var imageName = Path.GetFileName(image);
            //        sample.FramePath = Path.Combine(digitTrainingFolder, imageName);
            //        sample.Object = digit;

            //        sourceSamples.Add(sample);
            //    }
            //}

            #endregion

            #region Config
            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(Test_InvariantRepresentation)}");
            int numColumns = 1024;

            #endregion

            #region Run experiment
            HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

            //var numUniqueInputs = trainingSamples.Count;

            Debug.WriteLine("Start training ...");

            LearningUnit learningUnit1 = new LearningUnit(16, 16, numColumns, "placeholder");
            learningUnit1.TrainingNewbornCycle(trainingSet);

            //
            // Add the stable SDRs to samples.
            List<Sample> samples = new List<Sample>();
            foreach (var trainingSample in trainingSamples)
            {
                var activeColumns = learningUnit1.Predict(trainingSample.FramePath);
                if (activeColumns != null && activeColumns.Length != 0)
                {
                    trainingSample.PixelIndicies = new int[activeColumns.Length];
                    trainingSample.PixelIndicies = activeColumns;
                    samples.Add(trainingSample);
                }
            }
            cls.LearnObj(samples);


            // Create and add SDRs for the testing samples.
            foreach (var testingSample in testingSamples)
            {
                var activeColumns = learningUnit1.Predict(testingSample.FramePath);
                if (activeColumns != null)
                {
                    testingSample.PixelIndicies = new int[activeColumns.Length];
                    testingSample.PixelIndicies = activeColumns;
                }
            }

            Debug.WriteLine("Running test ...");


            // Classifying each testing sample.
            //var testingSamplesDict = testingSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());
            var testingSamplesDict = testingSamples.Select(x => x).GroupBy(x => x.FramePath.Split('\\')[^2]).ToDictionary(group => group.Key, group => group.ToList());

            double match = 0;
            Dictionary<string, string> finalPredict = new Dictionary<string, string>();

            foreach (var item in testingSamplesDict)
            {
                string logFileName = Path.Combine(item.Value[0].FramePath, @"..\..", $"{(item.Value[0].FramePath).Split('\\')[^2]}_Frame_Prediction_16x16_1800-200_testNotInTrain_cycle200_scorefilter0.log");

                TextWriterTraceListener myTextListener = new TextWriterTraceListener(logFileName);
                Trace.Listeners.Add(myTextListener);
                Trace.WriteLine($"Actual label: {item.Value[0].Object}");
                Trace.WriteLine($"{(item.Key)}");
                Trace.WriteLine("=======================================");
                string predictedObj = cls.PredictObj(item.Value, 5);
                Trace.Flush();
                Trace.Close();

                if (predictedObj.Equals(item.Value[0].Object))
                {
                    match++;
                }

                if (!finalPredict.ContainsKey(item.Key))
                {
                    finalPredict.Add(item.Key, predictedObj);
                }

                Debug.WriteLine($"{item.Key}: {predictedObj}");

                //var savePathList = Directory.GetFiles(itemFolderPath).ToList();
                //List<string> results = new List<string>();
                //foreach (var path in savePathList)
                //{
                //    var activecolumns = learningunit2.predict(path);

                //    var sdrbinarray = learningunit2.tosdrbinarray(activecolumns);
                //    var res = cls.validateobj(activecolumns, 2);
                //    results.addrange(res);
                //}
                //var resultOrder = results.GroupBy(x => x)
                //                        .OrderByDescending(g => g.Count())
                //                        .Select(g => g.Key).ToList();
                //var bestResult = resultOrder.First();


                //if (bestResult == item.Key)
                //{
                //    match++;
                //}
            }



            // Calculate Accuracy
            double numOfItems = testingSamplesDict.Count();
            var accuracy = (match / numOfItems) * 100;
            testingSamples.Clear();
            testingSamplesDict.Clear();

            string logResult = Path.Combine(experimentFolder, $"Prediction_Result.log");
            TextWriterTraceListener resultFile = new TextWriterTraceListener(logResult);
            Trace.Listeners.Add(resultFile);
            foreach (var r in finalPredict)
            {
                Trace.WriteLine($"{r.Key}: {r.Value}");
            }
            Debug.WriteLine($"match: {match}/{numOfItems} = {accuracy}%");
            Debug.WriteLine("------------ END ------------");
            Trace.Flush();
            Trace.Close();
            #endregion

            //var predictedObj = cls.PredictObj2(item.Value, 10);



            //var itemFolderPath = Path.Combine(directoryPath, $"{item.Key}");
            //Utility.CreateFolderIfNotExist(itemFolderPath);
            //double bestPixelDensity = 0.0;
            //var testImages = scaledTestSet.Images.Where(x => x.Label == item.Key).ToList();
            //foreach (var testImage in testImages)
            //{
            //    Dictionary<Frame, double> frameDensityDict = new Dictionary<Frame, double>();
            //    double minDensity = 10.0;
            //    foreach (var obj in predictedObj)
            //    {
            //        var frame = obj.Position;
            //        double whitePixelDensity = testImage.FrameDensity(frame, minDensity);
            //        frameDensityDict.Add(frame, whitePixelDensity);
            //    }
            //    Frame chosenFrame = frameDensityDict.OrderByDescending(g => g.Value).Select(g => g.Key).First();
            //    var savePath = Path.Combine(itemFolderPath, $"{testImage.Label}.png");
            //    testImage.SaveTo(savePath, chosenFrame, true);
            //}

            //if (predictedObj.Equals(item.Key))
            //{
            //    match++;
            //}



            //        Debug.WriteLine($"{item.Key} predicted as ");

            //        Debug.WriteLine($"{predictedObj[0].Object} fsdgsdfg as ");
            //        //var savePathList = Directory.GetFiles(itemFolderPath).ToList();
            //        //List<string> results = new List<string>();
            //        //foreach (var path in savePathList)
            //        //{
            //        //    var activecolumns = learningunit2.predict(path);

            //        //    var sdrbinarray = learningunit2.tosdrbinarray(activecolumns);
            //        //    var res = cls.validateobj(activecolumns, 2);
            //        //    results.addrange(res);
            //        //}
            //        //var resultOrder = results.GroupBy(x => x)
            //        //                        .OrderByDescending(g => g.Count())
            //        //                        .Select(g => g.Key).ToList();
            //        //var bestResult = resultOrder.First();


            //        //if (bestResult == item.Key)
            //        //{
            //        //    match++;
            //        //}
            //    }

            //    //
            //    // Calculate Accuracy
            //    double numOfItems = testingSamplesDict.Count();
            //    var accuracy = (match / numOfItems)*100;
            //    testingSamples.Clear();
            //    testingSamplesDict.Clear();

            //    Debug.WriteLine($"match: {match}");
            //    Debug.WriteLine($"numOfItems: {numOfItems}");
            //    Debug.WriteLine($"accuracy: {accuracy}");
            // }

            // Debug.WriteLine("------------ END ------------");

        }



        ///// <summary>
        ///// Latest Experiment
        ///// </summary>
        ///// <param name="experimentFolder"></param>
        //private static void InvariantRepresentation(string experimentFolder)
        //{
        //    #region Samples taking
        //    List<Sample> trainingSamples = new List<Sample>();
        //    List<Sample> trainingBigSamples = new List<Sample>();
        //    List<Sample> testingSamples = new List<Sample>();
        //    List<Sample> sourceSamples = new List<Sample>();

        //    Utility.CreateFolderIfNotExist(experimentFolder);

        //    // Get the folder of MNIST archives tar.gz files.
        //    string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
        //    Utility.CreateFolderIfNotExist(sourceMNIST);
        //    Mnist.DataGen("MnistDataset", sourceMNIST, 15);

        //    // generate 32x32 source MNISTDataSet
        //    int imageWidth = 32; int imageHeight = 32;
        //    int frameWidth = 16; int frameHeight = 16;
        //    DataSet sourceSet = new DataSet(sourceMNIST);

        //    DataSet sourceSet_32x32 = DataSet.ScaleSet(experimentFolder, imageWidth, imageHeight, sourceSet, "sourceSet");
        //    DataSet testSet_32x32 = sourceSet_32x32.GetTestData(10);

        //    DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder, "testSet_100x100"));
        //    DataSet scaledTrainSet = DataSet.CreateTestSet(sourceSet_32x32, 100, 100, Path.Combine(experimentFolder, "trainSet_100x100"));

        //    Debug.WriteLine("Generating dataset ... ");

        //    // write extracted/filtered frame from 32x32 dataset into 4x4 for SP to learn all pattern
        //    //var listOfFrame = Frame.GetConvFrames(imageWidth, imageHeight, frameWidth, frameHeight, 4, 4);
        //    var listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, 4);
        //    string extractedFrameFolder = Path.Combine(experimentFolder, "extractedFrameTraining");
        //    string extractedBigFrameFolder = Path.Combine(experimentFolder, "extractedBigFrameTraining");
        //    string extractedFrameFolderBinarized = Path.Combine(experimentFolder, "extractedFrameBinarized");
        //    int index = 0;
        //    List<string> frameDensityList = new List<string>();

        //    string extractedFrameFolderTest = Path.Combine(experimentFolder, "extractedFrameTesting");

        //    // Creating the training frames for each images and put them in folders.
        //    foreach (var image in sourceSet_32x32.Images)
        //    {
        //        Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolder, $"{image.Label}"));
        //        //Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderBinarized, $"{image.Label}"));
        //        foreach (var frame in listOfFrame)
        //        {
        //            if (image.IsRegionInDensityRange(frame, 25, 80))
        //            {
        //                if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
        //                {
        //                    string savePath = Path.Combine(extractedFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

        //                    image.SaveTo(savePath, frame, true);

        //                    frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
        //                    index += 1;
        //                }
        //            }
        //        }
        //        index = 0;
        //    }

        //    // Create training samples from the extracted frames.
        //    foreach (var classFolder in Directory.GetDirectories(extractedFrameFolder))
        //    {
        //        string label = Path.GetFileName(classFolder);
        //        foreach (var imagePath in Directory.GetFiles(classFolder))
        //        {
        //            var fileName = Path.GetFileNameWithoutExtension(imagePath);
        //            var coordinatesString = fileName.Split('_').ToList();
        //            List<int> coorOffsetList = new List<int>();
        //            foreach (var coordinates in coordinatesString)
        //            {
        //                coorOffsetList.Add(int.Parse(coordinates));
        //            }

        //            //
        //            // Calculate offset coordinates.
        //            var tlX = coorOffsetList[0] = 0 - coorOffsetList[0];
        //            var tlY = coorOffsetList[1] = 0 - coorOffsetList[1];
        //            var brX = coorOffsetList[2] = imageWidth - coorOffsetList[2] - 1;
        //            var brY = coorOffsetList[3] = imageHeight - coorOffsetList[3] - 1;

        //            Sample sample = new Sample();
        //            sample.Object = label;
        //            sample.FramePath = imagePath;
        //            sample.Position = new Frame(tlX, tlY, brX, brY);
        //            trainingSamples.Add(sample);
        //        }
        //    }

        //    // Creating the testing frames for each images and put them in folders.
        //    Utility.CreateFolderIfNotExist(extractedFrameFolderTest);
        //    //listOfFrame = Frame.GetConvFrames(80, 80, frameWidth, frameHeight, 10, 10);
        //    listOfFrame = Frame.GetConvFramesbyPixel(100, 100, frameWidth, frameHeight, 4);
        //    index = 0;
        //    foreach (var testImage in scaledTestSet.Images)
        //    {
        //        Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderTest, $"{testImage.ImagePath.Substring(testImage.ImagePath.Length - 5, 1)}", $"{testImage.Label}"));
        //        foreach (var frame in listOfFrame)
        //        {
        //            if (testImage.IsRegionInDensityRange(frame, 25, 80))
        //            {
        //                if (!DataSet.ExistImageInDataSet(testImage, extractedFrameFolderTest, frame))
        //                {
        //                    string savePath = Path.Combine(extractedFrameFolderTest, $"{testImage.ImagePath.Substring(testImage.ImagePath.Length - 5, 1)}", $"{testImage.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

        //                    testImage.SaveTo(savePath, frame, true);

        //                    frameDensityList.Add($"pattern {index}, Pixel Density {testImage.FrameDensity(frame, 255 / 2) * 100}");
        //                    index += 1;
        //                }
        //            }
        //        }
        //        index = 0;
        //    }

        //    //
        //    // Creating the big training frames for each images and put them in folders.
        //    //listOfFrame = Frame.GetConvFrames(80, 80, frameWidth, frameHeight, 10, 10);
        //    var listOfBigFrame = Frame.GetConvFramesbyPixel(100, 100, imageWidth, imageHeight, 4);
        //    index = 0;
        //    foreach (var image in scaledTrainSet.Images)
        //    {
        //        Utility.CreateFolderIfNotExist(Path.Combine(extractedBigFrameFolder, $"{image.Label}"));
        //        double minDensity = 25;
        //        string savePath = "";
        //    restart:
        //        foreach (var frame in listOfBigFrame)
        //        {
        //            if (image.IsRegionInDensityRange(frame, minDensity, 80))
        //            {
        //                if (!DataSet.ExistImageInDataSet(image, extractedBigFrameFolder, frame))
        //                {

        //                    savePath = Path.Combine(extractedBigFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

        //                    image.SaveTo(savePath, frame, true);

        //                    frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
        //                    index += 1;
        //                }
        //            }
        //            if ((frame == listOfBigFrame.Last()) && string.IsNullOrEmpty(savePath))
        //            {
        //                minDensity -= 1;
        //                if (minDensity < 10)
        //                {
        //                    break;
        //                }
        //                goto restart;
        //            }
        //        }
        //        index = 0;
        //    }
        //    //listOfBigFrame = Frame.GetConvFramesbyPixel(32, 32, 32, 32, 4);
        //    //foreach (var image in sourceSet_32x32.Images)
        //    //{
        //    //    foreach (var frame in listOfBigFrame)
        //    //    {
        //    //        var savePath = Path.Combine(extractedBigFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}_{sourceSet_32x32.Images.IndexOf(image)}.png");
        //    //        image.SaveTo(savePath, frame, true);
        //    //    }
        //    //}

        //    //
        //    // Create big training samples from the extracted frames.
        //    foreach (var classFolder in Directory.GetDirectories(extractedBigFrameFolder))
        //    {
        //        string label = Path.GetFileName(classFolder);
        //        foreach (var imagePath in Directory.GetFiles(classFolder))
        //        {
        //            var fileName = Path.GetFileNameWithoutExtension(imagePath);
        //            var coordinatesString = fileName.Split('_').ToList();
        //            List<int> coorOffsetList = new List<int>();
        //            foreach (var coordinates in coordinatesString)
        //            {
        //                coorOffsetList.Add(int.Parse(coordinates));
        //            }

        //            //
        //            // Calculate offset coordinates.
        //            var tlX = coorOffsetList[0];
        //            var tlY = coorOffsetList[1];
        //            var brX = coorOffsetList[2];
        //            var brY = coorOffsetList[3];

        //            Sample sample = new Sample();
        //            sample.Object = label;
        //            sample.FramePath = imagePath;
        //            sample.Position = new Frame(tlX, tlY, brX, brY);
        //            trainingBigSamples.Add(sample);
        //        }
        //    }


        //    DataSet trainingSet = new DataSet(extractedFrameFolder);
        //    DataSet trainingBigSet = new DataSet(extractedBigFrameFolder);

        //    //a
        //    // Create the big testing frame
        //    //string[] digits = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        //    //foreach (var digit in digits)
        //    //{
        //    //    //
        //    //    // training images.
        //    //    string digitTrainingFolder = Path.Combine(experimentFolder, "sourceSet_32x32", digit);
        //    //    var trainingImages = Directory.GetFiles(digitTrainingFolder);

        //    //    foreach (string image in trainingImages)
        //    //    {
        //    //        Sample sample = new Sample();
        //    //        var imageName = Path.GetFileName(image);
        //    //        sample.FramePath = Path.Combine(digitTrainingFolder, imageName);
        //    //        sample.Object = digit;

        //    //        sourceSamples.Add(sample);
        //    //    }
        //    //}

        //    #endregion

        //    #region Config
        //    Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(InvariantRepresentation)}");

        //    int inputBits = 256;
        //    int numColumns = 1024;

        //    #endregion

        //    #region Run experiment
        //    HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

        //    var numUniqueInputs = trainingSamples.Count;

        //    Debug.WriteLine("Start training ...");

        //    LearningUnit learningUnit1 = new LearningUnit(16, 16, numColumns, "placeholder");
        //    //LearningUnit learningUnit2 = new LearningUnit(32, 32, numColumns*4, "placeholder");
        //    learningUnit1.TrainingNewbornCycle(trainingSet);
        //    //learningUnit2.TrainingNewbornCycle(trainingBigSet);

        //    //
        //    // Add the stable SDRs to samples.
        //    List<Sample> samples = new List<Sample>();
        //    foreach (var trainingSample in trainingSamples)
        //    {
        //        //var lyrOut1 = layer1.Compute(trainingSample.FramePath, false);
        //        //var activeColumns = layer1.GetResult("sp1") as int[];

        //        var activeColumns = learningUnit1.Predict(trainingSample.FramePath);
        //        if (activeColumns != null && activeColumns.Length != 0)
        //        {
        //            trainingSample.PixelIndicies = new int[activeColumns.Length];
        //            trainingSample.PixelIndicies = activeColumns;
        //            samples.Add(trainingSample);
        //        }
        //    }
        //    cls.LearnObj(samples);

        //    //List<Sample> bigSamples = new List<Sample>();
        //    //foreach (var bigSample in trainingBigSamples)
        //    //{
        //    //    var activeColumns = learningUnit2.Predict(bigSample.FramePath);
        //    //    var sdrBinArray = learningUnit2.ToSDRBinArray(activeColumns);
        //    //    if (activeColumns != null && activeColumns.Length != 0)
        //    //    {
        //    //        bigSample.PixelIndicies = new int[activeColumns.Length];
        //    //        bigSample.PixelIndicies = activeColumns;
        //    //        bigSamples.Add(bigSample);
        //    //    }
        //    //}
        //    //cls.LearnMnistObj(bigSamples);


        //    Debug.WriteLine("Running test ...");

        //    // Create testing samples from the extracted frames.
        //    string[] directories = System.IO.Directory.GetDirectories(extractedFrameFolderTest, "*", System.IO.SearchOption.TopDirectoryOnly);
        //    var directoryCount = 0;
        //    foreach (string directory in directories)
        //    {
        //        var directoryPath = Path.Combine(experimentFolder, $"predictedFrames_{directoryCount}");
        //        directoryCount++;
        //        Utility.CreateFolderIfNotExist(directoryPath);
        //        foreach (var classFolder in Directory.GetDirectories(directory))
        //        {
        //            string label = Path.GetFileName(classFolder);
        //            foreach (var imagePath in Directory.GetFiles(classFolder))
        //            {
        //                var fileName = Path.GetFileNameWithoutExtension(imagePath);
        //                var coordinates = fileName.Split('_');
        //                var tlX = int.Parse(coordinates[0]);
        //                var tlY = int.Parse(coordinates[1]);
        //                var blX = int.Parse(coordinates[2]);
        //                var brY = int.Parse(coordinates[3]);
        //                Sample sample = new Sample();
        //                sample.Object = label;
        //                sample.FramePath = imagePath;
        //                sample.Position = new Frame(tlX, tlY, blX, brY);
        //                testingSamples.Add(sample);
        //            }
        //        }

        //        //
        //        // Create and add SDRs for the testing samples.
        //        foreach (var testingSample in testingSamples)
        //        {
        //            //var lyrOut1 = layer1.Compute(testingSample.FramePath, false);
        //            //var activeColumns = layer1.GetResult("sp1") as int[];

        //            var activeColumns = learningUnit1.Predict(testingSample.FramePath);
        //            if (activeColumns != null)
        //            {
        //                testingSample.PixelIndicies = new int[activeColumns.Length];
        //                testingSample.PixelIndicies = activeColumns;
        //            }
        //        }

        //        //
        //        // Classifying each testing sample.
        //        var testingSamplesDict = testingSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());
        //        double match = 0;
        //        foreach (var item in testingSamplesDict)
        //        {
        //            var predictedObj = cls.PredictObj(item.Value, 5);
        //            //var predictedObj = cls.PredictObj2(item.Value, 10);
        //            var itemFolderPath = Path.Combine(directoryPath, $"{item.Key}");
        //            Utility.CreateFolderIfNotExist(itemFolderPath);
        //            double bestPixelDensity = 0.0;
        //            var testImages = scaledTestSet.Images.Where(x => x.Label == item.Key).ToList();
        //            foreach (var testImage in testImages)
        //            {
        //                Dictionary<Frame, double> frameDensityDict = new Dictionary<Frame, double>();
        //                double minDensity = 10.0;
        //                foreach (var obj in predictedObj)
        //                {
        //                    var frame = obj.Position;
        //                    double whitePixelDensity = testImage.FrameDensity(frame, minDensity);
        //                    frameDensityDict.Add(frame, whitePixelDensity);
        //                }
        //                Frame chosenFrame = frameDensityDict.OrderByDescending(g => g.Value).Select(g => g.Key).First();
        //                var savePath = Path.Combine(itemFolderPath, $"{testImage.Label}.png");
        //                testImage.SaveTo(savePath, chosenFrame, true);             
        //            }

        //            if (predictedObj.Equals(item.Key))
        //            {
        //                match++;
        //            }



        //            Debug.WriteLine($"{item.Key} predicted as ");

        //            Debug.WriteLine($"{predictedObj[0].Object} fsdgsdfg as ");
        //            //var savePathList = Directory.GetFiles(itemFolderPath).ToList();
        //            //List<string> results = new List<string>();
        //            //foreach (var path in savePathList)
        //            //{
        //            //    var activecolumns = learningunit2.predict(path);

        //            //    var sdrbinarray = learningunit2.tosdrbinarray(activecolumns);
        //            //    var res = cls.validateobj(activecolumns, 2);
        //            //    results.addrange(res);
        //            //}
        //            //var resultOrder = results.GroupBy(x => x)
        //            //                        .OrderByDescending(g => g.Count())
        //            //                        .Select(g => g.Key).ToList();
        //            //var bestResult = resultOrder.First();


        //            //if (bestResult == item.Key)
        //            //{
        //            //    match++;
        //            //}
        //        }

        //        //
        //        // Calculate Accuracy
        //        double numOfItems = testingSamplesDict.Count();
        //        var accuracy = (match / numOfItems)*100;
        //        testingSamples.Clear();
        //        testingSamplesDict.Clear();

        //        Debug.WriteLine($"match: {match}");
        //        Debug.WriteLine($"numOfItems: {numOfItems}");
        //        Debug.WriteLine($"accuracy: {accuracy}");
        //    }

        //    Debug.WriteLine("------------ END ------------");
        //    #endregion
        //}

        private static void SPCapacityTest()
        {
            Dictionary<string, object> settings = new Dictionary<string, object>()
            {
                { "W", 15},
                { "N", 100},
                { "Radius", -1.0},
                { "MinVal", 0.0},
                { "Periodic", false},
                { "Name", "scalar"},
                { "ClipInput", false},
                { "MaxVal", (double)600}
            };

            EncoderBase encoder = new ScalarEncoder(settings);

            bool isInStableState = false;
            HtmConfig htm = new HtmConfig(new int[] { 100 }, new int[] { 1024 });
            Connections conn = new(htm);
            HomeostaticPlasticityController hpc = new HomeostaticPlasticityController(conn, 600 * 100, (isStable, numPatterns, actColAvg, seenInputs) =>
            {
                if (isStable)
                    // Event should be fired when entering the stable state.
                    Debug.WriteLine($"STABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                else
                    // Ideal SP should never enter unstable state after stable state.
                    Debug.WriteLine($"INSTABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");

                // We are not learning in instable state.
                isInStableState = isStable;

                // Clear active and predictive cells.
                //tm.Reset(mem);
            }, numOfCyclesToWaitOnChange: 100);
            SpatialPooler sp = new SpatialPooler(hpc);
            sp.Init(conn);
            while (!isInStableState)
            {
                for (double i = 0; i < 600; i += 1)
                {
                    sp.Compute(encoder.Encode(i), true);
                }
            }
            hpc.TraceState();
        }
    }
}
