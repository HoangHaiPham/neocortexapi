﻿// Copyright (c) Damir Dobric. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
using Invariant.Entities;
using NeoCortexApi;
using NeoCortexApi.Classifiers;
using NeoCortexApi.Entities;
using NeoCortexApi.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace InvariantLearning_Utilities
{
    /// <summary>
    /// Defines the predicting input.
    /// </summary>
    public class ClassifierResult<TIN>
    {
        /// <summary>
        /// The predicted input value.
        /// </summary>
        public TIN PredictedInput { get; set; }

        /// <summary>
        /// Number of identical non-zero bits in the SDR.
        /// </summary>
        public int NumOfSameBits { get; set; }

        /// <summary>
        /// The similarity between the SDR of  predicted cell set with the SDR of the input.
        /// </summary>
        public double Similarity { get; set; }
    }


    /// <summary>
    /// Classifier implementation which memorize all seen values.
    /// </summary>
    /// <typeparam name="TIN"></typeparam>
    /// <typeparam name="TOUT"></typeparam>
    public class HtmClassifier<TIN, TOUT> : IClassifier<TIN, TOUT>
    {
        private int maxRecordedElements = 10;

        private List<TIN> inputSequence = new List<TIN>();

        private Dictionary<int[], int> inputSequenceMap = new Dictionary<int[], int>();

        /// <summary>
        /// Recording of all SDRs. See maxRecordedElements.
        /// </summary>
        private Dictionary<TIN, List<int[]>> m_AllInputs = new Dictionary<TIN, List<int[]>>();
        private List<Sample> m_AllSamples = new List<Sample>();
        private List<Sample> m_WinnerSamples = new List<Sample>();
        private List<Sample> m_AllMnistSamples = new List<Sample>();

        /// <summary>
        /// Recording of all SDRs. See maxRecordedElements.
        /// </summary>

        private Dictionary<TIN, List<int[]>> m_SelectedInputs = new Dictionary<TIN, List<int[]>>();
        private List<Sample> m_SelectedSamples = new List<Sample>();

        /// <summary>
        /// Sum similarity score of all samples
        /// </summary>
        private Dictionary<string, double> m_SumSimilarityScoreSamples = new Dictionary<string,double>();


        /// <summary>
        /// Mapping between the input key and the SDR assootiated to the input.
        /// </summary>
        //private Dictionary<TIN, int[]> m_ActiveMap2 = new Dictionary<TIN, int[]>();

        /// <summary>
        /// Clears th elearned state.
        /// </summary>
        public void ClearState()
        {
            m_AllInputs.Clear();
        }

        /// <summary>
        /// Checks if the same SDR is already stored under the given key.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sdr"></param>
        /// <returns></returns>
        private bool ContainsSdr(TIN input, int[] sdr)
        {
            foreach (var item in m_AllInputs[input])
            {
                if (item.SequenceEqual(sdr))
                    return true;
                else
                    return false;
            }

            return false;
        }


        private int GetBestMatch(TIN input, int[] cellIndicies, out double similarity, out int[] bestSdr)
        {
            int maxSameBits = 0;
            bestSdr = new int[1];

            foreach (var sdr in m_AllInputs[input])
            {
                var numOfSameBitsPct = sdr.Intersect(cellIndicies).Count();
                if (numOfSameBitsPct >= maxSameBits)
                {
                    maxSameBits = numOfSameBitsPct;
                    bestSdr = sdr;
                }
            }

            similarity = Math.Round(MathHelpers.CalcArraySimilarity(bestSdr, cellIndicies), 2);

            return maxSameBits;
        }


        /// <summary>
        /// Assotiate specified input to the given set of predictive cells.
        /// </summary>
        /// <param name="input">Any kind of input.</param>
        /// <param name="output">The SDR of the input as calculated by SP.</param>
        public void Learn(TIN input, Cell[] output)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var cellIndicies = GetCellIndicies(output);

            Learn(input, cellIndicies);
        }

        /// <summary>
        /// Assotiate specified input to the given set of predictive cells. This can also be used to classify Spatial Pooler Columns output as int array
        /// </summary>
        /// <param name="input">Any kind of input.</param>
        /// <param name="output">The SDR of the input as calculated by SP as int array</param>
        public void Learn(TIN input, int[] cellIndicies)
        {
            if (m_AllInputs.ContainsKey(input) == false)
                m_AllInputs.Add(input, new List<int[]>());

            // Store the SDR only if it was not stored under the same key already.
            if (!ContainsSdr(input, cellIndicies))
                m_AllInputs[input].Add(cellIndicies);
            else
            {
                // for debugging
            }

            //
            // Make sure that only few last SDRs are recorded.
            if (m_AllInputs[input].Count > maxRecordedElements)
            {
                Debug.WriteLine($"The input {input} has more ");
                m_AllInputs[input].RemoveAt(0);
            }

            var previousOne = m_AllInputs[input][Math.Max(0, m_AllInputs[input].Count - 2)];

            if (!previousOne.SequenceEqual(cellIndicies))
            {
                // double numOfSameBitsPct = (double)(((double)(this.activeMap2[input].Intersect(cellIndicies).Count()) / Math.Max((double)cellIndicies.Length, this.activeMap2[input].Length)));
                // double numOfSameBitsPct = (double)(((double)(this.activeMap2[input].Intersect(cellIndicies).Count()) / (double)this.activeMap2[input].Length));
                var numOfSameBitsPct = previousOne.Intersect(cellIndicies).Count();
                Debug.WriteLine($"Prev/Now/Same={previousOne.Length}/{cellIndicies.Length}/{numOfSameBitsPct}");
            }
        }


        /// <summary>
        /// Gets multiple predicted values.
        /// </summary>
        /// <param name="predictiveCells">The current set of predictive cells.</param>
        /// <param name="howMany">The number of predections to return.</param>
        /// <returns>List of predicted values with their similarities.</returns>
        public List<ClassifierResult<TIN>> GetPredictedInputValues(Cell[] predictiveCells, short howMany = 1)
        {
            var cellIndicies = GetCellIndicies(predictiveCells);

            return GetPredictedInputValues(cellIndicies, howMany);
        }

        /// <summary>
        /// Gets multiple predicted values. This can also be used to classify Spatial Pooler Columns output as int array
        /// </summary>
        /// <param name="predictiveCells">The current set of predictive cells in int array.</param>
        /// <param name="howMany">The number of predections to return.</param>
        /// <returns>List of predicted values with their similarities.</returns>
        public List<ClassifierResult<TIN>> GetPredictedInputValues(int[] cellIndicies, short howMany = 1)
        {
            List<ClassifierResult<TIN>> res = new List<ClassifierResult<TIN>>();
            double maxSameBits = 0;
            TIN predictedValue = default;
            Dictionary<TIN, ClassifierResult<TIN>> dict = new Dictionary<TIN, ClassifierResult<TIN>>();

            var predictedList = new List<KeyValuePair<double, string>>();
            if (cellIndicies.Length != 0)
            {
                int indxOfMatchingInp = 0;
                Debug.WriteLine($"Item length: {cellIndicies.Length}\t Items: {this.m_AllInputs.Keys.Count}");
                int n = 0;

                List<int> sortedMatches = new List<int>();

                Debug.WriteLine($"Predictive cells: {cellIndicies.Length} \t {Helpers.StringifyVector(cellIndicies)}");

                foreach (var pair in this.m_AllInputs)
                {
                    if (ContainsSdr(pair.Key, cellIndicies))
                    {
                        Debug.WriteLine($">indx:{n.ToString("D3")}\tinp/len: {pair.Key}/{cellIndicies.Length}, Same Bits = {cellIndicies.Length.ToString("D3")}\t, Similarity 100.00 %\t {Helpers.StringifyVector(cellIndicies)}");

                        res.Add(new ClassifierResult<TIN> { PredictedInput = pair.Key, Similarity = (float)100.0, NumOfSameBits = cellIndicies.Length });
                    }
                    else
                    {
                        // Tried following:
                        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(arr).Count()) / Math.Max(arr.Length, pair.Value.Count())));
                        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(celIndicies).Count()) / (double)pair.Value.Length));// ;
                        double similarity;
                        int[] bestMatch;
                        var numOfSameBitsPct = GetBestMatch(pair.Key, cellIndicies, out similarity, out bestMatch);// pair.Value.Intersect(cellIndicies).Count();
                        //double simPercentage = Math.Round(MathHelpers.CalcArraySimilarity(pair.Value, cellIndicies), 2);
                        dict.Add(pair.Key, new ClassifierResult<TIN> { PredictedInput = pair.Key, NumOfSameBits = numOfSameBitsPct, Similarity = similarity });
                        predictedList.Add(new KeyValuePair<double, string>(similarity, pair.Key.ToString()));

                        if (numOfSameBitsPct > maxSameBits)
                        {
                            Debug.WriteLine($">indx:{n.ToString("D3")}\tinp/len: {pair.Key}/{bestMatch.Length}, Same Bits = {numOfSameBitsPct.ToString("D3")}\t, Similarity {similarity.ToString("000.00")} % \t {Helpers.StringifyVector(bestMatch)}");
                            maxSameBits = numOfSameBitsPct;
                            predictedValue = pair.Key;
                            indxOfMatchingInp = n;
                        }
                        else
                            Debug.WriteLine($"<indx:{n.ToString("D3")}\tinp/len: {pair.Key}/{bestMatch.Length}, Same Bits = {numOfSameBitsPct.ToString("D3")}\t, Similarity {similarity.ToString("000.00")} %\t {Helpers.StringifyVector(bestMatch)}");
                    }
                    n++;
                }
            }

            int cnt = 0;
            foreach (var keyPair in dict.Values.OrderByDescending(key => key.Similarity))
            {
                res.Add(keyPair);
                if (++cnt >= howMany)
                    break;
            }

            return res;
        }

        /// <summary>
        /// Remember the training samples.
        /// </summary>
        public void LearnObj(List<Sample> trainingSamples)
        {
            m_AllSamples.AddRange(trainingSamples);
        }

        /// <summary>
        /// Remember the mnist samples.
        /// </summary>
        public void LearnMnistObj(List<Sample> mnistSamples)
        {
            m_AllMnistSamples.AddRange(mnistSamples);
        }

        /// <summary>
        /// Predict the sample
        /// </summary>
        public string PredictObj(Sample testingSample, int howManyFeatures = 0)
        {
            string predictedLabel;
            Dictionary<Sample, double> matchingFeatureList = new Dictionary<Sample, double>();
            matchingFeatureList = GetMatchingFeatures(m_AllSamples, testingSample, howManyFeatures);
            var sumSimilarity = matchingFeatureList.Select(x => x).GroupBy(x => x.Key.Object).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value), 2));
            foreach (var label in sumSimilarity)
            {
                if (!m_SumSimilarityScoreSamples.ContainsKey(label.Key))
                {
                    m_SumSimilarityScoreSamples.Add(label.Key, label.Value);
                }
                else
                {
                    m_SumSimilarityScoreSamples[label.Key] += label.Value;
                }
            }
            //foreach (var digit in m_SumSimilarityScoreSamples)
            //{
            //    Trace.WriteLine($"Sum similarity of label {digit.Key}: {digit.Value}");
            //}
            predictedLabel = m_SumSimilarityScoreSamples.MaxBy(entry => entry.Value).Key;
            //Trace.WriteLine($"Label {testingSample.Object} predicted as (maxSum) {predictedLabel}");
            //Trace.WriteLine("=======================================");
            m_SumSimilarityScoreSamples.Clear();
            return predictedLabel;
        }

        /// <summary>
        /// Predict normal image not in 100x100 scale
        /// </summary>
        public string PredictObj_ForNormalImage(List<Sample> testingSamples, int howManyFeatures)
        {
            foreach (var testingSample in testingSamples)
            {
                Dictionary<Sample, double> matchingFeatureList = new Dictionary<Sample, double>();
                matchingFeatureList = GetMatchingFeatures(m_AllSamples, testingSample, howManyFeatures);

                var sumSimilarity = matchingFeatureList.Select(x => x).GroupBy(x => x.Key.Object).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value), 2));

                //m_SumSimilarityScoreSamples.Add(sumSimilarity);
                foreach (var label in sumSimilarity)
                {
                    if (!m_SumSimilarityScoreSamples.ContainsKey(label.Key))
                    {
                        m_SumSimilarityScoreSamples.Add(label.Key, label.Value);
                    }
                    else
                    {
                        m_SumSimilarityScoreSamples[label.Key] += label.Value;
                    }
                }
                
            }

            foreach (var digit in m_SumSimilarityScoreSamples)
            {
                Trace.WriteLine($"Sum similarity of label {digit.Key}: {digit.Value}");
            }

            var sumPredictedLabel = m_SumSimilarityScoreSamples.MaxBy(entry => entry.Value);

            Trace.WriteLine($"Label {testingSamples[0].Object} predicted as (maxSum) {sumPredictedLabel.Key}");
            Trace.WriteLine("=======================================");

            m_SumSimilarityScoreSamples.Clear();

            return sumPredictedLabel.Key;
        }

        public List<string> ValidateObj(int[] objIndicies, int howManyObjs)
        {
            string winner = String.Empty;
            List<string> winnerList = new List<string>();
            Dictionary<string, double> distanceDict = new Dictionary<string, double>();
            int maxSameBits = 0;
            double maxHamDist = 0;
            double maxSimilarity = 10.0;
            foreach (var sample in m_AllMnistSamples)
            {
                var similarity = MathHelpers.CalcArraySimilarity(objIndicies, sample.PixelIndicies);
                if (similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    winner = sample.Object;
                    winnerList.Add(winner);
                }
            }
            if (winnerList.Count > howManyObjs)
            {
                winnerList.RemoveRange(0, winnerList.Count - howManyObjs);
            }
            return winnerList;
        }

        /// <summary>
        /// Find the matching samples and add them to m_SlectedSamples
        /// </summary>
        public void AddSelectedSamples(Sample testingSample, IEnumerable<int[]> matchingFeatureList)
        {
            foreach (var trainingSample in m_AllSamples)
            {
                foreach (var feature in matchingFeatureList)
                {
                    if (trainingSample.PixelIndicies.SequenceEqual(feature))
                    {
                        Sample sample = new Sample() { FramePath = trainingSample.FramePath };
                        sample.Object = trainingSample.Object;
                        sample.PixelIndicies = trainingSample.PixelIndicies;
                        sample.Position = new Frame(
                            testingSample.Position.tlX + trainingSample.Position.tlX,
                            testingSample.Position.tlY + trainingSample.Position.tlY,
                            testingSample.Position.brX + trainingSample.Position.brX,
                            testingSample.Position.brY + trainingSample.Position.brY
                        );
                        bool goodPosition = (sample.Position.tlX >= 0 && sample.Position.tlX < 100 && sample.Position.tlY >= 0 && sample.Position.tlY < 100 && sample.Position.brX >= 0 && sample.Position.brX < 100 && sample.Position.brY >= 0 && sample.Position.brY < 100);
                        if (goodPosition)
                        {
                            m_SelectedSamples.Add(sample);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the best matching features with the most same bits.
        /// </summary>
        public Dictionary<Sample, double> GetMatchingFeatures(List<Sample> trainingSamplesIndicies, Sample testingSamplesIndicies, int maxFeatures)
        {
            Dictionary<Sample, double> results = new Dictionary<Sample, double>();
            Dictionary<Sample, double> similarityScore = new Dictionary <Sample, double>();
            foreach (var trainingIndicies in trainingSamplesIndicies)
            {
                double similarity = MathHelpers.CalcArraySimilarity(testingSamplesIndicies.PixelIndicies, trainingIndicies.PixelIndicies);
                if (!similarityScore.ContainsKey(trainingIndicies))
                {
                    similarityScore.Add(trainingIndicies, similarity);
                }
            }
            // get highest similarity score
            var maxSimilarityScore = similarityScore.MaxBy(entry => entry.Value);
            // filter SDRs which have similarity score >= maxSimilarityScore
            double thresholdSimilarityScore = maxSimilarityScore.Value / 2;
            //double thresholdSimilarityScore = 0;
            var topN_similarity = similarityScore.Where(entry => entry.Value > thresholdSimilarityScore).ToDictionary(pair => pair.Key, pair => pair.Value);
            if (maxFeatures > 0)
            {
                topN_similarity = similarityScore.OrderByDescending(entry => entry.Value).Take(maxFeatures).ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            //Trace.WriteLine($"Frame: {Path.GetFileName(testingSamplesIndicies.FramePath).Split('.')[0]}");
            //foreach (var simi in topN_similarity)
            //{
            //    Trace.WriteLine($"Predicted label {simi.Key.Object} - Similarity: {Math.Round(simi.Value, 2)}");
            //}
            //// count the number of times that label is predicted
            //var countSimilarityEachLabel = topN_similarity.Select(x => x).GroupBy(x => x.Key.Object).ToDictionary(group => group.Key, group => group.ToList().Count);
            //// sum all similarity score for each table
            //var sumSimilarityEachLabel = topN_similarity.Select(x => x).GroupBy(x => x.Key.Object).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value), 2));
            //// avg = sumSimilarity/Count
            //var avgSimilarityEachLabel = topN_similarity.Select(x => x).GroupBy(x => x.Key.Object).ToDictionary(group => group.Key, group => Math.Round(group.Sum(x => x.Value) / group.ToList().Count, 2));
            //foreach (var digit in countSimilarityEachLabel)
            //{
            //    Trace.WriteLine($"count similarity >= {thresholdSimilarityScore}% of label {digit.Key}: {digit.Value} - Sum similarity of label {digit.Key}: {sumSimilarityEachLabel[digit.Key]} - Avg similarity of label {digit.Key}: {avgSimilarityEachLabel[digit.Key]}");
            //}
            //// get label predict
            //string countPredictedLabel, sumPredictedLabel, avgPredictedLabel;
            //if (maxSimilarityScore.Value >= 90)
            //{
            //    countPredictedLabel = maxSimilarityScore.Key.Object;
            //    sumPredictedLabel = maxSimilarityScore.Key.Object;
            //    avgPredictedLabel = maxSimilarityScore.Key.Object;
            //}
            //else
            //{
            //    countPredictedLabel = countSimilarityEachLabel.OrderByDescending(x => x.Value).First().Key;
            //    sumPredictedLabel = sumSimilarityEachLabel.OrderByDescending(x => x.Value).First().Key;
            //    avgPredictedLabel = avgSimilarityEachLabel.OrderByDescending(x => x.Value).First().Key;
            //}
            //Trace.WriteLine($"Label {testingSamplesIndicies.Object}: predicted as (maxCount) {countPredictedLabel} - predicted as (maxSum) {sumPredictedLabel} - predicted as (maxAvg) {avgPredictedLabel}");
            //Trace.WriteLine("=======================================");

            results = topN_similarity;
            return results;
        }


        /// <summary>
        /// Get the best matching feature with the most same bits.
        /// </summary>
        private List<string> GetBestMatchingFeature(Dictionary<string, List<int[]>> input, int[] cellIndicies, int howMany)
        {
            int maxSameBits = 0;
            int cnt = 0;
            //string bestFeature = "";
            List<string> result = new List<string>();
            Dictionary<string, int> dict = new Dictionary<string, int>();
            foreach (var feature in input.Keys)
            {
                var inputStringArray = feature.Split("-");
                int[] inputArray = Array.ConvertAll(inputStringArray, s => int.Parse(s));
                var numOfSameBitsPct = inputArray.Intersect(cellIndicies).Count();

                if (numOfSameBitsPct >= maxSameBits)
                {
                    maxSameBits = numOfSameBitsPct;
                    //bestFeature = feature;
                    dict.Add(feature, numOfSameBitsPct);
                }
            }

            foreach (var keyPair in dict.OrderByDescending(i => i.Value))
            {
                result.Add(keyPair.Key);
                if (++cnt >= howMany)
                    break;
            }

            return result;
            //return bestFeature;
        }

        private void GetInputsFromLabel(TIN input)
        {
            if (input != null)
            {
                foreach (var pair in this.m_AllInputs)
                {
                    if (pair.Key.ToString().Contains(input.ToString()))
                    {
                        if (m_SelectedInputs.ContainsKey(pair.Key))
                        {
                            m_SelectedInputs.Remove(pair.Key);
                        }
                        m_SelectedInputs.Add(pair.Key, m_AllInputs[pair.Key]);
                    }
                }
            }
        }



        /// <summary>
        /// Gets predicted value for next cycle
        /// </summary>
        /// <param name="predictiveCells">The list of predictive cells.</param>
        /// <returns></returns>
        [Obsolete("This method will be removed in the future. Use GetPredictedInputValues instead.")]
        public TIN GetPredictedInputValue(Cell[] predictiveCells)
        {
            throw new NotImplementedException("This method will be removed in the future. Use GetPredictedInputValues instead.");
            // bool x = false;
            //double maxSameBits = 0;
            //TIN predictedValue = default;

            //if (predictiveCells.Length != 0)
            //{
            //    int indxOfMatchingInp = 0;
            //    Debug.WriteLine($"Item length: {predictiveCells.Length}\t Items: {m_ActiveMap2.Keys.Count}");
            //    int n = 0;

            //    List<int> sortedMatches = new List<int>();

            //    var celIndicies = GetCellIndicies(predictiveCells);

            //    Debug.WriteLine($"Predictive cells: {celIndicies.Length} \t {Helpers.StringifyVector(celIndicies)}");

            //    foreach (var pair in m_ActiveMap2)
            //    {
            //        if (pair.Value.SequenceEqual(celIndicies))
            //        {
            //            Debug.WriteLine($">indx:{n}\tinp/len: {pair.Key}/{pair.Value.Length}\tsimilarity 100pct\t {Helpers.StringifyVector(pair.Value)}");
            //            return pair.Key;
            //        }

            //        // Tried following:
            //        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(arr).Count()) / Math.Max(arr.Length, pair.Value.Count())));
            //        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(celIndicies).Count()) / (double)pair.Value.Length));// ;
            //        var numOfSameBitsPct = pair.Value.Intersect(celIndicies).Count();
            //        if (numOfSameBitsPct > maxSameBits)
            //        {
            //            Debug.WriteLine($">indx:{n}\tinp/len: {pair.Key}/{pair.Value.Length} = similarity {numOfSameBitsPct}\t {Helpers.StringifyVector(pair.Value)}");
            //            maxSameBits = numOfSameBitsPct;
            //            predictedValue = pair.Key;
            //            indxOfMatchingInp = n;
            //        }
            //        else
            //            Debug.WriteLine($"<indx:{n}\tinp/len: {pair.Key}/{pair.Value.Length} = similarity {numOfSameBitsPct}\t {Helpers.StringifyVector(pair.Value)}");

            //        n++;
            //    }
            //}

            //return predictedValue;
        }
   

        /// <summary>
        /// Traces out all cell indicies grouped by input value.
        /// </summary>
        public string TraceState(string fileName = null)
        {
            StringWriter strSw = new StringWriter();

            StreamWriter sw = null;

            if (fileName != null)
                sw = new StreamWriter(fileName);

            List<TIN> processedValues = new List<TIN>();

            //
            // Trace out the last stored state.
            foreach (var item in this.m_AllInputs)
            {
                strSw.WriteLine("");
                strSw.WriteLine($"{item.Key}");
                strSw.WriteLine($"{Helpers.StringifyVector(item.Value.Last())}");
            }

            strSw.WriteLine("........... Cell State .............");

            foreach (var item in m_AllInputs)
            {
                strSw.WriteLine("");

                strSw.WriteLine($"{item.Key}");

                strSw.Write(Helpers.StringifySdr(new List<int[]>(item.Value)));

            }

            if (sw != null)
            {
                sw.Write(strSw.ToString());
                sw.Flush();
                sw.Close();
            }

            Debug.WriteLine(strSw.ToString());
            return strSw.ToString();
        }


        private string ComputeHash(byte[] rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(rawData);

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }


        private static byte[] FlatArray(Cell[] output)
        {
            byte[] arr = new byte[output.Length];
            for (int i = 0; i < output.Length; i++)
            {
                arr[i] = (byte)output[i].Index;
            }
            return arr;
        }

        private static int[] GetCellIndicies(Cell[] output)
        {
            int[] arr = new int[output.Length];
            for (int i = 0; i < output.Length; i++)
            {
                arr[i] = output[i].Index;
            }
            return arr;
        }

        private int PredictNextValue(int[] activeArr, int[] predictedArr)
        {
            var same = predictedArr.Intersect(activeArr);

            return same.Count();
        }


    }
}
