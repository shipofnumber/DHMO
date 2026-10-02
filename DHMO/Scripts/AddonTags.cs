using Virial.Runtime;

namespace DHMO;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public static class AddonGameTags
{
    public static CommunicableTextTag Digestion { get; private set; } = null!;
    public static CommunicableTextTag Execution { get; private set; } = null!;
    public static CommunicableTextTag Explosion { get; private set; } = null!;
    public static CommunicableTextTag Missing { get; private set; } = null!;

    public static RoleTeam JournalistTeam { get; private set; } = null!;
    public static RoleTeam PelicanTeam { get; private set; } = null!;
    public static RoleTeam RavenTeam { get; private set; } = null!;

    public static GameEnd JournalistEnd { get; private set; } = null!;
    public static GameEnd LoveTriangleEnd { get; private set; } = null!;
    public static GameEnd PelicanEnd { get; private set; } = null!;
    public static GameEnd RavenEnd { get; private set; } = null!;
    
    internal static CommunicableTextTag JournalistWinDetail = null!;
    internal static CommunicableTextTag LoveTriangleWinDetail = null!;
    internal static CommunicableTextTag LoveTriangleBlockedDetail = null!;
    internal static CommunicableTextTag PelicanHuntOverWinDetail = null!;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        Digestion = preprocessor.RegisterCommunicableText("state.digestion");
        Execution = preprocessor.RegisterCommunicableText("state.execution");
        Explosion = preprocessor.RegisterCommunicableText("state.bomb.explosion");
        Missing = preprocessor.RegisterCommunicableText("state.missing");

        JournalistTeam = preprocessor.CreateTeam("teams.journalist", new(218, 165, 32), TeamRevealType.OnlyMe);
        PelicanTeam = preprocessor.CreateTeam("teams.pelican", new(0, 153, 76), TeamRevealType.OnlyMe);
        RavenTeam = preprocessor.CreateTeam("teams.raven", new(49, 36, 82), TeamRevealType.OnlyMe);

        JournalistEnd = preprocessor.CreateEnd("journalist", JournalistTeam.Color);
        LoveTriangleEnd = preprocessor.CreateEnd("loveTriangle", new(255, 0, 184), 19);
        PelicanEnd = preprocessor.CreateEnd("pelican", PelicanTeam.Color);
        RavenEnd = preprocessor.CreateEnd("raven", RavenTeam.Color);
        
        JournalistWinDetail = preprocessor.RegisterCommunicableText("end.detail.journalist.win");
        LoveTriangleWinDetail = preprocessor.RegisterCommunicableText("end.detail.loveTriangle.win");
        LoveTriangleBlockedDetail = preprocessor.RegisterCommunicableText("end.detail.loveTriangle.blocked");
        PelicanHuntOverWinDetail = preprocessor.RegisterCommunicableText("end.detail.pelican.huntOverWin");
    }
}
