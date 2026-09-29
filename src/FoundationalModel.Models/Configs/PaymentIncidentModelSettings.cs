namespace FoundationalModel.Models.Configs
{
    public class PaymentIncidentModelSettings
    {
        public const string SectionName = "ModelProviderSeetings:PaymentIncidentModel";
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string EndpointId { get; set; } = string.Empty;
    }
}
