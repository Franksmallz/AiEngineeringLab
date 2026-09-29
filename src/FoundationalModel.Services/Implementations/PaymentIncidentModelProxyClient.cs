using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class PaymentIncidentModelProxyClient : IPaymentIncidentModelProxyClient
    {
        private readonly HttpClient _httpClient;

        public PaymentIncidentModelProxyClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken cancellationToken = default)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await _httpClient.PostAsync(uri, content, cancellationToken);
                return response;
            }
        }
    }
}
