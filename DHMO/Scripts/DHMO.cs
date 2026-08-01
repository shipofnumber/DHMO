using DHMO.Roles.Abilities;
using Virial.Runtime;

namespace DHMO;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class DHMO
{
    internal static Harmony? harmony = new("DHMO");

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        NebulaGameEnd.RegisterWinCondTip(Raven.Instance.RavenTeamWin!, () => ((ISpawnable)Raven.MyRole).CanSpawnInCurrentGame, "raven", null);
        NebulaGameEnd.RegisterWinCondTip(Pelican.Instance.PelicanTeamWin!, () => ((ISpawnable)Pelican.MyRole).CanSpawnInCurrentGame, "pelican.devour", null);
        NebulaGameEnd.RegisterWinCondTip(Pelican.Instance.PelicanTeamWin!, () => ((ISpawnable)Pelican.MyRole).CanSpawnInCurrentGame && Pelican.InvokePelicanTime, "pelican.timeOver", null);
    }

    static DHMO()
    {
        harmony?.PatchAll();
        GeneralConfigurations.MeetingOptions.AppendConfiguration(RoleMarkAbility.CanUseMark);
    }
}

public class DLog
{
    public static void Log(object message)
    {
        var log = NebulaAPI.Logging.NebulaLogger("DHMO");
        var text = message?.ToString() ?? string.Empty;
        log.Message(text);
    }
}