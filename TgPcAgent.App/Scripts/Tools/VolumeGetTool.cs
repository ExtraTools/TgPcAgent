namespace TgPcAgent.App.Scripts.Tools;

public sealed record VolumeGetParams;

public sealed class VolumeGetTool : ScriptToolBase<VolumeGetParams>
{
    public override string Name => "volume.get";
    public override string Description => "Текущая громкость";

    protected override Task<StepResult> ExecuteTypedAsync(VolumeGetParams p, ScriptContext ctx, CancellationToken ct)
    {
        var level = (int)(NativeAudio.GetMasterVolume() * 100);
        return Task.FromResult(new StepResult(true, $"Громкость: {level}%"));
    }
}
