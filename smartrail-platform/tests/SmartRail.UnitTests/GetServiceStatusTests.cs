using SmartRail.Application.Features.Status;
using SmartRail.Domain;
using Xunit;

namespace SmartRail.UnitTests;

public class GetServiceStatusTests
{
    [Fact]
    public void Returns_normal_status_for_main_line()
    {
        var result = GetServiceStatus.Handle(TimeProvider.System);
        Assert.Equal("Main Line", result.Line);
        Assert.Equal(StatusGroup.Normal, result.Group);
    }
}
