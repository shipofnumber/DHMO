namespace DHMO;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class DHMO
{
    internal static Harmony? harmony = new("DHMO");
    static DHMO() => harmony?.PatchAll();
}

public class DLog
{
    public static void Log(object? message)
    {
        var log = NebulaAPI.Logging.NebulaLogger("DHMO");
        var text = message?.ToString() ?? string.Empty;

        log.Message(text);
    }
}