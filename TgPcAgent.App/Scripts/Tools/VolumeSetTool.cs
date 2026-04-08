using System.Runtime.InteropServices;

namespace TgPcAgent.App.Scripts.Tools;

public sealed record VolumeSetParams(int Level);

public sealed class VolumeSetTool : ScriptToolBase<VolumeSetParams>
{
    public override string Name => "volume.set";
    public override string Description => "Установить громкость (0-100)";

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const int APPCOMMAND_VOLUME_UP = 0x0A;
    private const int APPCOMMAND_VOLUME_DOWN = 0x09;
    private const int WM_APPCOMMAND = 0x319;

    protected override Task<StepResult> ExecuteTypedAsync(VolumeSetParams p, ScriptContext ctx, CancellationToken ct)
    {
        var level = Math.Clamp(p.Level, 0, 100);
        NativeAudio.SetMasterVolume(level / 100f);
        return Task.FromResult(new StepResult(true, $"Громкость: {level}%"));
    }
}

internal static class NativeAudio
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorClass { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int NotImpl1(); int NotImpl2(); int NotImpl3(); int NotImpl4(); int NotImpl5(); int NotImpl6();
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);
    }

    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    public static void SetMasterVolume(float level)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
        enumerator.GetDefaultAudioEndpoint(0, 1, out var device);
        device.Activate(IID_IAudioEndpointVolume, 1, IntPtr.Zero, out var o);
        var volume = (IAudioEndpointVolume)o;
        var guid = Guid.Empty;
        volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref guid);
    }

    public static float GetMasterVolume()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
        enumerator.GetDefaultAudioEndpoint(0, 1, out var device);
        device.Activate(IID_IAudioEndpointVolume, 1, IntPtr.Zero, out var o);
        var volume = (IAudioEndpointVolume)o;
        volume.GetMasterVolumeLevelScalar(out var level);
        return level;
    }
}
