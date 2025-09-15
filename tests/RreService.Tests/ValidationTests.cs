using RreService.Abstractions;
using Xunit;

public class ValidationTests
{
    [Fact]
    public void MissingExecutorRejected()
    {
        var (ok, err) = Validation.Validate(new NormalizedRequest("", null, null, null, null, null));
        Assert.False(ok);
        Assert.Contains("executor", err);
    }
}