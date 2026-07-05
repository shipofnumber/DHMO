using DHMO.Roles.Abilities;
using Virial.Runtime;

namespace DHMO;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class DHMO
{
    internal static Harmony? harmony = new("DHMO");

    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;

        int val = 0;
        foreach (var addon in NebulaAddon.AllAddons)
            if (addon.NeedHandshake) val ^= AddonHandshakeHashPatch.CalculateAddonHash(addon);

        AddonHandshakeHashPatch.AddonHandshakeHash = val;
    }

    static DHMO()
    {
        harmony?.PatchAll();
        GeneralConfigurations.MeetingOptions.AppendConfiguration(RoleMarkAbility.CanUseMark);
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