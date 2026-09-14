namespace FoundationalModel.Models.Dtos.Requests
{
    public class UserContext
    {
        public string UserId { get; set; }
        public string TenantId { get; init; }
        public HashSet<string> Permissions { get; init; } = [];
    }
}
