using RiskClassificationLab.Models;

namespace RiskInference.Api.Contracts;

// Input contract for POST /risk/predict. These are the six raw inputs used in
// Chapter 6 training. The caller never sends engineered features. The saved
// model pipeline derives IsNewBeneficiary, IsLargeTransaction,
// NewBeneficiaryLargeAmount and IsLateNight itself, with the same
// EngineeredFeatures.Map code as in training.
public sealed class RiskPredictionRequest
{
    // Each field is nullable so a missing field is an error, not a silent zero.
    // The model trains on float columns, so the fields stay float. Validate()
    // makes sure that the whole-number fields really are whole numbers.
    public float? Amount { get; init; }
    public float? TransactionHour { get; init; }
    public float? CustomerTransactionCount24h { get; init; }
    public float? RecentFailureCount { get; init; }
    public float? BeneficiaryAgeDays { get; init; }
    public bool? IsHighRiskCountry { get; init; }

    // Returns the invalid fields and one message for each. The keys use the
    // JSON field names (camelCase). An empty result means the request is valid.
    // The messages only describe the input rule. They never name internals.
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        Check(Amount, nameof(Amount), "a number greater than 0",
            v => v > 0, errors);
        Check(TransactionHour, nameof(TransactionHour), "a whole number from 0 to 23",
            v => IsWhole(v) && v >= 0 && v <= 23, errors);
        Check(CustomerTransactionCount24h, nameof(CustomerTransactionCount24h), "a whole number, 0 or more",
            v => IsWhole(v) && v >= 0, errors);
        Check(RecentFailureCount, nameof(RecentFailureCount), "a whole number, 0 or more",
            v => IsWhole(v) && v >= 0, errors);
        Check(BeneficiaryAgeDays, nameof(BeneficiaryAgeDays), "a whole number, 0 or more",
            v => IsWhole(v) && v >= 0, errors);

        if (IsHighRiskCountry is null)
            errors[ToJsonName(nameof(IsHighRiskCountry))] = [$"{ToJsonName(nameof(IsHighRiskCountry))} is required."];

        return errors;
    }

    // Maps a valid request to the exact type the model was trained on. The
    // caller must validate first. RiskLevel is the training label. The model
    // ignores it when it scores, so it stays empty.
    public TransactionRiskData ToModelInput()
    {
        if (Validate().Count > 0)
            throw new InvalidOperationException("The request is not valid.");

        return new TransactionRiskData
        {
            Amount = Amount!.Value,
            TransactionHour = TransactionHour!.Value,
            CustomerTransactionCount24h = CustomerTransactionCount24h!.Value,
            RecentFailureCount = RecentFailureCount!.Value,
            BeneficiaryAgeDays = BeneficiaryAgeDays!.Value,
            IsHighRiskCountry = IsHighRiskCountry!.Value
        };
    }

    private static bool IsWhole(float value) => value == MathF.Floor(value);

    private static void Check(
        float? value, string field, string rule, Func<float, bool> isValid,
        Dictionary<string, string[]> errors)
    {
        var name = ToJsonName(field);

        if (value is null)
            errors[name] = [$"{name} is required."];
        else if (!float.IsFinite(value.Value))
            errors[name] = [$"{name} must be a finite number."];
        else if (!isValid(value.Value))
            errors[name] = [$"{name} must be {rule}."];
    }

    private static string ToJsonName(string propertyName) =>
        char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
