namespace FoundationalModel.Services.Interfaces
{
    public interface IPaymentIncidentModelProxyClient
    {
        Task<HttpResponseMessage> PostAsync(string uri, HttpContent content, CancellationToken cancellationToken = default);
          
    }
}
