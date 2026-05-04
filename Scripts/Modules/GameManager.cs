using static Nebula.Behavior.MeetingPlayerButtonManager;

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

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game == null || local == null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new Vector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
        if (MeetingHud.Instance)
            MeetingHud.Instance.StartCoroutine(CoResetMeetingPlayerIcon().WrapToIl2Cpp());
    }

    static IEnumerator CoResetMeetingPlayerIcon()
    {
        var nebulaGameManager = NebulaGameManager.Instance;
        var meetingHud = MeetingHud.Instance;
        if (nebulaGameManager is null || meetingHud is null) yield break;

        var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
        yield return buttonManager;

        if (buttonManager == null || buttonManager.allButtons == null) yield break;

        if (buttonManager.allButtons.Count > 0)
        {
            buttonManager.allButtons.Do(b =>
            {
                if (b != null && b.gameObject != null)
                    b.gameObject.Destroy();
            });
            buttonManager.allButtons.Clear();
        }

        var playerStates = meetingHud.playerStates;
        if (playerStates == null) yield break;

        foreach (var playerVoteArea in playerStates)
        {
            var player = nebulaGameManager.GetPlayer(playerVoteArea.TargetPlayerId);
            if (player == null) continue;

            if (playerVoteArea.Buttons == null) continue;
            GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
            if (template == null) continue;

            GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
            targetBox.name = "MeetingModButton";
            targetBox.transform.localPosition = MeetingHudExtension.VoteAreaPlayerIconPos;

            if (!targetBox.TryGetComponent<SpriteRenderer>(out var renderer))
                renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = null;

            if (!targetBox.TryGetComponent<PassiveButton>(out var button))
                button = targetBox.GetComponent<PassiveButton>();

            button.OnClick.RemoveAllListeners();
            button.OnMouseOver.RemoveAllListeners();
            button.OnMouseOut.RemoveAllListeners();

            var allActionsCount = buttonManager.allActions.Count;
            string leftClickText = Language.Translate("ui.meeting.leftClick");
            string rightClickText = allActionsCount > 1 ? "<br>" + Language.Translate("ui.meeting.rightClick") : string.Empty;
            string finalTip = leftClickText + rightClickText;

            Variable<MeetingPlayerButtonState> stateRef = new();
            MeetingPlayerButton myRecord = new(targetBox, renderer, player, stateRef);
            stateRef.Value = new MeetingPlayerButtonState { MyButton = myRecord };
            buttonManager.allButtons.Add(myRecord);

            button.OnClick.AddListener(() => buttonManager.DoClick(stateRef.Value));
            button.OnMouseOver.AddListener(() =>
            {
                NebulaManager.Instance.SetHelpWidget(button, new NoSGUIText(
                    Virial.Media.GUIAlignment.Left,
                    NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.OverlayContent),
                    new RawTextComponent(finalTip)
                ));
            });
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));

            if (!targetBox.TryGetComponent<ExtraPassiveBehaviour>(out var epb))
                epb = targetBox.AddComponent<ExtraPassiveBehaviour>();
            epb.OnRightClicked = buttonManager.IncrementCurrentAction;
        }
    }
}