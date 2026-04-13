using NUnit.Framework;
using ASCOM.ArduSafeMon;

namespace ArduSafeMon.Tests
{
    [TestFixture]
    public class DriverSimulationTests
    {
        [Test]
        public void SimulationMode_SimulatedSafeTrue_IsSafeReturnsTrue()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            driver.Connect();
            Assert.That(driver.IsSafe, Is.True);
            driver.Disconnect();
        }

        [Test]
        public void SimulationMode_SimulatedSafeFalse_IsSafeReturnsFalse()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: false);
            driver.Connect();
            Assert.That(driver.IsSafe, Is.False);
            driver.Disconnect();
        }

        [Test]
        public void SimulationMode_ConnectSucceeds_WithoutSerialPort()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            Assert.DoesNotThrow(() => driver.Connect());
            Assert.That(driver.Connected, Is.True);
            driver.Disconnect();
        }

        [Test]
        public void SimulationMode_DisconnectSetsConnectedFalse()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            driver.Connect();
            driver.Disconnect();
            Assert.That(driver.Connected, Is.False);
        }

        [Test]
        public void Name_ReturnsArduSafeMon()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            Assert.That(driver.Name, Is.EqualTo("ArduSafeMon"));
        }

        [Test]
        public void InterfaceVersion_Returns2()
        {
            var driver = new SafetyMonitor(simulationMode: true, simulatedSafe: true);
            Assert.That(driver.InterfaceVersion, Is.EqualTo((short)2));
        }
    }
}
