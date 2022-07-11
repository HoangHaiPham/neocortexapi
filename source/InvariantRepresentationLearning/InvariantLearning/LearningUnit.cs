﻿using NeoCortexApi;
using NeoCortexApi.Entities;
using NeoCortexApi.Network;
using System.Diagnostics;
using HtmImageEncoder;
using NeoCortexApi.Classifiers;

using Invariant.Entities;

namespace InvariantLearning
{
    public class LearningUnit
    {
        public string OutputPredictFolder = "";
        private CortexLayer<object, object> cortexLayer;
        private bool isInStableState;

        private int inputDim;
        private int columnDim;
        private HtmClassifier<string, int[]> classifier;


        public LearningUnit(int inputDim, int columnDim)
        {
            this.inputDim = inputDim;
            this.columnDim = columnDim;

            // CortexLayer
            cortexLayer = new CortexLayer<object, object>("Invariant");

            // HTM CLASSIFIER
            classifier = new HtmClassifier<string, int[]>();
        }

        /// <summary>
        /// Training with newborn cycle before Real Learning of DataSet
        /// </summary>
        /// <param name="trainingDataSet">the Training Dataset</param>
        public void TrainingNewbornCycle(DataSet trainingDataSet)
        {
            // HTM CONFIG
            HtmConfig config = new HtmConfig(new int[] { inputDim * inputDim }, new int[] { columnDim });

            // CONNECTIONS
            Connections conn = new Connections(config);

            // HPC
            HomeostaticPlasticityController hpc = new HomeostaticPlasticityController(conn, trainingDataSet.Count * 50, (isStable, numPatterns, actColAvg, seenInputs) =>
            {
                if (isStable)
                    // Event should be fired when entering the stable state.
                    Debug.WriteLine($"STABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                else
                    // Ideal SP should never enter unstable state after stable state.
                    Debug.WriteLine($"INSTABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");

                // We are not learning in instable state.
                this.isInStableState = isStable;

                // Clear active and predictive cells.
                //tm.Reset(mem);
            }, numOfCyclesToWaitOnChange: 50);

            // SPATIAL POOLER
            SpatialPooler sp = new SpatialPooler(hpc);
            sp.Init(conn);

            // IMAGE ENCODER
            ImageEncoder imgEncoder = new(new Daenet.ImageBinarizerLib.Entities.BinarizerParams()
            {
                Inverse = false,
                ImageHeight = inputDim,
                ImageWidth = inputDim,
                GreyScale = true,
            });

            // CORTEX LAYER
            cortexLayer.AddModule("encoder", imgEncoder);
            cortexLayer.AddModule("sp", sp);

            // STABLE STATE
            isInStableState = false;

            // New Born Cycle Loop
            int cycle = 0;
            while (!this.isInStableState)
            {
                Debug.Write($"Cycle {cycle}: ");
                foreach (var sample in trainingDataSet.images)
                {
                    Debug.Write(".");
                    cortexLayer.Compute(sample.imagePath, true);
                }
                Debug.Write("\n");
                cycle++;
            }
        }

        public void Learn(Picture sample)
        {
            Debug.WriteLine($"Label: {sample.label}___{Path.GetFileNameWithoutExtension(sample.imagePath)}");

            // Claculates the SDR of Active Columns.
            cortexLayer.Compute(sample.imagePath, true);

            var activeColumns = cortexLayer.GetResult("sp") as int[];
            classifier.Learn(sample.label, activeColumns);
        }

        public Dictionary<string, double> Predict(Picture image)
        {
            // Create the folder for the frame extracted by InvImage
            string spFolder = Path.Combine("Predict", OutputPredictFolder, $"SP of {inputDim}x{inputDim}");
            Utility.CreateFolderIfNotExist(spFolder);

            // dictionary for saving result
            Dictionary<string, double> result = new Dictionary<string, double>();
            Dictionary<string, string> allResultForEachFrame = new Dictionary<string, string>();

            var frameMatrix = Frame.GetConvFramesbyPixel(image.imageWidth, image.imageHeight, inputDim, inputDim, 5);


            foreach (var frame in frameMatrix)
            {
                if (image.IsRegionEmpty(frame))
                {
                    continue;
                }
                else
                {
                    // Save frame to folder
                    string outFile = Path.Combine(spFolder, $"frame__{frame.tlX}_{frame.tlY}.png");
                    Picture.SaveAsImage(image.GetPixels(frame), outFile);

                    // Compute the SDR
                    var sdr = cortexLayer.Compute(outFile, false) as int[];

                    // Get Predicted Labels
                    var predictedLabel = classifier.GetPredictedInputValues(sdr, 1);

                    // Check if there are Predicted Label ?
                    if (predictedLabel.Count != 0)
                    {
                        foreach (var a in predictedLabel)
                        {
                            Debug.WriteLine($"Predicting image: {image.imagePath}");
                            Debug.WriteLine($"label predicted as : {a.PredictedInput}");
                            Debug.WriteLine($"similarity : {a.Similarity}");
                            Debug.WriteLine($"Number of Same Bits: {a.NumOfSameBits}");
                        }
                    }

                    allResultForEachFrame.Add(outFile,GetStringFromResult(predictedLabel));
                    // Aggregate Label to Result
                    AddResult(ref result, predictedLabel, frameMatrix.Count);
                }
            }
            Utility.WriteResultOfOneSPDetailed(allResultForEachFrame, Path.Combine(spFolder,$"SP of {inputDim}x{inputDim} detailed.csv"));
            return result;
        }

        private string GetStringFromResult(List<ClassifierResult<string>> predictedLabel)
        {
            string resultString = "";
            foreach (ClassifierResult<string> result in predictedLabel)
            {
                resultString += $",{result.PredictedInput}__{result.NumOfSameBits}_{result.Similarity}";
            }
            return resultString;
        }

        private void AddResult(ref Dictionary<string, double> result, List<ClassifierResult<string>> predictedLabel, int frameCount)
        {
            Dictionary<string, double> res = new Dictionary<string, double>();
            foreach (var label in predictedLabel)
            {
                if (result.ContainsKey(label.PredictedInput))
                {
                    result[label.PredictedInput] += label.NumOfSameBits;
                }
                else
                {
                    result.Add(label.PredictedInput, label.NumOfSameBits);
                }
            }
        }
    }
}