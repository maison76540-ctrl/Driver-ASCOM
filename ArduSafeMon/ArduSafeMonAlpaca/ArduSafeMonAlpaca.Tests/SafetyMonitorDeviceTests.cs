using NUnit.Framework;
using ArduSafeMonAlpaca;

namespace ArduSafeMonAlpaca.Tests;

[TestFixture]
public class SafetyMonitorDeviceTests
{
    // ── ParseResponse ──────────────────────────────────────────────────────

    [Test]
    public void ParseResponse_safe_returnsTrue()
        => Assert.That(SafetyMonitorDevice.ParseResponse("safe"), Is.True);

    [Test]
    public void ParseResponse_SAFE_uppercase_returnsTrue()
        => Assert.That(SafetyMonitorDevice.ParseResponse("SAFE"), Is.True);

    [Test]
    public void ParseResponse_safeWithSpaces_returnsTrue()
        => Assert.That(SafetyMonitorDevice.ParseResponse("  safe  "), Is.True);

    [Test]
    public void ParseResponse_notsafe_returnsFalse()
        => Assert.That(SafetyMonitorDevice.ParseResponse("notsafe"), Is.False);

    [Test]
    public void ParseResponse_empty_returnsFalse()
        => Assert.That(SafetyMonitorDevice.ParseResponse(""), Is.False);

    [Test]
    public void ParseResponse_null_returnsFalse()
        => Assert.That(SafetyMonitorDevice.ParseResponse(null!), Is.False);

    // ── Simulation mode ────────────────────────────────────────────────────

    [Test]
    public void Connect_simulationMode_doesNotThrow()
    {
        var dev = new SafetyMonitorDevice(new AppSettings { SimulationMode = true });
        Assert.DoesNotThrow(() => dev.Connect());
        dev.Disconnect();
    }

    [Test]
    public void IsSafe_simulationModeSafe_returnsTrue()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = true, SimulatedSafe = true });
        dev.Connect();
        Assert.That(dev.IsSafe, Is.True);
        dev.Disconnect();
    }

    [Test]
    public void IsSafe_simulationModeNotSafe_returnsFalse()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = true, SimulatedSafe = false });
        dev.Connect();
        Assert.That(dev.IsSafe, Is.False);
        dev.Disconnect();
    }

    [Test]
    public void IsSafe_notConnected_returnsFalse()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = true, SimulatedSafe = true });
        Assert.That(dev.IsSafe, Is.False);
    }

    [Test]
    public void Connected_afterConnect_isTrue()
    {
        var dev = new SafetyMonitorDevice(new AppSettings { SimulationMode = true });
        dev.Connect();
        Assert.That(dev.Connected, Is.True);
        dev.Disconnect();
    }

    [Test]
    public void Connected_afterDisconnect_isFalse()
    {
        var dev = new SafetyMonitorDevice(new AppSettings { SimulationMode = true });
        dev.Connect();
        dev.Disconnect();
        Assert.That(dev.Connected, Is.False);
    }

    [Test]
    public void Connect_portNotFound_throwsWithMessage()
    {
        var dev = new SafetyMonitorDevice(
            new AppSettings { SimulationMode = false, ComPort = "COM99" });
        var ex = Assert.Throws<InvalidOperationException>(() => dev.Connect());
        Assert.That(ex!.Message, Does.Contain("COM99"));
    }
}
