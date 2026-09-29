using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace FoundationalModel.Services.Implementations
{
    public class PaymentIncidentModelServiceProxy : IPaymentIncidentModelServiceProxy
    {
        private readonly PaymentIncidentModelSettings _settings;
        private readonly IPaymentIncidentModelProxyClient _client;
        public PaymentIncidentModelServiceProxy(IOptions<PaymentIncidentModelSettings> options, 
            IPaymentIncidentModelProxyClient client)
        {
            _settings = options.Value;
            _client = client;
        }
        public async Task<PaymentIncidentResponse> GenerateAsync(PaymentIncidentRequest request)
        {
            var requestUri = $"v2/{_settings.EndpointId}/runsync";

            var messageJson = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, Application.Json);
            var httpResponse = await _client.PostAsync(requestUri, messageJson);

            string contentSting = await httpResponse.Content.ReadAsStringAsync();
            if(!httpResponse.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonSerializer.Deserialize<PaymentIncidentResponse>(contentSting) ?? throw new InvalidOperationException("Unable to answer at the moment");
        }
    }
}
