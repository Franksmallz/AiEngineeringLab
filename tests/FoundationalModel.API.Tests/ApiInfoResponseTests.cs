using FoundationalModel.API.Contracts;

namespace FoundationalModel.API.Tests;

public sealed class ApiInfoResponseTests
{
    [Fact]
    public void Response_keeps_the_public_contract_explicit()
    {
        var now = DateTimeOffset.UtcNow;
        var response = new ApiInfoResponse("ai-engineering-hands-on-api", "v1", "Testing", now);

        Assert.Equal("ai-engineering-hands-on-api", response.Name);
        Assert.Equal("v1", response.Version);
        Assert.Equal("Testing", response.Environment);
        Assert.Equal(now, response.UtcTime);
    }
}
