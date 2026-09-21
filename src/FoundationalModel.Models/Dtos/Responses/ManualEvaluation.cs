using FoundationalModel.Models.Dtos.Requests;

namespace FoundationalModel.Models.Dtos.Responses
{
    public class ManualEvaluation
    {
        public string ModelVersion { get; set; } = string.Empty;
        public List<ManualSemanticScore> Scores { get; set; } = [];


        public static ManualEvaluation Create(string modelVersion, IReadOnlyList<EvaluationResult> results)
        {
            return new ManualEvaluation
            {
                ModelVersion = modelVersion,
                Scores = results
                     .Select((_, index) => new ManualSemanticScore
                     {
                         CaseNumber = index + 1,
                         CategoryCorrect = false,
                         ActionCorrect = false,
                         HasContradictionOrHallucination = false,
                         Notes = string.Empty

                     })
                     .ToList()
            };
        }
    }
}
