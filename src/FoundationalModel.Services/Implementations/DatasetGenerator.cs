using FoundationalModel.Models.Dtos.Requests;
using System.Text;

namespace FoundationalModel.Services.Implementations
{
    public class DatasetGenerator : IDatasetReportGenerator
    {
        public string Generate(string datasetName, IReadOnlyList<ParsedTrainingExample> examples)
        {
            var categoryGroups = examples
                .GroupBy(x => x.Category)
                .OrderBy(group  => group.Key)
                .ToList();

            var duplicateInputCount = examples
                .GroupBy(x => x.Input.Trim(), StringComparer.OrdinalIgnoreCase)
                .Count(group => group.Count() > 1);

            var categoriesWithRetryableVariation = categoryGroups
                .Count(group => group
                .Select(x => x.Retryable.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1);

            var categoriesWithMultipleActions = categoryGroups
                .Count(group => group
                .Select(x => x.Action.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1);

            var report = new StringBuilder();

            report.AppendLine($"# {datasetName} Quality Report");
            report.AppendLine();

            report.AppendLine($"## Summary");
            report.AppendLine();
            report.AppendLine($"- Total examples: {examples.Count}");
            report.AppendLine($"- Categories: {categoryGroups.Count}");

            report.AppendLine($"- Exact duplicate inputs: {duplicateInputCount}");

            report.AppendLine($"- Categories with retryable variations: {categoriesWithRetryableVariation}");
            report.AppendLine($"- Categories with multiple actions: {categoriesWithMultipleActions}");
            report.AppendLine();

            report.AppendLine($"## Category Distribution");
            report.AppendLine();

            foreach(var group in categoryGroups)
            {
                report.AppendLine($"- {group.Key}: {group.Count()}");
            }

            report.AppendLine();

            foreach(var group in categoryGroups)
            {
                var uniqueActions = group
                    .Select(x => x.Action.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                report.AppendLine($"- {group.Key} : {uniqueActions} unique action(s)");
            }

            return report.ToString();

        }
    }
}
