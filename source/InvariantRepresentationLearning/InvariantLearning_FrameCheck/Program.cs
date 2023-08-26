﻿using NeoCortexApi;
using NeoCortexApi.Entities;
using System.Diagnostics;
using InvariantLearning_FrameCheck;
using Invariant.Entities;
using System.Collections.Concurrent;
using NeoCortexApi.Encoders;
using HtmImageEncoder;
using NeoCortexApi.Classifiers;
using NeoCortexApi.Network;

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
        public static int MAX_CYCLE = 10;
        public static int NUM_IMAGES_PER_LABEL = 20;
        public static int PER_TESTSET = 10;


        public static void Main()
        {
            string experimentTime = DateTime.UtcNow.ToLongDateString().Replace(", ", " ") + "_" + DateTime.UtcNow.ToLongTimeString().Replace(":", "-");

            //string experimentTime = DateTime.UtcNow.ToShortDateString().ToString().Replace('/', '-');
            Console.WriteLine($"HtmInvariantLearning_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL*10}-{PER_TESTSET}%_Cycle{MAX_CYCLE}_{experimentTime}");
            TestSemantic_InvariantRepresentation($"HtmInvariantLearning_{FRAME_WIDTH}x{FRAME_HEIGHT}_{NUM_IMAGES_PER_LABEL*10}-{PER_TESTSET}%_Cycle {MAX_CYCLE}_{experimentTime}");
        }


        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void TestSemantic_InvariantRepresentation(string experimentFolder)
        {
            #region Samples taking
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

            // get % of sourceSet_scale to be testSet
            DataSet testSet_scale = sourceSet_scale.GetTestData(PER_TESTSET);

            //DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder, "testSet_100x100"));
            //DataSet scaledTrainSet = DataSet.CreateTestSet(sourceSet_32x32, 100, 100, Path.Combine(experimentFolder, "trainSet_100x100"));

            Debug.WriteLine("Generating dataset ... ");

            // write extracted/filtered frame from original dataset into frames for SP to learn all pattern (EX: 32x32 -> quadrants 16x16)
            var listOfFrame = Frame.GetConvFramesbyPixel(IMAGE_WIDTH, IMAGE_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT, PIXEL_SHIFTED);

            //string extractedFrameFolder = "unknow";

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
            #endregion

            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(TestSemantic_InvariantRepresentation)}");

            #region Run experiment
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



            //Dictionary<string, List<List<int>>> trainLabel_SDRList = new Dictionary<string, List<List<int>>>();
            //foreach (var imageName in trainImageName_SDRFrames)
            //{
            //    string label = imageName.Key.Split('_')[1];
            //    if (!trainLabel_SDRList.ContainsKey(label))
            //    {
            //        trainLabel_SDRList.Add(label, new List<List<int>>());
            //    }
            //    List<int> combineSDRFrames = new List<int>();
            //    foreach (int[] i in imageName.Value)
            //    {
            //        combineSDRFrames.AddRange(i);
            //    }
            //    trainLabel_SDRList[label].Add(combineSDRFrames);
            //}

            ////cls.LearnObj(trainLabel_SDRList);

            ////foreach (var imageName in imageName_SDRFrames)
            ////{
            ////    if (!imageName_SDRList.ContainsKey(imageName.Key))
            ////    {
            ////        imageName_SDRList.Add(imageName.Key, new List<int>());
            ////    }
            ////    foreach (int[] i in imageName.Value)
            ////    {
            ////        imageName_SDRList[imageName.Key].AddRange(i);
            ////    }
            ////}

            ////var labelName_SDRList = imageName_SDRList.Select(x => x).GroupBy(x => x.Key.Split('_')[1]).ToDictionary(g => g.Key, g => g.ToList());


            ////foreach (var trainingSample in trainingSamples)
            ////{
            ////    //Console.WriteLine(Path.GetDirectoryName(trainingSample.FramePath).Split('\\').Last());

            ////    //Path.GetFileName(trainingSample.FramePath);

            ////    foreach (var imageFolder in Directory.GetDirectories(trainingSample.FramePath))
            ////    {

            ////        //Console.WriteLine(Path.GetDirectoryName(trainingSample.FramePath).Split('\\').Last());

            ////        var activeColumns = learningUnit1.Predict(trainingSample.FramePath);
            ////    if (activeColumns != null && activeColumns.Length != 0)
            ////    {
            ////        trainingSample.PixelIndicies = new int[activeColumns.Length];
            ////        trainingSample.PixelIndicies = activeColumns;
            ////        samples.Add(trainingSample);
            ////    }
            ////}

            ////cls.LearnObj(samples);


            //// Create and add SDRs for the testing samples.
            //foreach (var testingSample in testingSamples)
            //{
            //    var activeColumns = learningUnit1.Predict(testingSample.FramePath);
            //    if (activeColumns != null)
            //    {
            //        testingSample.PixelIndicies = new int[activeColumns.Length];
            //        testingSample.PixelIndicies = activeColumns;
            //    }
            //}

            //// Semantic array for each ImageName in Testing Set (combine all the SDR frames to be the unique SDR)
            //var testImageName_SDRFrames = testingSamples.Select(x => x).GroupBy(x => Path.GetDirectoryName(x.FramePath).Split('\\').Last()).ToDictionary(g => g.Key, g => g.Select(x => x.PixelIndicies).ToList());
            //Dictionary<string, List<List<int>>> testLabel_SDRList = new Dictionary<string, List<List<int>>>();
            //foreach (var imageName in testImageName_SDRFrames)
            //{
            //    string label = imageName.Key.Split('_')[1];
            //    if (!testLabel_SDRList.ContainsKey(label))
            //    {
            //        testLabel_SDRList.Add(label, new List<List<int>>());
            //    }
            //    List<int> combineSDRFrames = new List<int>();
            //    foreach (int[] i in imageName.Value)
            //    {
            //        combineSDRFrames.AddRange(i);
            //    }
            //    testLabel_SDRList[label].Add(combineSDRFrames);
            //}

            ////////////STOP AT HERE

            //Debug.WriteLine("Running test ...");

            ////// Compression SDR to store indexes of on bit
            ////Dictionary<string, List<List<int>>> trainLabel_SDRListIndexes = new Dictionary<string, List<List<int>>>();
            ////foreach (var trainLabel in trainLabel_SDRList)
            ////{
            ////    string label = trainLabel.Key;
            ////    if (!trainLabel_SDRListIndexes.ContainsKey(label))
            ////    {
            ////        trainLabel_SDRListIndexes.Add(label, new List<List<int>>());
            ////    }
            ////    foreach (List<int> sdr in trainLabel.Value)
            ////    {
            ////        List<int> SDRIndexes = new List<int>();
            ////        for (int i = 0; i < sdr.Count; i += 1)
            ////        {
            ////            if (sdr[i] > 0)
            ////            {
            ////                SDRIndexes.Add(i);
            ////            }
            ////        }
            ////        trainLabel_SDRListIndexes[label].Add(SDRIndexes);
            ////    }
            ////}



            //Debug.WriteLine("Running test ...");


            ////Dictionary<Sample, double> similarityScore = new Dictionary<Sample, double>();
            ////foreach (var trainingIndicies in trainingSamplesIndicies)
            ////{
            ////    double similarity = MathHelpers.CalcArraySimilarity(testingSamplesIndicies.PixelIndicies, trainingIndicies.PixelIndicies);
            ////    if (!similarityScore.ContainsKey(trainingIndicies))
            ////    {
            ////        similarityScore.Add(trainingIndicies, similarity);
            ////    }

            ////    //if (similarity > maxSimilarity)
            ////    //{
            ////    //    maxSimilarity = similarity;
            ////    //    results.Add(trainingIndicies);
            ////    //}

            ////    //var numOfSameBitsPct = testingSamplesIndicies.Intersect(trainingIndicies).Count();
            ////    //int numOfBits = trainingIndicies.Count();
            ////    //double similarity = ((double) numOfSameBitsPct/ (double) numOfBits)*100;

            ////    //if (numOfSameBitsPct >= maxSameBits /*similarity >= 50*/)
            ////    //{
            ////    //    maxSameBits = numOfSameBitsPct;
            ////    //    results.Add(trainingIndicies);
            ////    //}
            ////}







            //// Classifying each testing sample.
            ////var testingSamplesDict = testingSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());
            //var testingSamplesDict = testingSamples.Select(x => x).GroupBy(x => x.FramePath.Split('\\')[^2]).ToDictionary(group => group.Key, group => group.ToList());

            //double match = 0;
            //Dictionary<string, string> finalPredict = new Dictionary<string, string>();

            //foreach (var item in testingSamplesDict)
            //{
            //    string logFileName = Path.Combine(item.Value[0].FramePath, @"..\..", $"{(item.Value[0].FramePath).Split('\\')[^2]}_Frame_Prediction_16x16_1800-200_testNotInTrain_cycle200_scorefilter0.log");

            //    TextWriterTraceListener myTextListener = new TextWriterTraceListener(logFileName);
            //    Trace.Listeners.Add(myTextListener);
            //    Trace.WriteLine($"Actual label: {item.Value[0].Object}");
            //    Trace.WriteLine($"{(item.Key)}");
            //    Trace.WriteLine("=======================================");
            //    string predictedObj = cls.PredictObj(item.Value, 5);
            //    Trace.Flush();
            //    Trace.Close();

            //    if (predictedObj.Equals(item.Value[0].Object))
            //    {
            //        match++;
            //    }

            //    if (!finalPredict.ContainsKey(item.Key))
            //    {
            //        finalPredict.Add(item.Key, predictedObj);
            //    }

            //    Debug.WriteLine($"{item.Key}: {predictedObj}");
            //}



            //// Calculate Accuracy
            //double numOfItems = testingSamplesDict.Count();
            //var accuracy = (match / numOfItems) * 100;
            //testingSamples.Clear();
            //testingSamplesDict.Clear();

            //string logResult = Path.Combine(experimentFolder, $"Prediction_Result.log");
            //TextWriterTraceListener resultFile = new TextWriterTraceListener(logResult);
            //Trace.Listeners.Add(resultFile);
            //foreach (var r in finalPredict)
            //{
            //    Trace.WriteLine($"{r.Key}: {r.Value}");
            //}
            //Debug.WriteLine($"match: {match}/{numOfItems} = {accuracy}%");
            //Debug.WriteLine("------------ END ------------");
            //Trace.Flush();
            //Trace.Close();
            #endregion

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