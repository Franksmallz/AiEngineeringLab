using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class DatasetAuditService : IDatasetAuditService
    {
        public void PrintSummary(string name, IReadOnlyList<ParsedTrainingExample> examples)
        {
            Console.WriteLine();
            Console.WriteLine($"===== {name} =====");
            Console.WriteLine($"Total examples: {examples.Count}");

            var categoryCounts = examples
                .GroupBy(x => x.Category)
                .OrderBy(x => x.Key)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("Category distribution:");

            foreach (var group in categoryCounts)
            {
                Console.WriteLine($"{group.Key}: {group.Count()}");
            }

            var duplicateInputs = examples
                .GroupBy(
                    x => x.Input.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToList();

            Console.WriteLine();
            Console.WriteLine(
                $"Exact duplicate inputs: {duplicateInputs.Count}");

            var retryableVariation = examples
                .GroupBy(x => x.Category)
                .Select(group => new
                {
                    Category = group.Key,
                    Count = group
                        .Select(x => x.Retryable)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()
                })
                .OrderBy(x => x.Category)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("Retryable variation:");

            foreach (var item in retryableVariation)
            {
                Console.WriteLine(
                    $"{item.Category}: {item.Count} unique value(s)");
            }

            var actionVariation = examples
                .GroupBy(x => x.Category)
                .Select(group => new
                {
                    Category = group.Key,
                    Count = group
                        .Select(x => x.Action.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()
                })
                .OrderBy(x => x.Category)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("Action variation:");

            foreach (var item in actionVariation)
            {
                Console.WriteLine(
                    $"{item.Category}: {item.Count} unique action(s)");
            }
        }
    }
}
