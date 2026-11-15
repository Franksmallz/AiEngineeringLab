using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RiskInference.Api.Contracts;
using RiskInference.Api.Model;

namespace RiskInference.Api.Tests;

public class RiskEndpointTests : IClassFixture<WebApplicationFactory<RiskModelService>>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string ValidJson = """
        { "amount": 4500, "transactionHour": 2, "customerTransactionCount24h": 12,
          "recentFailureCount": 3, "beneficiaryAgeDays": 2, "isHighRiskCountry": true }
        """;

    private readonly HttpClient _client;

    public RiskEndpointTests(WebApplicationFactory<RiskModelService> factory) => _client = factory.CreateClient();

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string With(string field, string? value)
    {
        // Replaces one field of the valid body. A null value removes the field.
        var node = System.Text.Json.Nodes.JsonNode.Parse(ValidJson)!.AsObject();
        if (value is null) node.Remove(field);
        else node[field] = System.Text.Json.Nodes.JsonNode.Parse(value);
        return node.ToJsonString();
    }

    // ---- Valid requests ------------------------------------------------------

    [Fact]
    public async Task Valid_transaction_returns_200_and_a_prediction()
    {
        var response = await _client.PostAsync("/risk/predict", Json(ValidJson));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<RiskPredictionResponse>(Web))!;
        Assert.Contains(body.PredictedRisk, new[] { "Low", "Medium", "High" });
        Assert.InRange(body.HighRiskScore, 0f, 1f);
        Assert.Equal("chapter-06-v1", body.ModelVersion);
        Assert.Equal("risk-policy-v1", body.DecisionPolicyVersion);
        Assert.Equal(0.1121373102068901, body.Threshold);
        Assert.Equal(body.HighRiskScore >= body.Threshold, body.FlaggedHighRisk);
    }

    [Fact]
    public async Task The_response_uses_the_documented_field_names()
    {
        var response = await _client.PostAsync("/risk/predict", Json(ValidJson));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(
            new[] { "decisionPolicyVersion", "flaggedHighRisk", "highRiskScore", "modelVersion", "predictedRisk", "threshold" },
            names);
    }

    [Fact]
    public async Task Concurrent_valid_requests_get_consistent_predictions()
    {
        var expected = await (await _client.PostAsync("/risk/predict", Json(ValidJson)))
            .Content.ReadAsStringAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 300).Select(async _ =>
        {
            var response = await _client.PostAsync("/risk/predict", Json(ValidJson));
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        }));

        Assert.All(results, r =>
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(expected, r.Body);
        });
    }

    // ---- Invalid requests ----------------------------------------------------

    [Theory]
    [InlineData("amount", "-10", "amount")]
    [InlineData("amount", "0", "amount")]
    [InlineData("transactionHour", "24", "transactionHour")]
    [InlineData("transactionHour", "-1", "transactionHour")]
    [InlineData("transactionHour", "2.5", "transactionHour")]
    [InlineData("recentFailureCount", "-1", "recentFailureCount")]
    [InlineData("customerTransactionCount24h", "1.5", "customerTransactionCount24h")]
    [InlineData("beneficiaryAgeDays", "-3", "beneficiaryAgeDays")]
    public async Task Out_of_range_value_returns_400_and_names_the_field(string field, string value, string errorField)
    {
        var response = await _client.PostAsync("/risk/predict", Json(With(field, value)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty(errorField, out _));
    }

    [Theory]
    [InlineData("isHighRiskCountry")]
    [InlineData("amount")]
    [InlineData("transactionHour")]
    [InlineData("customerTransactionCount24h")]
    [InlineData("recentFailureCount")]
    [InlineData("beneficiaryAgeDays")]
    public async Task Missing_field_returns_400(string field)
    {
        var response = await _client.PostAsync("/risk/predict", Json(With(field, null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Missing_country_flag_is_not_read_as_false()
    {
        var response = await _client.PostAsync("/risk/predict", Json(With("isHighRiskCountry", null)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"amount\": \"lots\" }")]
    [InlineData("{ \"isHighRiskCountry\": \"yes\" }")]
    public async Task Malformed_json_returns_400(string body)
    {
        var response = await _client.PostAsync("/risk/predict", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("{ \"amount\": -1 }")]
    [InlineData("{ \"amount\": \"lots\" }")]
    public async Task Error_bodies_do_not_expose_internals(string body)
    {
        var response = await _client.PostAsync("/risk/predict", Json(body));
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        foreach (var secret in new[]
                 {
                     "Exception", "   at ", "StackTrace", ".cs", "Microsoft.", "System.",
                     "ML.NET", "PredictionEngine", "chapter-06", ".zip", "Models/", "Score", "SHA", "C:\\"
                 })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Health --------------------------------------------------------------

    [Fact]
    public async Task Health_returns_200_when_the_model_is_loaded()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("chapter-06-v1", root.GetProperty("modelVersion").GetString());
        Assert.Equal("risk-policy-v1", root.GetProperty("decisionPolicyVersion").GetString());
        Assert.Equal("risk-features-v1", root.GetProperty("featureSchemaVersion").GetString());
        Assert.Equal(0.1121373102068901, root.GetProperty("threshold").GetDouble());
    }
}
