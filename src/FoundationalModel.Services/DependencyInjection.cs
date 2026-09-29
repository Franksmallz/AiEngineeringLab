using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace FoundationalModel.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection RegisterHttpClient(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            serviceCollection.AddHttpClient<IPaymentIncidentModelProxyClient, PaymentIncidentModelProxyClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["ModelProviderSeetings:PaymentIncidentModel:BaseUrl"]);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration["ModelProviderSeetings:PaymentIncidentModel:ApiKey"]);
            });
            return serviceCollection;
        }
    }
}
