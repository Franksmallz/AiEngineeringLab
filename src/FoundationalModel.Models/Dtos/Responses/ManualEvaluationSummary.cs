namespace FoundationalModel.Models.Dtos.Responses
{
    public sealed class ManualEvaluationSummary
    {
        public string ModelVersion { get; set; } = string.Empty;

        public int TotalCases { get; set; }

        public int CategoryCorrect { get; set; }

        public int RetryableCorrect { get; set; }

        public int ActionCorrect { get; set; }

        public int ContradictionsOrHallucinations { get; set; }

        public static ManualEvaluationSummary Calculate(ManualEvaluation evaluation)
        {
            return new ManualEvaluationSummary
            {
                ModelVersion = evaluation.ModelVersion,
                TotalCases = evaluation.Scores.Count,

                CategoryCorrect =
               evaluation.Scores.Count(x => x.CategoryCorrect),

                RetryableCorrect =
               evaluation.Scores.Count(x => x.RetryableCorrect),

                ActionCorrect =
               evaluation.Scores.Count(x => x.ActionCorrect),

                ContradictionsOrHallucinations =
               evaluation.Scores.Count(
                   x => x.HasContradictionOrHallucination)
            };
        }
    }
}
