namespace TgPcAgent.App.Services;

internal sealed class FirstRunExperiencePolicy
{
    public bool ShouldOpenSettings(bool configExists)
    {
        return !configExists;
    }
}
