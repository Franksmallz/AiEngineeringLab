using System.Text.Json;
using RiskClassificationLab.Models;
using RiskInference.Api.Contracts;

namespace RiskInference.Api.Tests;

public class RiskPredictionRequestTests
{
    private static RiskPredictionRequest With(float amount = 250, float hour = 14) => new()
    {
        Amount = amount, TransactionHour = hour, CustomerTransactionCount24h = 2,
        RecentFailureCount = 0, BeneficiaryAgeDays = 90, IsHighRiskCountry = false
    };

    [Fact]
    public void Valid_request_has_no_errors() => Assert.Empty(With().Validate());

    [Fact]
    public void Empty_request_reports_every_field() =>
        Assert.Equal(6, new RiskPredictionRequest().Validate().Count);

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Bad_amount_is_rejected(float amount) =>
        Assert.True(With(amount: amount).Validate().ContainsKey("amount"));

    [Theory]
    [InlineData(-1f)]
    [InlineData(24f)]
    [InlineData(2.5f)]
    public void Hour_outside_0_to_23_is_rejected(float hour) =>
        Assert.True(With(hour: hour).Validate().ContainsKey("transactionHour"));

    [Theory]
    [InlineData("customerTransactionCount24h")]
    [InlineData("recentFailureCount")]
    [InlineData("beneficiaryAgeDays")]
    public void Count_and_age_fields_must_be_non_negative_whole_numbers(string field)
    {
        foreach (var bad in new[] { -1f, 1.5f })
        {
            var request = new RiskPredictionRequest
            {
                Amount = 250, TransactionHour = 14, IsHighRiskCountry = false,
                CustomerTransactionCount24h = field == "customerTransactionCount24h" ? bad : 1,
                RecentFailureCount = field == "recentFailureCount" ? bad : 0,
                BeneficiaryAgeDays = field == "beneficiaryAgeDays" ? bad : 30
            };
            var errors = request.Validate();
            Assert.Single(errors);
            Assert.True(errors.ContainsKey(field));
        }
    }

    [Fact]
    public void The_example_json_from_the_spec_binds_and_is_valid()
    {
        const string json = """
            { "amount": 4500, "transactionHour": 2, "customerTransactionCount24h": 12,
              "recentFailureCount": 3, "beneficiaryAgeDays": 2, "isHighRiskCountry": true }
            """;
        var request = JsonSerializer.Deserialize<RiskPredictionRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Empty(request.Validate());
        var input = request.ToModelInput();
        Assert.Equal(4500f, input.Amount);
        Assert.Equal(2f, input.BeneficiaryAgeDays);
        Assert.True(input.IsHighRiskCountry);
    }

    [Fact]
    public void ToModelInput_rejects_an_invalid_request() =>
        Assert.Throws<InvalidOperationException>(() => new RiskPredictionRequest().ToModelInput());

    // Guard against drift: the request fields must stay the same names and
    // types, in the same order, as the Chapter 6 training columns (not the label).
    // The one allowed difference is nullability, which gives "required" checks.
    [Fact]
    public void Request_fields_match_the_chapter_06_training_inputs()
    {
        var training = typeof(TransactionRiskData).GetProperties()
            .Where(p => p.Name != nameof(TransactionRiskData.RiskLevel))
            .Select(p => (p.Name, p.PropertyType))
            .ToList();

        var request = typeof(RiskPredictionRequest).GetProperties()
            .Select(p => (p.Name, Type: Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
            .ToList();

        Assert.Equal(training.Select(t => t.Name), request.Select(r => r.Name));
        Assert.Equal(training.Select(t => t.PropertyType), request.Select(r => r.Type));
    }

    // The deployed service must derive the same four features as training.
    // Chapter 6 rules: new beneficiary < 7 days, large amount > 3000,
    // late night hour <= 4. Columns: amount, hour, age, then the four outputs
    // (isNew, isLarge, newAndLarge, lateNight).
    [Theory]
    [InlineData(4500f, 2f, 2f, 1f, 1f, 1f, 1f)]
    [InlineData(3000f, 4f, 7f, 0f, 0f, 0f, 1f)]        // all three boundary values: not new, not large, late night
    [InlineData(3000.01f, 5f, 6.99f, 1f, 1f, 1f, 0f)]  // just past the boundaries
    [InlineData(100f, 12f, 365f, 0f, 0f, 0f, 0f)]
    public void Engineered_features_follow_the_chapter_06_rules(
        float amount, float hour, float ageDays, float isNew, float isLarge, float newAndLarge, float lateNight)
    {
        var output = new EngineeredFeatures();
        EngineeredFeatures.Map(
            new EngineeredFeatureInput { Amount = amount, TransactionHour = hour, BeneficiaryAgeDays = ageDays },
            output);

        Assert.Equal(isNew, output.IsNewBeneficiary);
        Assert.Equal(isLarge, output.IsLargeTransaction);
        Assert.Equal(newAndLarge, output.NewBeneficiaryLargeAmount);
        Assert.Equal(lateNight, output.IsLateNight);
    }
}
