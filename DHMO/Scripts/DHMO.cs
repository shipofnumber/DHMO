using Nebula.Scripts;
using Virial.Runtime;

namespace DHMO;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class DHMO
{
    internal static Harmony? harmony = new("DHMO");

    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;
        CertifiedPatch.addonsList = NebulaAddon.AllAddons.Where(a => a.NeedHandshake).Select(a => new CertifiedPatch.AddonInfo(a)).ToList();
    }

    static DHMO()
    {
        harmony?.PatchAll();
        GeneralConfigurations.MeetingOptions.AppendConfiguration(DHMOGameManager.CanUseMark);
        NebulaGameEnd.RegisterWinCondTip(Raven.Instance.RavenTeamWin!, () => ((ISpawnable)Raven.MyRole).CanSpawnInCurrentGame, "raven", null);
        NebulaGameEnd.RegisterWinCondTip(Pelican.Instance.PelicanTeamWin!, () => ((ISpawnable)Pelican.MyRole).CanSpawnInCurrentGame, "pelican", null);
    }

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