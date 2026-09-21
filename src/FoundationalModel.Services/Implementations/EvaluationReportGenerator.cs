using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using System.Text;

namespace FoundationalModel.Services.Implementations
{
    public class EvaluationReportGenerator : IEvaluationReportGenerator
    {
        public string Genrate(ManualEvaluationSummary v1, ManualEvaluationSummary v2, int v1SchemaCompliant, int v2SchemaCompliant)
        {
            var report = new StringBuilder();

            report.AppendLine("# Chapter 8 Dataset Engineering Experiment");
            report.AppendLine();

            report.AppendLine("## Objective");
            report.AppendLine();
            report.AppendLine(
                "Evaluate whether improved dataset diversity and response-only loss " +
                "improve payment-incident fine-tuning performance.");
            report.AppendLine();

            report.AppendLine("## Dataset Changes");
            report.AppendLine();
            report.AppendLine("- V1: 50 examples across 10 categories.");
            report.AppendLine("- V2: 50 examples across the same 10 categories.");
            report.AppendLine("- V2 increased scenario diversity and action diversity.");
            report.AppendLine("- V2 introduced context-sensitive retryability where appropriate.");
            report.AppendLine("- Exact duplicates and train/eval leakage were not found.");
            report.AppendLine();

            report.AppendLine("## Training Change");
            report.AppendLine();
            report.AppendLine(
                "- V1 used full-sequence loss.");
            report.AppendLine(
                "- V2 used response-only loss by masking prompt tokens with -100.");
            report.AppendLine();

            report.AppendLine("## Evaluation Results");
            report.AppendLine();

            report.AppendLine("| Metric | V1 | V2 |");
            report.AppendLine("|---|---:|---:|");

            report.AppendLine(
                $"| Category correct | {v1.CategoryCorrect}/{v1.TotalCases} | {v2.CategoryCorrect}/{v2.TotalCases} |");

            report.AppendLine(
                $"| Retryable correct | {v1.RetryableCorrect}/{v1.TotalCases} | {v2.RetryableCorrect}/{v2.TotalCases} |");

            report.AppendLine(
                $"| Action correct | {v1.ActionCorrect}/{v1.TotalCases} | {v2.ActionCorrect}/{v2.TotalCases} |");

            report.AppendLine(
                $"| Schema compliant | {v1SchemaCompliant}/{v1.TotalCases} | {v2SchemaCompliant}/{v2.TotalCases} |");

            report.AppendLine(
                $"| Contradiction / hallucination | {v1.ContradictionsOrHallucinations}/{v1.TotalCases} | {v2.ContradictionsOrHallucinations}/{v2.TotalCases} |");

            report.AppendLine();

            report.AppendLine("## Conclusion");
            report.AppendLine();
            report.AppendLine(
                "V2 improved dataset quality and used a more appropriate training objective, " +
                "but it did not improve overall model performance on the frozen evaluation set.");

            report.AppendLine();
            report.AppendLine(
                "The experiment suggests that better dataset structure alone was insufficient " +
                "with only 50 examples and the Qwen2.5-0.5B base model.");

            report.AppendLine();
            report.AppendLine(
                "The next experiment should increase supervision density by providing multiple " +
                "examples for each scenario pattern while keeping the frozen evaluation set unchanged.");

            return report.ToString();
        }
    }
}
