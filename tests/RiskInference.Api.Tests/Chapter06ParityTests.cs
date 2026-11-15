using System.Collections;
using Microsoft.ML;
using Microsoft.ML.Data;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Implementations.ML;

namespace RiskInference.Api.Tests;

// Every 25th record of the Chapter 6 training file (200 records). Training
// records are used on purpose: the Chapter 6 test set stays untouched.
internal sealed class DatasetSample : IEnumerable<TransactionRiskData>
{
    public IEnumerator<TransactionRiskData> GetEnumerator() =>
        new DatasetReader(TestSupport.PathResolver())
            .Read("chapter-06/train-5000.csv")
            .Where((_, i) => i % 25 == 0)
            .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// The reference is the Chapter 6 evaluation path: load the frozen model with
// FinalModelStore, run the whole batch through Transform, and read the Score
// slots by class name. The API uses one PredictionEngine per call. Both paths
// must agree.
public class Chapter06ParityTests
{
    private const float ScoreTolerance = 1e-6f;

    [Fact]
    public void The_api_matches_the_chapter_06_batch_pipeline()
    {
        var records = new DatasetSample().ToList();
        Assert.Equal(200, records.Count);

        var reference = ChapterSixBatchScores(records);
        var service = TestSupport.CreateService();

        var flagged = 0;
        var mediumButFlagged = 0;
        for (var i = 0; i < records.Count; i++)
        {
            var actual = service.Predict(records[i]);
            var (expectedClass, expectedHighScore) = reference[i];

            Assert.Equal(expectedClass, actual.PredictedRisk);
            Assert.Equal(expectedHighScore, actual.HighRiskScore, ScoreTolerance);
            // The decision must match exactly, with the Chapter 6 rule.
            Assert.Equal((double)expectedHighScore >= TestSupport.Threshold, actual.FlaggedHighRisk);

            if (actual.FlaggedHighRisk) flagged++;
            if (actual.FlaggedHighRisk && actual.PredictedRisk != "High") mediumButFlagged++;
        }

        // The sample must exercise both outcomes, or the test proves little.
        Assert.InRange(flagged, 1, records.Count - 1);
        Console.WriteLine($"Parity: {records.Count} records, {flagged} flagged, {mediumButFlagged} flagged but not predicted High.");
    }

    private static List<(string PredictedClass, float HighScore)> ChapterSixBatchScores(
        List<TransactionRiskData> records)
    {
        var ml = new MLContext();
        var model = new FinalModelStore(TestSupport.PathResolver())
            .LoadModel(ml, TestSupport.ModelPath, TestSupport.ModelSha256);

        var scored = model.Transform(ml.Data.LoadFromEnumerable(records));

        VBuffer<ReadOnlyMemory<char>> slotNames = default;
        scored.Schema["Score"].GetSlotNames(ref slotNames);
        var highSlot = slotNames.DenseValues().Select(n => n.ToString()).ToList().IndexOf("High");
        Assert.True(highSlot >= 0);

        return ml.Data.CreateEnumerable<TransactionRiskPrediction>(scored, reuseRowObject: false)
            .Select(p => (p.RiskLevel, p.Score[highSlot]))
            .ToList();
    }
}
