namespace TgPcAgent.Core.Commands;

public sealed class CallbackPayloadParser
{
    public CallbackPayload Parse(string? data)
    {
        var rawData = data?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawData))
        {
            return new CallbackPayload(CallbackActionType.Unknown, null, rawData);
        }

        if (rawData.StartsWith("confirm:", StringComparison.Ordinal))
        {
            return new CallbackPayload(CallbackActionType.Confirm, rawData["confirm:".Length..], rawData);
        }

        if (rawData.StartsWith("scanapps:page:", StringComparison.Ordinal))
        {
            return new CallbackPayload(CallbackActionType.ScanAppsPage, rawData["scanapps:page:".Length..], rawData);
        }

        if (rawData.StartsWith("scanapps:open:", StringComparison.Ordinal))
        {
            return new CallbackPayload(CallbackActionType.OpenApp, rawData["scanapps:open:".Length..], rawData);
        }

        if (rawData.StartsWith("auto:", StringComparison.Ordinal))
        {
            return new CallbackPayload(CallbackActionType.Automation, rawData["auto:".Length..], rawData);
        }

        return new CallbackPayload(CallbackActionType.Unknown, rawData, rawData);
    }
}
