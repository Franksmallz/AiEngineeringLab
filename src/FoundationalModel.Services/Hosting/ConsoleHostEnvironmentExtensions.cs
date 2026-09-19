using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FoundationalModel.Services.Hosting;

public static class ConsoleHostEnvironmentExtensions
{
    public static IServiceCollection AddConsoleHostEnvironment(
        this IServiceCollection services,
        string environmentName,
        string? contentRootPath = null)
    {
        var contentRoot = Path.GetFullPath(contentRootPath ?? AppContext.BaseDirectory);

        services.AddSingleton<IHostEnvironment>(new ConsoleHostEnvironment
        {
            EnvironmentName = environmentName,
            ApplicationName = AppDomain.CurrentDomain.FriendlyName,
            ContentRootPath = contentRoot,
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot)
        });

        return services;
    }

    private sealed class ConsoleHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
