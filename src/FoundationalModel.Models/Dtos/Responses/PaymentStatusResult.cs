namespace FoundationalModel.Models.Dtos.Responses
{
    public class PaymentStatusResult
    {
        public string PaymentId { get; set; }
        public string Status { get; set; }  
        public string Reason { get; set; }
        public string TenantId { get; set; }
    }
}
