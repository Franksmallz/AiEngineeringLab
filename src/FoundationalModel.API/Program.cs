using Anthropic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Services.Autofac;
using OpenAI.Embeddings;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var anthropicConfig = builder.Configuration.GetSection(AnthropicProviderSettings.SectionName).Get<AnthropicProviderSettings>();
var openAiConfig = builder.Configuration.GetSection(OpenAiProviderSettings.SectionName).Get<OpenAiProviderSettings>()
    ?? new OpenAiProviderSettings();

builder.Services.AddScoped<AnthropicClient>(_ =>  new() { ApiKey = anthropicConfig.ApiKey, Timeout = TimeSpan.FromSeconds(anthropicConfig.Timeout), MaxRetries = anthropicConfig.MaxRetries});
builder.Services.Configure<AnthropicProviderSettings>(
    builder.Configuration.GetSection(AnthropicProviderSettings.SectionName));
builder.Services.Configure<OpenAiProviderSettings>(
    builder.Configuration.GetSection(OpenAiProviderSettings.SectionName));
builder.Services.Configure<RagSettings>(
    builder.Configuration.GetSection(RagSettings.SectionName));
builder.Services.AddScoped(_ => new EmbeddingClient(openAiConfig.Model, openAiConfig.ApiKey));
// Configure Autofac through the host builder. UseAutofacServiceProviderFactory()
// returns an IHostBuilder, so module registration must happen through
// ConfigureContainer rather than by chaining a custom AddAutofacModule method.
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule<AutofacContainerModule>();
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
