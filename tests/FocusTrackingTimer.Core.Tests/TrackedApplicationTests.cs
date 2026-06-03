using FocusTrackingTimer.Core.Tracking;

namespace FocusTrackingTimer.Core.Tests;

public class TrackedApplicationTests
{
    [Fact]
    public void ConstructorTrimsInputValues()
    {
        TrackedApplication trackedApplication = new(" chrome ", " Chrome Browser ");

        Assert.Equal("chrome", trackedApplication.ProcessName);
        Assert.Equal("Chrome Browser", trackedApplication.DisplayName);
    }

    [Fact]
    public void ConstructorThrowsWhenProcessNameIsMissing()
    {
        Assert.Throws<ArgumentException>(() => new TrackedApplication(" ", "Chrome"));
    }

    [Fact]
    public void ConstructorThrowsWhenDisplayNameIsMissing()
    {
        Assert.Throws<ArgumentException>(() => new TrackedApplication("chrome", " "));
    }

    [Fact]
    public void FromExecutableInputNormalizesExecutableName()
    {
        TrackedApplication trackedApplication = TrackedApplication.FromExecutableInput(@"C:\Apps\Unity.exe");

        Assert.Equal("Unity", trackedApplication.ProcessName);
        Assert.Equal("Unity", trackedApplication.DisplayName);
    }
}
