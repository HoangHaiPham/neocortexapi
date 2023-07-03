using NeoCortexApi;
using NeoCortexApi.Entities;
using System.Diagnostics;
using InvariantLearning;
using Invariant.Entities;
using System.Collections.Concurrent;
using NeoCortexApi.Encoders;
using System.Threading;

namespace InvariantLearning
{
    public class InvariantLearning
    {
        public static void Main()
        {
            // OLD
            string experimentTime = DateTime.UtcNow.ToLongDateString().ToString().Replace(',',' ') +" "+DateTime.UtcNow.ToLongTimeString().ToString().Replace(':','_');
            // ExperimentEvaluatateImageClassification($"EvaluateImageClassification {experimentTime}");

            //string experimentTime = DateTime.UtcNow.ToShortDateString().ToString().Replace('/', '-');
            // Invariant Learning Experiment
            InvariantRepresentation($"HtmInvariantLearning {experimentTime}");

        }

        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void InvariantRepresentation(string experimentFolder)
        {
            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, 10);

            // generate 32x32 source MNISTDataSet
            int width = 32; int height = 32;
            int frameWidth = 16; int frameHeight = 16;
            int pixelShifted = 16;

            DataSet sourceSet = new DataSet(sourceMNIST);
        
            DataSet sourceSet_32x32 = DataSet.ScaleSet(experimentFolder, width, height, sourceSet , "sourceSet");
            DataSet testSet_32x32 = sourceSet_32x32.GetTestData(20);

            DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder,"testSet_100x100"));

            // write extracted/filtered frame from 32x32 dataset into 4x4 for SP to learn all pattern
            var listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, pixelShifted);
            // string extractedFrameFolder = Path.Combine(experimentFolder, "extractedFrame");
            // string extractedFrameFolderBinarized = Path.Combine(experimentFolder, "extractedFrameBinarized");
            string extractedFrameFolder = "unknow";
            string extractedFrameFolderBinarized = "unknow";

            int index = 0;
            List<string> frameDensityList = new List<string>();
            foreach (var image in sourceSet_32x32.Images)
            {

                extractedFrameFolder = Path.Combine(experimentFolder, "TrainingExtractedFrame", $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");
                extractedFrameFolderBinarized = Path.Combine(experimentFolder, "TrainingExtractedFrameBinarized", $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");
                
                // Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolder, $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}"));
                // Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderBinarized, $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}"));

                Utility.CreateFolderIfNotExist(extractedFrameFolder);
                Utility.CreateFolderIfNotExist(extractedFrameFolderBinarized);

                foreach (var frame in listOfFrame)
                {
                    if (image.IsRegionInDensityRange(frame, 10, 100))
                    {
                        if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(extractedFrameFolder, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");
                            string savePathBinarized = Path.Combine(extractedFrameFolderBinarized, $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}_binarize.png");
                            image.SaveTo(savePath, frame, true);
                            image.SaveTo(savePathBinarized, frame);
                            frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
                            index += 1;
                        }
                    }
                }
            }
            File.WriteAllLines(Path.Combine(experimentFolder, "TrainingExtractedFrame", "PixelDensity.txt"), frameDensityList.ToArray());
            DataSet trainingExtractedFrameSet = new DataSet(Path.Combine(experimentFolder, "TrainingExtractedFrameBinarized"), true);

            // Learning the filtered frame set with SP
            LearningUnit spLayer1 = new LearningUnit(32,32,2048,experimentFolder);
            spLayer1.TrainingNewbornCycle(trainingExtractedFrameSet);
            // spLayer1.TrainingNormal(extractedFrameSet, 1);

            string extractedImageSource = Path.Combine(experimentFolder, "extractedSet");
            Utility.CreateFolderIfNotExist(extractedImageSource);

            // Saving representation/semantic array with its label in files

            // NEED TO MODIFY HERE TO TRAINING FRAM, NOT TESTSET, FOLDER STRUCTURE
            Dictionary<string, List<int[]>> lib = new Dictionary<string, List<int[]>>();
            foreach (var image in sourceSet_32x32.Images)
            {
                string extractedFrameFolderofImage = Path.Combine(extractedImageSource, $"{image.Label}", $"Label_{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");
                Utility.CreateFolderIfNotExist(extractedFrameFolderofImage);
                if (!lib.ContainsKey(image.Label))
                {
                    lib.Add(image.Label, new List<int[]>());
                }
                int[] current = new int[spLayer1.columnDim];
                foreach (var frame in listOfFrame)
                {
                    if (image.IsRegionInDensityRange(frame, 10, 100))
                    {
                        string frameImage = Path.Combine(extractedFrameFolderofImage, $"{frame.tlX}-{frame.tlY}_{frame.brX}-{frame.brY}.png");
                        image.SaveTo(frameImage, frame, true);
                        int[] trainFrameSDR = spLayer1.Predict(frameImage);
                        //current = Utility.AddArray(current, trainFrameSDR);

                        lib[image.Label].Add(trainFrameSDR);
                    }
                }
                // from Toan -> build semantic array
                //lib[image.Label].Add(current);
            }

            Console.WriteLine("test");

            // Using trained SP to create semantic arrays for label 
            //Dictionary<string, List<int[]>> semanticArrayTrain = new Dictionary<string, List<int[]>>();
            //foreach (var l in lib)
            //{
            //    var label = l.Key;
            //    if (!semanticArrayTrain.ContainsKey(label))
            //    {
            //        semanticArrayTrain.Add(label, new List<int[]>());
            //    }
            //    int[] sdr_sum = new int[spLayer1.columnDim];
            //    foreach (int[] v in l.Value)
            //    {
            //        sdr_sum = Utility.AddArray(sdr_sum, v);
            //    }
            //    semanticArrayTrain[label].Add(sdr_sum);
            //}


            foreach (var a in lib)
            {
                var temp_label = a.Key;
                using (StreamWriter sw = new StreamWriter(Path.Combine(extractedImageSource, $"{temp_label}.txt")))
                {
                    foreach (var s in a.Value)
                    {
                        sw.WriteLine(string.Join(',', s) + "\n");
                    }
                    // var curKey = semanticArrayTrain.FirstOrDefault(x => x.Key == temp_label);
                    //foreach (var smA in curKey.Value)
                    //{
                    //    sw.WriteLine(string.Join(',', smA) + "\n");
                    //}
                }
            }


            // Testing section, calculate accuracy
            string testFolder = Path.Combine(experimentFolder, "Test");
            Utility.CreateFolderIfNotExist(testFolder);
            int match = 0;
            listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, pixelShifted);

            //Trace.Listeners.Add(new TextWriterTraceListener("new_16x16_train100_test20_cycle3.log"));
            //Trace.AutoFlush = true;
            //Trace.Indent();
            //Trace.WriteLine("============= Entering Main =============");

            foreach (var testImage in testSet_32x32.Images)
            {
                string testImageFolder = Path.Combine(testFolder, $"{testImage.Label}", $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");
                Utility.CreateFolderIfNotExist(testImageFolder);
                testImage.SaveTo(Path.Combine(testImageFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_origin.png"));

                int[] current = new int[spLayer1.columnDim];


                var testActualLabel = testImage.Label;

                TextWriterTraceListener myTextListener = new TextWriterTraceListener(Path.Combine(testImageFolder, $"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}_Frame_Prediction_16x16_testInTrain_100.log"));
                Trace.Listeners.Add(myTextListener);
                //Trace.AutoFlush = true;
                //Trace.Indent();
                Trace.WriteLine($"Actual label: {testActualLabel}");
                Trace.WriteLine($"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");
                Console.WriteLine($"Label_{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");

                foreach (var frame in listOfFrame)
                {
                    string frameImage = Path.Combine(testImageFolder, $"{frame.tlX}-{frame.tlY}_{frame.brX}-{frame.brY}.png");
                    testImage.SaveTo(frameImage, frame, true);

                    Console.WriteLine($"Frame: {frame.tlX}-{frame.tlY}_{frame.brX}-{frame.brY}");
                    Trace.WriteLine($"Frame: {frame.tlX}-{frame.tlY}_{frame.brX}-{frame.brY}");

                    if (testImage.IsRegionInDensityRange(frame, 10, 100))
                    {
                        // get SDR of each frame
                        int[] testFrameSDR = spLayer1.Predict(frameImage);

                        // compare similarity for predicting each frame
                        string testPredictedLabel = "unknow";
                        List<Dictionary<string, string>> testSimilarityListFrame = new List<Dictionary<string, string>>();
                        foreach (var digitClass in lib)
                        {
                            string currentLabel = digitClass.Key;
                            foreach (var entry in digitClass.Value)
                            {
                                double arrayGeometricDistance = Utility.CalArrayUnion(entry, testFrameSDR);
                                testSimilarityListFrame.Add(new Dictionary<string, string>()
                                {
                                    {"Label", currentLabel},
                                    {"Value", Convert.ToString(arrayGeometricDistance)}
                                });
                            }
                        }

                        // get top 3 lowest arrayGeometricDistance
                        // sorted from lowest arrayGeometricDistance to highest
                        int topN_test = 3;
                        var topN_testSimilarity = testSimilarityListFrame.OrderBy(dict => Convert.ToDouble(dict["Value"])).ThenByDescending(dict => dict["Label"]).Take(topN_test);

                        // get # of times each lable is predicted
                        Dictionary<string, double> testCountFreq = new Dictionary<string, double>();
                        foreach (var s in topN_testSimilarity)
                        {
                            if (!testCountFreq.ContainsKey(s["Label"]))
                            {
                                testCountFreq.Add(s["Label"], 0);
                            }
                            testCountFreq[s["Label"]] += 1;
                            Console.WriteLine($"Label {s["Label"]} - Similarity {s["Value"]}");
                            Trace.WriteLine($"Label {s["Label"]} - Similarity {s["Value"]}");
                        }

                        // get % of prediction for each label
                        foreach (var freq in testCountFreq)
                        {
                            Console.WriteLine($"% of label {freq.Key}: {Math.Round(100 * freq.Value / topN_test)} %");
                            Trace.WriteLine($"% of label {freq.Key}: {Math.Round(100 * freq.Value / topN_test)} %");
                        }

                        // get predictedLabel based on the highest %
                        // if % of labels are equal -> take the one with lowest arrayGeometricDistance, which has been already sorted in top3_similarity
                        testPredictedLabel = testCountFreq.OrderByDescending(x => x.Value).First().Key;

                        Console.WriteLine($"{testActualLabel} predicted as {testPredictedLabel}");
                        Trace.WriteLine($"{testActualLabel} predicted as {testPredictedLabel}");
                        Console.WriteLine("=======================================");
                        Trace.WriteLine("=======================================");

                        // Generate semantic array for test image
                        current = Utility.AddArray(current, spLayer1.Predict(frameImage));
                    }        
                }
                //Thread.Sleep(2000);
                //Trace.Unindent();
                Trace.Flush();
                Trace.Close();




                #region runTest

                //    string actualLabel = testImage.Label;
                //    string predictedLabel = "unknow";
                //    //double lowestMatch = 10000;

                //    Trace.WriteLine($"Actual label: {actualLabel}");

                //    List <Dictionary<string, string>> similarityList = new List<Dictionary<string, string>>();

                //    foreach (var digitClass in lib)
                //    {
                //        string currentLabel = digitClass.Key;
                //        foreach (var entry in digitClass.Value)
                //        {
                //            double arrayGeometricDistance = Utility.CalArrayUnion(entry, current);
                //            similarityList.Add(new Dictionary<string, string>() 
                //            {
                //                {"Label", currentLabel},
                //                {"Value", Convert.ToString(arrayGeometricDistance)}
                //            });  

                //            //if (arrayGeometricDistance < lowestMatch)
                //            //{
                //            //    predictedLabel = currentLabel;
                //            //    lowestMatch = arrayGeometricDistance;
                //            //}
                //        }
                //    }

                //    // get top 3 lowest arrayGeometricDistance
                //    // sorted from lowest arrayGeometricDistance to highest
                //    int topN = 3;
                //    var topN_similarity = similarityList.OrderBy(dict => Convert.ToDouble(dict["Value"])).ThenByDescending(dict => dict["Label"]).Take(topN);

                //    // get # of times each lable is predicted
                //    Dictionary<string, double> countFreq = new Dictionary<string, double>();
                //    foreach (var s in topN_similarity)
                //    {
                //        if (!countFreq.ContainsKey(s["Label"]))
                //        {
                //            countFreq.Add(s["Label"], 0);
                //        }
                //        countFreq[s["Label"]] += 1;
                //        Console.WriteLine($"Label {s["Label"]} - Similarity {s["Value"]}");
                //        Trace.WriteLine($"Label {s["Label"]} - Similarity {s["Value"]}");
                //    }

                //    // get % of prediction for each label
                //    foreach (var freq in countFreq)
                //    {
                //        Console.WriteLine($"% of label {freq.Key}: {Math.Round(100*freq.Value/topN)} %");
                //        Trace.WriteLine($"% of label {freq.Key}: {Math.Round(100 * freq.Value / topN)} %");
                //    }

                //    // get predictedLabel based on the highest %
                //    // if % of labels are equal -> take the one with lowest arrayGeometricDistance, which has been already sorted in top3_similarity
                //    predictedLabel = countFreq.OrderByDescending(x => x.Value).First().Key;

                //    // if predictedLabel match with actualLabel -> increase # corretion prediction 1 unit.
                //    if (actualLabel == predictedLabel)
                //    {
                //        match += 1;
                //    }
                //    Console.WriteLine($"{actualLabel} predicted as {predictedLabel}");
                //    Trace.WriteLine($"{actualLabel} predicted as {predictedLabel}");
                //    Console.WriteLine("=======================================");
                //    Trace.WriteLine("=======================================");

                //}

                //Console.WriteLine($"Accuracy = {(double)100*(((double)match) / ((double)testSet_32x32.Count))} %");
                //Trace.WriteLine($"Accuracy = {(double)100*(((double)match) / ((double)testSet_32x32.Count))} %");

                //Trace.WriteLine("============= Exiting Main =============");
                //Trace.Unindent();
                //Trace.Flush();
                #endregion
            }

            Console.WriteLine("test");
        }


        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void Toan_InvariantRepresentation(string experimentFolder)
        {
            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, 10);

            // generate 32x32 source MNISTDataSet
            int width = 32; int height = 32;
            DataSet sourceSet = new DataSet(sourceMNIST);
        
            DataSet sourceSet_32x32 = DataSet.ScaleSet(experimentFolder, width, height, sourceSet , "sourceSet");
            DataSet testSet_32x32 = sourceSet_32x32.GetTestData(20);

            DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder,"testSet_32x32"));

            // write extracted/filtered frame from 32x32 dataset into 4x4 for SP to learn all pattern
            //var listOfFrame = Frame.GetConvFrames(width, height, 4, 4, 8, 8);
            var listOfFrame = Frame.GetConvFramesbyPixel(32, 32, 4, 4, 4);
            string extractedFrameFolder = Path.Combine(experimentFolder, "extractedFrame");
            int index = 0;
            List<string> frameDensityList = new List<string>();
            foreach (var image in sourceSet_32x32.Images)
            {
                foreach (var frame in listOfFrame)
                {
                        if (image.IsRegionInDensityRange(frame, 30, 80))
                        {
                            Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolder, $"{index}"));
                            if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
                            {
                                string savePath = Path.Combine(extractedFrameFolder, $"{index}", $"{index}.png");
                                string extractedFrameFolderBinarized = Path.Combine(experimentFolder, "extractedFrameBinarized");
                                Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderBinarized, $"{index}"));
                                string savePathOri = Path.Combine(extractedFrameFolderBinarized, $"{index}", $"{index}_ori.png");

                                image.SaveTo(savePath, frame, true);
                                image.SaveTo(savePathOri, frame);

                                frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
                                index += 1;
                            }
                        }
                }
            }
            File.WriteAllLines(Path.Combine(extractedFrameFolder, "PixelDensity.txt"), frameDensityList.ToArray());
            DataSet extractedFrameSet = new DataSet(extractedFrameFolder);

            // Learning the filtered frame set with SP
            LearningUnit spLayer1 = new LearningUnit(32,32,2048,experimentFolder);
            spLayer1.TrainingNewbornCycle(extractedFrameSet);
            // spLayer1.TrainingNormal(extractedFrameSet, 1);

            string extractedImageSource = Path.Combine(experimentFolder, "extractedSet");
            Utility.CreateFolderIfNotExist(extractedImageSource);

            // Saving representation/semantic array with its label in files
            Dictionary<string, List<int[]>> lib = new Dictionary<string, List<int[]>>();

            foreach (var image in testSet_32x32.Images)
            {
                string extractedFrameFolderofImage = Path.Combine(extractedImageSource, $"{image.Label}_{Path.GetFileNameWithoutExtension(image.ImagePath)}");
                Utility.CreateFolderIfNotExist(extractedFrameFolderofImage);
                if (!lib.ContainsKey(image.Label))
                {
                    lib.Add(image.Label, new List<int[]>());
                }
                int[] current = new int[spLayer1.columnDim];
                foreach (var frame in listOfFrame)
                {
                    if (image.IsRegionInDensityRange(frame, 30, 80))
                    {
                        string frameImage = Path.Combine(extractedFrameFolderofImage, $"{frame.tlX}-{frame.tlY}_{frame.brX}-{frame.brY}.png");
                        image.SaveTo(frameImage, frame, true);
                        int[] a = spLayer1.Predict(frameImage);
                        current = Utility.AddArray(current, a);
                    }
                }
                lib[image.Label].Add(current);
            }

            foreach (var a in lib)
            {
                using (StreamWriter sw = new StreamWriter(Path.Combine(extractedImageSource, $"{a.Key}.txt")))
                {
                    foreach (var s in a.Value)
                    {
                        sw.WriteLine(string.Join(',', s));
                    }
                }
            }

            // Testing section, caculate accuracy
            string testFolder = Path.Combine(experimentFolder, "Test");
            Utility.CreateFolderIfNotExist(testFolder);
            int match = 0;
            //listOfFrame = Frame.GetConvFrames(100, 100, 4, 4, 25, 25);
            listOfFrame = Frame.GetConvFramesbyPixel(100, 100, 4, 4, 4);
            foreach (var testImage in scaledTestSet.Images)
            {
                string testImageFolder = Path.Combine(testFolder, $"{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}");
                Utility.CreateFolderIfNotExist(testImageFolder);
                testImage.SaveTo(Path.Combine(testImageFolder, "origin.png"));

                int[] current = new int[spLayer1.columnDim];
                foreach (var frame in listOfFrame)
                {
                    string frameImage = Path.Combine(testImageFolder, $"{frame.tlX}-{frame.tlY}_{frame.brX}-{frame.brY}.png");
                    testImage.SaveTo(frameImage, frame, true);
                    if (testImage.IsRegionInDensityRange(frame, 30, 80))
                    {
                        current = Utility.AddArray(current, spLayer1.Predict(frameImage));
                    }
                }
                string actualLabel = testImage.Label;
                string predictedLabel = "";
                double lowestMatch = 10000;
                foreach (var digitClass in lib)
                {
                    string currentLabel = digitClass.Key;
                    foreach (var entry in digitClass.Value)
                    {
                        double arrayGeometricDistance = Utility.CalArrayUnion(entry, current);
                        if (arrayGeometricDistance < lowestMatch)
                        {
                            predictedLabel = currentLabel;
                            lowestMatch = arrayGeometricDistance;
                        }
                    }
                }

                if (actualLabel == predictedLabel)
                {
                    match += 1;
                }
                Debug.WriteLine($"{actualLabel} predicted as {predictedLabel}");
            }
            Debug.WriteLine($"accuracy equals {(double)(((double)match) / ((double)scaledTestSet.Count))}");
        }

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
            HtmConfig htm = new HtmConfig(new int[] { 100 },new int[] { 1024 });
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
                    sp.Compute(encoder.Encode(i),true);
                }
            }
            hpc.TraceState();
        }

        /// <summary>
        /// SP of different sizes create SDR from images, this test checks the sparsity of the 2 patterns to see if bigger numCol ~ larger different from a pair
        /// </summary>
        /// <param name="outFolder"></param>
        private static void LocaDimensionTest(string outFolder)
        {
            List<LearningUnit> sps = new List<LearningUnit>();
            List<int> spDim = new List<int> { 28, 50};
            foreach(var dim in spDim)
            {
                sps.Add(new LearningUnit(dim,dim, 1024, outFolder));
            }


            Image four = new Image(Path.Combine("LocalDimensionTest","4.png"),"4");
            Image nine = new Image(Path.Combine("LocalDimensionTest", "9.png"), "9");

            DataSet training = new DataSet(new List<Image> { four, nine });

            foreach (var sp in sps)
            {
                sp.TrainingNewbornCycle(training);
                sp.TrainingNormal(training, 50);

                string similarityPath = Path.Combine("LocalDimensionTest_Res", $"SimilaritiesCalc__{sp.Id}");
                Utility.CreateFolderIfNotExist(similarityPath);

                var a = sp.classifier.TraceCrossSimilarity("4", "9");
                var j = sp.classifier.TraceCrossSimilarity("4", "9");
            }
        }

        private static void ExperimentEvaluatateImageClassification(string outFolder)
        {
            // reading Config from json
            var config = Utility.ReadConfig("experimentParams.json");
            Utility.CreateFolderIfNotExist(config.ExperimentFolder);
            string pathToTrainDataFolder = config.PathToTrainDataFolder;
            string pathToTestDataFolder = config.PathToTestDataFolder;

            Mnist.DataGen("MnistDataset", Path.Combine(config.ExperimentFolder, pathToTrainDataFolder), 10);

            Utility.CreateFolderIfNotExist(Path.Combine(config.ExperimentFolder, pathToTrainDataFolder));
            DataSet trainingData = new DataSet(Path.Combine(config.ExperimentFolder, pathToTrainDataFolder));

            Utility.CreateFolderIfNotExist(Path.Combine(config.ExperimentFolder, pathToTestDataFolder));
            DataSet testingData = trainingData.GetTestData(10);
            testingData.VisualizeSet(Path.Combine(config.ExperimentFolder, pathToTestDataFolder));

            LearningUnit sp = new(40,40, 1024,outFolder);

            sp.TrainingNewbornCycle(trainingData);

            sp.TrainingNormal(trainingData, config.runParams.Epoch);

            var allResult = new List<Dictionary<string, string>>();

            foreach (var testingImage in testingData.Images)
            {
                Utility.CreateFolderIfNotExist("TestResult");
                var res = sp.PredictScaledImage(testingImage, Path.Combine(config.ExperimentFolder, "TestResult"));
                res.Add("fileName", $"{testingImage.Label}_{Path.GetFileName(testingImage.ImagePath)}");
                res.Add("CorrectLabel", testingImage.Label);
                allResult.Add(res);
            }
            Utility.WriteListToCsv(Path.Combine(config.ExperimentFolder, "TestResult", "testOutput"), allResult);
            Utility.WriteListToOutputFile(Path.Combine(config.ExperimentFolder, "TestResult", "testOutput"), allResult);

            var a = sp.classifier.RenderCorrelationMatrixToCSVFormat();
            File.WriteAllLines(Path.Combine(config.ExperimentFolder, "correlationMat.csv"), a);

            string similarityPath = Path.Combine(config.ExperimentFolder, "SimilaritiesCalc");
            Utility.CreateFolderIfNotExist(similarityPath);

        }

        private static void ExperimentNormalImageClassification()
        {
            // reading Config from json
            var config = Utility.ReadConfig("experimentParams.json");
            string pathToTrainDataFolder = config.PathToTrainDataFolder;

            //Mnist.DataGenAll("MnistDataset", "TrainingFolder");
            Mnist.DataGen("MnistDataset", "TrainingFolder", 10);

            List<DataSet> testingData = new List<DataSet>();
            List<DataSet> trainingData = new List<DataSet>();

            DataSet originalTrainingDataSet = new DataSet(pathToTrainDataFolder);

            int k = 5;

            (trainingData, testingData) = originalTrainingDataSet.KFoldDataSetSplitEvenly(k);

            ConcurrentDictionary<string, double> foldValidationResult = new ConcurrentDictionary<string, double>();

            Parallel.For(0, k, (i) =>
            //for (int i = 0; i < k; i += 1)
            {
                // Visualizing data in k-fold scenarios
                string setPath = $"DatasetFold{i}";
                string trainSetPath = Path.Combine(setPath, "TrainingData");
                Utility.CreateFolderIfNotExist(trainSetPath);
                trainingData[i].VisualizeSet(trainSetPath);

                string testSetPath = Path.Combine(setPath, "TestingData");
                Utility.CreateFolderIfNotExist(trainSetPath);
                testingData[i].VisualizeSet(testSetPath);

                // passing the training data to the training experiment
                InvariantExperimentImageClassification experiment = new(trainingData[i], config.runParams);

                // train the network
                experiment.Train(false);

                // Prediction phase
                Utility.CreateFolderIfNotExist($"Predict_{i}");

                List<string> currentResList = new List<string>();

                Dictionary<string, List<Dictionary<string, string>>> allResult = new Dictionary<string, List<Dictionary<string, string>>>();

                foreach (var testImage in testingData[i].Images)
                {
                    var result = experiment.Predict(testImage, i.ToString());

                    string testImageID = $"{testImage.Label}_{Path.GetFileNameWithoutExtension(testImage.ImagePath)}";
                    UpdateResult(ref allResult, testImageID, result);
                }
                double foldValidationAccuracy = CalculateAccuracy(allResult);

                foreach (var sp in allResult)
                {
                    string path = Path.Combine($"Predict_{i}", sp.Key);
                    Utility.WriteListToCsv(path, allResult[sp.Key]);
                }

                foldValidationResult.TryAdd($"Fold_{i}_accuracy", foldValidationAccuracy);
            });
            Utility.WriteResultOfOneSP(new Dictionary<string, double>(foldValidationResult), $"KFold_{k}_Validation_Result");
        }

        /// <summary>
        /// Calculate by averaging similarity prediction of the correct label
        /// </summary>
        /// <param name="allResult"></param>
        /// <returns></returns>
        private static double CalculateAccuracy(Dictionary<string, List<Dictionary<string, string>>> allResult)
        {
            List<double> spAccuracy = new List<double>();

            foreach (var spResult in allResult.Values)
            {
                List<double> similarityList = new List<double>();
                foreach (var imagePredictResult in spResult)
                {
                    if (imagePredictResult.ContainsKey(imagePredictResult["CorrectLabel"]))
                    {
                        similarityList.Add(Double.Parse(imagePredictResult[imagePredictResult["CorrectLabel"]]));
                    }
                    else
                    {
                        similarityList.Add(0.0);
                    }
                }
                spAccuracy.Add(similarityList.Average());
            }
            return spAccuracy.Average();
        }

        private static void UpdateResult(ref Dictionary<string, List<Dictionary<string, string>>> allResult, string testImageID, Dictionary<string, Dictionary<string, string>> result)
        {
            foreach (var spKey in result.Keys)
            {
                if (!allResult.ContainsKey(spKey))
                {
                    allResult.Add(spKey, new List<Dictionary<string, string>>());
                }
            }

            foreach (var spKey in allResult.Keys)
            {
                Dictionary<string, string> resultEntryOfOneSP = new Dictionary<string, string>();
                resultEntryOfOneSP.Add("fileName", testImageID);
                foreach (var labelPred in result[spKey])
                {
                    resultEntryOfOneSP.Add(labelPred.Key, labelPred.Value);
                }
                allResult[spKey].Add(resultEntryOfOneSP);
            }
        }

        /*
private static void ExperimentPredictingWithFrameGrid()
{
   // populate the training and testing dataset with Mnist DataGen
   Mnist.DataGen("MnistDataset", "TrainingFolder", 5);
   Mnist.TestDataGen("MnistDataset", "TestingFolder", 5);

   // reading Config from json
   var config = Utility.ReadConfig("experimentParams.json");
   string pathToTrainDataFolder = config.PathToTrainDataFolder;
   string pathToTestDataFolder = config.PathToTestDataFolder;

   // generate the training data
   DataSet trainingSet = new DataSet(pathToTrainDataFolder);

   // generate the testing data
   DataSet testingSet = new DataSet(pathToTestDataFolder);

   // passing the training data to the training experiment
   InvariantExperimentImageClassification experiment = new(trainingSet, config.runParams);

   // train the network
   experiment.Train(true);


   // using predict to classify image from dataset
   Utility.CreateFolderIfNotExist("Predict");
   List<string> currentResList = new List<string>();
   /*
   CancellationToken cancelToken = new CancellationToken();
   while (true)
   {
       if (cancelToken.IsCancellationRequested)
       {
           return;
       }
       // This can be later changed to the validation test
       var result = experiment.Predict(testingSet.PickRandom());
       Debug.WriteLine($"predicted as {result.Item1}, correct label: {result.Item2}");


       double accuracy = Utility.AccuracyCal(currentResList);
       currentResList.Add($"{result.Item1}_{result.Item2}");
       Utility.WriteOutputToFile(Path.Combine("Predict", "PredictionOutput"),result);
   }



   foreach (var testImage in testingSet.Images)
   {
       var result = experiment.Predict(testImage);

       Debug.WriteLine($"predicted as {result.Item1}, correct label: {result.Item2}");

       double accuracy = Utility.AccuracyCal(currentResList);

       currentResList.Add($"{result.Item1}_{result.Item2}");

       Utility.WriteOutputToFile(Path.Combine("Predict", $"{Utility.GetHash()}_____PredictionOutput of testImage label {testImage.label}"), result);
   }

}
*/
    }
}