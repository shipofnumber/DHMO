using Cpp2IL.Core.Extensions;
using DHMO.Roles.Abilities;
using Virial.Runtime;

namespace DHMO;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class DHMO
{
    internal static Harmony? Harmony { get; } = new("DHMO");
    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        NebulaGameEnd.RegisterWinCondTip(AddonGameTags.JournalistEnd, () => ((ISpawnable)Journalist.MyRole).CanSpawnInCurrentGame, "journalist");
        NebulaGameEnd.RegisterWinCondTip(AddonGameTags.LoveTriangleEnd, () => ((ISpawnable)Lover.MyRole).CanSpawnInCurrentGame && !Lover.AvengerModeOption && DGameManager.TriangleModeOption, "loveTriangle");
        NebulaGameEnd.RegisterWinCondTip(AddonGameTags.PelicanEnd, () => ((ISpawnable)Pelican.MyRole).CanSpawnInCurrentGame, "pelican.devour");
        NebulaGameEnd.RegisterWinCondTip(AddonGameTags.PelicanEnd, () => ((ISpawnable)Pelican.MyRole).CanSpawnInCurrentGame && Pelican.InvokePelicanHunt, "pelican.huntOver");
        NebulaGameEnd.RegisterWinCondTip(AddonGameTags.RavenEnd, () => ((ISpawnable)Raven.MyRole).CanSpawnInCurrentGame, "raven");
    }

    static DHMO()
    {
        Harmony?.PatchAll();
        GeneralConfigurations.MeetingOptions.AppendConfiguration(RoleMarkAbility.CanUseMark);
    }
}

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public static class DHMOAsset
{
    public static AssetBundle AssetBundle { get; set; } = null!;

    public static AudioClip ChildSingingScaryMelodyMusic { get; set; } = null!;
    public static AudioClip DuckIntroSound { get; set; } = null!;
    public static AudioClip PelicanIntroSound { get; set; } = null!;
    public static AudioClip RavenIntroSound { get; set; } = null!;
    public static AudioClip SillyGooseIntroSound { get; set; } = null!;
    public static AudioClip VultureIntroSound { get; set; } = null!;

    private static T Load<T>(string path) where T : UnityEngine.Object => AssetBundle.LoadAsset<T>(path).MarkDontUnload();
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;

        var stream = NebulaAPI.AddonAsset.GetResource("Asset/dhmo_asset")?.AsStream();
        if (stream == null) yield break;

        AssetBundle = AssetBundle.LoadFromMemory(stream.ReadBytes());
        ChildSingingScaryMelodyMusic = Load<AudioClip>("ChildSingingScaryMelody.wav");
        DuckIntroSound = Load<AudioClip>("duckintro.wav");
        PelicanIntroSound = Load<AudioClip>("pelicanIntroSound.wav");
        RavenIntroSound = Load<AudioClip>("ravenIntroSound.wav");
        SillyGooseIntroSound = Load<AudioClip>("sillyGooseIntroSound.wav");
        VultureIntroSound = Load<AudioClip>("vultureIntro.wav");
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