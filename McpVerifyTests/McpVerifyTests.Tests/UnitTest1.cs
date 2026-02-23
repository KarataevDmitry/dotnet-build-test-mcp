using Xunit;

namespace McpVerifyTests.Tests;

public class UnitTest1
{
    [Fact]
    public void Pass_Always_Succeeds()
    {
        Assert.True(true);
    }

    [Fact]
    public void Fail_Always_Fails_For_Mcp_Verification()
    {
        Assert.Equal(1, 2);
    }
}
