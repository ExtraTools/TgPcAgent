namespace TgPcAgent.App.Scripts.Tools;

public sealed record VolumeMuteParams;
public sealed record VolumeUnmuteParams;

public sealed class VolumeMuteTool : ScriptToolBase<VolumeMuteParams>
{
    public override string Name => "volume.mute";
    public override string Description => "Выключить звук";

    protected override Task<StepResult> ExecuteTypedAsync(VolumeMuteParams p, ScriptContext ctx, CancellationToken ct)
    {
        NativeAudio.SetMasterVolume(0f);
        return Task.FromResult(new StepResult(true, "Звук выключен"));
    }
}

public sealed class VolumeUnmuteTool : ScriptToolBase<VolumeUnmuteParams>
{
    public override string Name => "volume.unmute";
    public override string Description => "Восстановить звук (50%)";

    protected override Task<StepResult> ExecuteTypedAsync(VolumeUnmuteParams p, ScriptContext ctx, CancellationToken ct)
    {
        NativeAudio.SetMasterVolume(0.5f);
        return Task.FromResult(new StepResult(true, "Звук восстановлен (50%)"));
    }
}
