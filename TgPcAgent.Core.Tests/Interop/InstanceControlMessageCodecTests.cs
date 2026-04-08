using TgPcAgent.Core.Interop;

namespace TgPcAgent.Core.Tests.Interop;

public sealed class InstanceControlMessageCodecTests
{
    [Theory]
    [InlineData(InstanceControlCommand.ShutdownExisting, "shutdown-existing")]
    [InlineData(InstanceControlCommand.RestartExisting, "restart-existing")]
    public void Encode_ReturnsExpectedWireValue(InstanceControlCommand command, string expected)
    {
        var encoded = InstanceControlMessageCodec.Encode(command);

        Assert.Equal(expected, encoded);
    }

    [Theory]
    [InlineData("shutdown-existing", InstanceControlCommand.ShutdownExisting)]
    [InlineData("restart-existing", InstanceControlCommand.RestartExisting)]
    public void TryDecode_KnownWireValue_ReturnsCommand(string payload, InstanceControlCommand expected)
    {
        var success = InstanceControlMessageCodec.TryDecode(payload, out var command);

        Assert.True(success);
        Assert.Equal(expected, command);
    }

    [Fact]
    public void TryDecode_UnknownWireValue_ReturnsFalse()
    {
        var success = InstanceControlMessageCodec.TryDecode("noop", out var command);

        Assert.False(success);
        Assert.Equal(InstanceControlCommand.Unknown, command);
    }
}
