namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager()
    {
        ModSingleton<DHMOGameManager>.Instance = this;
        GeneralConfigurations.MeetingOptions.AppendConfiguration(CanUseMark);
        NebulaGameEnd.RegisterWinCondTip(Raven.Instance.RavenTeamWin!, () => ((ISpawnable)Raven.MyRole).IsSpawnable, "raven", null);
        NebulaGameEnd.RegisterWinCondTip(Pelican.Instance.PelicanTeamWin!, () => ((ISpawnable)Pelican.MyRole).IsSpawnable, "pelican", null);
    }
    protected override void OnInjected(Game container) => this.Register(container);

    static BoolConfiguration CanUseMark = NebulaAPI.Configurations.Configuration("options.meeting.canUseMark", true);

    private readonly static List<string> allowedPlayer =
        ["eggantique#7155", //Water
         "primebling#0938", //饭团
         "logospruce#7295", //Plana
         "snaggyfin#5132", //Exe
        ];

    void OnGameStart(GameStartEvent ev)
    {
        MarkRole = [];
        if (NebulaAPI.CurrentGame == null) return;
        if (GamePlayer.LocalPlayer == null) return;

        GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev =>
        {
            foreach (var player in GamePlayer.AllPlayers)
            {
                if (player.VanillaPlayer) player.VanillaPlayer.ResetForMeeting();
            }
        }, NebulaAPI.CurrentGame);

        var markButton = new ModAbilityButtonImpl(true, alwaysShow: true).Register(NebulaAPI.CurrentGame);
        markButton.SetSprite(Mark?.GetSprite()).SetLabel("mark");
        markButton.Visibility = _ => !GamePlayer.LocalPlayer.IsDead && AmongUsUtil.InMeeting && (CanUseMark || allowedPlayer.Contains(GamePlayer.LocalPlayer.VanillaPlayer.FriendCode));
        markButton.Availability = _ => !Minigame.Instance && MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Results && MeetingHud.Instance.state != MeetingHud.VoteStates.Proceeding && !AddonHelper.IsOutMeeting();
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

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game == null || local == null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new Vector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
        if (MeetingHud.Instance)
        {
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.CheckCurrentAction();
            buttonManager?.UpdatePlayerState();
        }    
    }
}