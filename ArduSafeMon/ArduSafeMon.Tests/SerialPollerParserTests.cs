using NUnit.Framework;
using ASCOM.ArduSafeMon;

namespace ArduSafeMon.Tests
{
    [TestFixture]
    public class SerialPollerParserTests
    {
        [Test]
        public void ParseResponse_SafeHash_ReturnsTrue()
        {
            bool result = SerialPoller.ParseResponse("safe");
            Assert.That(result, Is.True);
        }

        [Test]
        public void ParseResponse_NotsafeHash_ReturnsFalse()
        {
            bool result = SerialPoller.ParseResponse("notsafe");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ParseResponse_EmptyString_ReturnsFalse()
        {
            bool result = SerialPoller.ParseResponse("");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ParseResponse_UnknownString_ReturnsFalse()
        {
            bool result = SerialPoller.ParseResponse("garbage");
            Assert.That(result, Is.False);
        }

        [Test]
        public void ParseResponse_CaseInsensitive_Safe_ReturnsTrue()
        {
            bool result = SerialPoller.ParseResponse("SAFE");
            Assert.That(result, Is.True);
        }
    }
}
