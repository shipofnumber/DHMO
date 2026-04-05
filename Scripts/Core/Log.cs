namespace DHMO.Core;

public class DLog
{
    public static void Log(object? message)
    {
        var log = NebulaAPI.Logging.NebulaLogger("DHMO");
        var text = message?.ToString() ?? "null";

        log.Message(text);
    }
}
