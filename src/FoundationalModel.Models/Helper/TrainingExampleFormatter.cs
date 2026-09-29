namespace FoundationalModel.Models.Helper
{
    public class TrainingExampleFormatter
    {
        public static string BuildExpectedOutput(string category, bool retryable, string action)
        {
            return
                $"Category: {category}\n" +
                $"Retryable: {(retryable ? "Yes" : "No")}\n" +
                $"Action: {action}";
        }
    }
}
