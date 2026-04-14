using NUnit.Framework;
using ArduSafeMonAlpaca;

namespace ArduSafeMonAlpaca.Tests;

[TestFixture]
public class AlpacaResponseTests
{
    [Test]
    public void Ok_setsValueAndZeroError()
    {
        var r = AlpacaResult.Ok("hello");
        Assert.That(r.Value, Is.EqualTo("hello"));
        Assert.That(r.ErrorNumber, Is.EqualTo(0));
        Assert.That(r.ErrorMessage, Is.EqualTo(""));
        Assert.That(r.ServerTransactionID, Is.GreaterThan(0));
    }

    [Test]
    public void Ok_propagatesClientTransactionId()
    {
        var r = AlpacaResult.Ok(true, clientTxId: 42);
        Assert.That(r.ClientTransactionID, Is.EqualTo(42));
    }

    [Test]
    public void Fail_setsErrorNumberAndMessage()
    {
        var r = AlpacaResult.Fail<bool>(0x0400, "Not connected");
        Assert.That(r.ErrorNumber, Is.EqualTo(0x0400));
        Assert.That(r.ErrorMessage, Is.EqualTo("Not connected"));
        Assert.That(r.Value, Is.False);
    }

    [Test]
    public void ServerTransactionId_incrementsOnEachCall()
    {
        var r1 = AlpacaResult.Ok(1);
        var r2 = AlpacaResult.Ok(2);
        Assert.That(r2.ServerTransactionID, Is.GreaterThan(r1.ServerTransactionID));
    }
}
