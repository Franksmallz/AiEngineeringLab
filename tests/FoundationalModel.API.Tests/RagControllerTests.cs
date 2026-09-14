using FoundationalModel.API.Controllers;
using FoundationalModel.Core.Enums;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoundationalModel.API.Tests;

public sealed class GenerateWithRagTests
{
    [Fact]
    public async Task Evaluate_rejects_missing_question_or_expected_answer()
    {
        var service = new StubRagService();
        var controller = new GenerateController(new StubGenerateService(), service);

        var result = await controller.GenerateWithRag(new RagEvaluationRequest { Question = "", ExpectedAnswer = "answer", ExpectedSource = "payments.md", MatchingType = MatchingType.Semantic }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(service.Called);
    }

    [Fact]
    public async Task Evaluate_returns_the_combined_result()
    {
        var expected = new RagCombinedResult { Id = 1, Question = "question", ExpectedAnswer = "answer", ExpectedSource = "payments.md" };
        var controller = new GenerateController(new StubGenerateService(), new StubRagService(expected));

        var result = await controller.GenerateWithRag(new RagEvaluationRequest { Question = "question", ExpectedAnswer = "answer", ExpectedSource = "payments.md", MatchingType = MatchingType.Direct }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task Service_caches_chunk_embeddings_but_always_embeds_the_question()
    {
        var root = Path.Combine(Path.GetTempPath(), "rag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        await File.WriteAllTextAsync(Path.Combine(root, "data", "payments.md"), "# Payments\n\nIdempotency prevents duplicate charges.");
        var embeddings = new StubEmbeddingService();
        var service = CreateService(root, embeddings, new StubGenerator());
        var request = new RagEvaluationRequest { Question = "How do retries avoid duplicate charges?", ExpectedAnswer = "Idempotency.", ExpectedSource = "payments.md", MatchingType = MatchingType.Semantic };

        await service.EvaluateAsync(request);
        var firstCallCount = embeddings.Requests.Count;
        var result = await service.EvaluateAsync(request);

        Assert.Equal(firstCallCount, embeddings.Requests.Count - 1); // only the second question embedding is new
        Assert.Equal(2, embeddings.Requests.Count(x => x == request.Question));
        Assert.True(File.Exists(Path.Combine(root, "data", "embeddings.json")));
        Assert.True(File.Exists(Path.Combine(root, "experiments", "week-06", "rag-combined-result.json")));
        Assert.DoesNotContain("embeddings", await File.ReadAllTextAsync(Path.Combine(root, "experiments", "week-06", "rag-combined-result.json")), StringComparison.OrdinalIgnoreCase);
    }

    private static RagEvaluationService CreateService(string root, StubEmbeddingService embeddings, StubGenerator generator) => new(
        new DocumentChunkService(), embeddings, new RetrieverService(), new VectorRetrieverService(), generator,
        new TestEnvironment(root), Options.Create(new RagSettings()), NullLogger<RagEvaluationService>.Instance,
        new DocumentLoader(
            new DocumentChunkService(),
            new TestPathResolver(root),
            Options.Create(new RagSettings()),
            new EmbeddingManagement(NullLogger<EmbeddingManagement>.Instance, embeddings)));

    private sealed class StubRagService(RagCombinedResult? result = null) : IRagEvaluationService
    {
        public bool Called { get; private set; }
        public Task<RagCombinedResult> EvaluateAsync(RagEvaluationRequest request, CancellationToken cancellationToken = default)
        { Called = true; return Task.FromResult(result ?? new RagCombinedResult()); }
    }

    private sealed class StubGenerateService : IGenerateService
    {
        public Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request) =>
            Task.FromResult(new SendMessageResponseDto());

        public Task<SendMessageResponseDto> SendMessageWithTools(SendMessageRequestDto request) =>
            Task.FromResult(new SendMessageResponseDto());
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public List<string> Requests { get; } = [];
        public Task<float[]> CreateEmbeddingAsync(string text)
        { Requests.Add(text); return Task.FromResult(new[] { 1f, 0f, 0f }); }
    }

    private sealed class StubGenerator : IGenerateService
    {
        public Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request) =>
            Task.FromResult(new SendMessageResponseDto { Success = true, Text = "response" });

        public Task<SendMessageResponseDto> SendMessageWithTools(SendMessageRequestDto request) =>
            Task.FromResult(new SendMessageResponseDto { Success = true, Text = "response" });
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestPathResolver(string root) : IPathResolver
    {
        public string ResolveConfiguredPath(string configuredPath) => Path.Combine(root, configuredPath);
    }
}
