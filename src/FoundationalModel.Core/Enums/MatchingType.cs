using System.Text.Json.Serialization;

namespace FoundationalModel.Core.Enums;

public enum MatchingType
{
    None = 0,
    Semantic = 1,
    Direct = 2,
    [JsonStringEnumMemberName("cross-document")]
    CrossDocument = 3
}
