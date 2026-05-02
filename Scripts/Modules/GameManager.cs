using Virial.Runtime;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;
    protected override void OnInjected(Game container) => this.Register(container);
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;
        GeneralConfigurations.MeetingOptions.AppendConfiguration(CanUseMark);
        NebulaGameEnd.RegisterWinCondTip(Raven.Instance.RavenTeamWin, () => ((ISpawnable)Raven.MyRole).IsSpawnable, "raven", null);
    }

    static BoolConfiguration CanUseMark = NebulaAPI.Configurations.Configuration("options.meeting.canUseMark", true);
    private readonly static List<string> allowedPlayer =
        ["eggantique#7155", //Water
         "primebling#0938", //饭团
         "logospruce#7295", //Plana
         "snaggyfin#5132", //Exe
        ];

    void OnGameStart(GameStartEvent _)
    {
        MarkRole = [];
        if (NebulaAPI.CurrentGame == null) return;
        if (GamePlayer.LocalPlayer == null) return;
        var markButton = new ModAbilityButtonImpl(true, alwaysShow: true).Register(NebulaAPI.CurrentGame);
        markButton.SetSprite(Mark?.GetSprite()).SetLabel("mark");
        markButton.Visibility = _ => !GamePlayer.LocalPlayer.IsDead && AmongUsUtil.InMeeting && (CanUseMark || allowedPlayer.Contains(GamePlayer.LocalPlayer.VanillaPlayer.FriendCode));
        markButton.Availability = _ => !Minigame.Instance && MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Results && MeetingHud.Instance.state != MeetingHud.VoteStates.Proceeding && !Raven.Instance.IsOutMeeting();
        markButton.OnClick = _ =>
        {
            RoleMarkMenu.Open(null, selectedPlayer =>
            {
                LastMarkWindow = RoleMarkWindow.OpenRoleSelectWindow(Nebula.Roles.Roles.AllRoles.Where(r => r.ShowOnHelpScreen && r.ShowOnFreeplayScreen), null, true, string.Empty,
                    role =>
                    {
                        if (selectedPlayer != null && MarkRole != null)
                        {
                            MarkRole[selectedPlayer.ToGamePlayer().PlayerId] = role;
                            if (LastMarkWindow) LastMarkWindow?.CloseScreen();
                            LastMarkWindow = null;
                        }
                    });
            }, (roleText, player) =>
            {
                if (MarkRole != null && player != null)
                {
                    roleText.SetText(MarkRole.TryGetValue(player.ToGamePlayer().PlayerId, out var role) ? $"{role.GetRoleIconTag()}{role.DisplayColoredName}" : string.Empty);
                }
            });
        };
    }

    static readonly Image? Mark = NebulaAPI.AddonAsset?.GetResource("mark.png")?.AsImage();

    public static MetaScreen? LastMarkWindow = null;

    public static Dictionary<byte, DefinedAssignable>? MarkRole = [];

    void OnRoleChanged(PlayerRoleSetEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game == null || local == null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player).StartOnScene());
    }
}