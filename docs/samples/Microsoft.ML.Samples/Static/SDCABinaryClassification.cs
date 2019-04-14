﻿using System;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.StaticPipe;

namespace Samples.Static
{
    public class SdcaBinaryClassificationExample
    {
        public static void Example()
        {
            // Downloading a classification dataset from github.com/dotnet/machinelearning.
            // It will be stored in the same path as the executable
            string dataFilePath = Microsoft.ML.SamplesUtils.DatasetUtils.DownloadAdultDataset();

            // Data Preview
            // 1. Column [Label]: IsOver50K (boolean)
            // 2. Column: workclass (text/categorical)
            // 3. Column: education (text/categorical)
            // 4. Column: marital-status (text/categorical)
            // 5. Column: occupation (text/categorical)
            // 6. Column: relationship (text/categorical)
            // 7. Column: ethnicity (text/categorical)
            // 8. Column: sex (text/categorical)
            // 9. Column: native-country-region (text/categorical)
            // 10. Column: age (numeric)
            // 11. Column: fnlwgt (numeric)
            // 12. Column: education-num (numeric)
            // 13. Column: capital-gain (numeric)
            // 14. Column: capital-loss (numeric)
            // 15. Column: hours-per-week (numeric)

            // Creating the ML.Net IHostEnvironment object, needed for the pipeline
            var mlContext = new MLContext();

            // Creating Data Loader with the initial schema based on the format of the data
            var loader = TextLoaderStatic.CreateLoader(
                mlContext,
                c => (
                    Age: c.LoadFloat(0),
                    Workclass: c.LoadText(1),
                    Fnlwgt: c.LoadFloat(2),
                    Education: c.LoadText(3),
                    EducationNum: c.LoadFloat(4),
                    MaritalStatus: c.LoadText(5),
                    Occupation: c.LoadText(6),
                    Relationship: c.LoadText(7),
                    Ethnicity: c.LoadText(8),
                    Sex: c.LoadText(9),
                    CapitalGain: c.LoadFloat(10),
                    CapitalLoss: c.LoadFloat(11),
                    HoursPerWeek: c.LoadFloat(12),
                    NativeCountry: c.LoadText(13),
                    IsOver50K: c.LoadBool(14)),
                separator: ',',
                hasHeader: true);

            // Load the data, and leave 10% out, so we can use them for testing
            var data = loader.Load(dataFilePath);
            var (trainData, testData) = mlContext.Data.TrainTestSplit(data, testFraction: 0.1);

            // Create the Estimator
            var learningPipeline = loader.MakeNewEstimator()
                .Append(row => (
                        Features: row.Age.ConcatWith(
                            row.EducationNum,
                            row.MaritalStatus.OneHotEncoding(),
                            row.Occupation.OneHotEncoding(),
                            row.Relationship.OneHotEncoding(),
                            row.Ethnicity.OneHotEncoding(),
                            row.Sex.OneHotEncoding(),
                            row.HoursPerWeek,
                            row.NativeCountry.OneHotEncoding().SelectFeaturesBasedOnCount(count: 10)),
                        Label: row.IsOver50K))
                .Append(row => (
                        Features: row.Features.Normalize(),
                        Label: row.Label,
                        Score: mlContext.BinaryClassification.Trainers.Sdca(
                            row.Label,
                            row.Features,
                            l1Threshold: 0.25f,
                            numberOfIterations: 100)))
                .Append(row => (
                    Label: row.Label,
                    Score: row.Score,
                    PredictedLabel: row.Score.predictedLabel));

            // Fit this Pipeline to the Training Data
            var model = learningPipeline.Fit(trainData);

            // Evaluate how the model is doing on the test data
            var dataWithPredictions = model.Transform(testData);

            var metrics = mlContext.BinaryClassification.EvaluateWithPRCurve(dataWithPredictions, row => row.Label, row => row.Score, out List<BinaryPrecisionRecallDataPoint> prCurve);

            Console.WriteLine($"Accuracy: {metrics.Accuracy}"); // 0.83
            Console.WriteLine($"AUC: {metrics.AreaUnderRocCurve}"); // 0.88
            Console.WriteLine($"F1 Score: {metrics.F1Score}"); // 0.59

            Console.WriteLine($"Negative Precision: {metrics.NegativePrecision}"); // 0.87
            Console.WriteLine($"Negative Recall: {metrics.NegativeRecall}"); // 0.91
            Console.WriteLine($"Positive Precision: {metrics.PositivePrecision}"); // 0.65
            Console.WriteLine($"Positive Recall: {metrics.PositiveRecall}"); // 0.55

            foreach(var prData in prCurve)
            {
                Console.Write($"Threshold: {prData.Threshold} ");
                Console.Write($"Precision: {prData.Precision} ");
                Console.Write($"Recall: {prData.Recall} ");
                Console.WriteLine($"FPR: {prData.FalsePositiveRate}");
            }
        }
    }
}