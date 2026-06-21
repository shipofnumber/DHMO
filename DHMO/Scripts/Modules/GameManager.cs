namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;
    protected override void OnInjected(Game container) => this.Register(container);

    public static BoolConfiguration CanUseMark = NebulaAPI.Configurations.Configuration("options.meeting.canUseMark", true);

    void OnGameStart(GameStartEvent ev)
    {
        MarkRole = [];
        if (NebulaAPI.CurrentGame == null) return;
        if (GamePlayer.LocalPlayer == null) return;

        var markButton = new ModAbilityButtonImpl(true, alwaysShow: true).Register(NebulaAPI.CurrentGame);
        markButton.SetSprite(Mark?.GetSprite()).SetLabel("mark");
        markButton.Visibility = _ => GamePlayer.LocalPlayer.IsAlive && AmongUsUtil.InMeeting && CanUseMark;
        markButton.Availability = _ => !Minigame.Instance && MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Results && MeetingHud.Instance.state != MeetingHud.VoteStates.Proceeding && !AddonHelper.IsOutMeeting();
        markButton.OnClick = _ =>
        {
            RoleMarkMenu.Open(selectedPlayer =>
            {
                LastMarkWindow = RoleMarkWindow.OpenRoleSelectWindow(Nebula.Roles.Roles.AllRoles.Where(r => r.ShowOnHelpScreen && r.ShowOnFreeplayScreen), null, true, string.Empty,
                    role =>
                    {
                        if (selectedPlayer != null && MarkRole != null)
                        {
                            MarkRole[selectedPlayer.PlayerId] = role;
                            if (LastMarkWindow) LastMarkWindow?.CloseScreen();
                            LastMarkWindow = null;
                        }
                    });
            }, (roleText, player) =>
            {
                if (MarkRole != null && player != null)
                {
                    roleText.SetText(MarkRole.TryGetValue(player.PlayerId, out var role) ? $"{role.GetRoleIconTag()}{role.DisplayColoredName}" : string.Empty);
                }
            });
        };
    }

    static readonly Image? Mark = NebulaAPI.AddonAsset?.GetResource("Button/MarkButton.png")?.AsImage();

    public static MetaScreen? LastMarkWindow = null;

    public static Dictionary<byte, DefinedAssignable>? MarkRole = [];

    void OnMeetingPreEnd(MeetingVoteEndEvent ev)
    {
        if (NebulaAPI.CurrentGame is null) return;
        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player.VanillaPlayer) MeetingStartPatch.ModResetForMeeting(player.VanillaPlayer);
        }
    }

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game is null || local is null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new Vector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
    }
}