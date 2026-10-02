using Image = Virial.Media.Image;
using Object = UnityEngine.Object;

namespace DHMO.Roles.Impostor;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class JudgeMeetingManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static JudgeMeetingManager() => DIManager.Instance.RegisterModule(() => new JudgeMeetingManager());
    public JudgeMeetingManager() => ModSingleton<JudgeMeetingManager>.Instance = this;

    protected override void OnInjected(Game container) => this.Register(container);
    
    public bool IsAdjournment { get; private set; }
    public bool IsSpecialMeeting { get; private set; }
    public float LastMeetingDiscussionTime { get; internal set; }
    public float LastMeetingVotingTime { get; internal set; }
    
    public GamePlayer? Reporter { get; private set; }
    public GamePlayer? Reported { get; private set; }
    
    private TimerImpl? timer = null;
    public GamePlayer? WillSacrificialExile { get; internal set; }
    
    public void MarkAsAdjournment(bool value = true) => IsAdjournment = value;
    public void MarkAsSpecialMeeting(bool value = true) => IsSpecialMeeting = value;
    
    void OnMeetingPreStart(MeetingPreStartEvent ev)
    {
        if (IsAdjournment) return;

        Reporter = ev.Reporter;
        Reported = ev.Reported;
    }

    [EventPriority(EventPriority.Low)]
    void FixExileText(FixExileTextEvent ev)
    {
        if (WillSacrificialExile != null)
        {
            if (ExileController.Instance.AsBoolFast(out var exileController))
            {
                exileController.ImpostorText.gameObject.SetActive(false);
            }
            
            ev.SetExileText(Language.Translate("role.evilJudge.sacrificialExileText").Replace("%PLAYER%", WillSacrificialExile.Name));
        }
    }
    
    void CheckCanPushEmergencyButton(CheckCanPushEmergencyButtonEvent ev)
    {
        if (ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment && !MeetingHud.Instance.AsBoolFast())
            ev.DenyButton("role.evilJudge.presentPushEmergencyMeetingText");
    }
    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        WillSacrificialExile = null;
        
        if (!IsAdjournment) return;
        timer ??= new TimerImpl(EvilJudge.ReopenCountdownOption).RegisterPermanently().SetPredicate(() => !AmongUsUtil.InMeeting).Start();
    }
    
    void AppendTaskPanel(PlayerTaskTextLocalEvent ev)
    {
        if (!IsAdjournment || timer == null) return;

        if (timer.CurrentTime <= 0f)
        {
            if (Reporter == null || Reporter.IsDead || !(Reported?.IsDead ?? true))
            {
                MarkAsAdjournment(false);
                timer.Release();
                timer = null;
                return;
            }

            if (!MeetingHud.Instance.AsBoolFast())
            {
                if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
                {
                    NebulaManager.Instance.StartCoroutine(CoCallEmergencyMeeting(Reporter.VanillaPlayer).WrapToIl2Cpp());
                    if (Reporter.AmOwner)
                    {
                        Reporter.VanillaPlayer.RemainingEmergencies++;
                    }
                }

                timer.Release();
                timer = null;
                MarkAsSpecialMeeting();

                return;
            }
        }

        ev.AppendText(Language.Translate("role.evilJudge.reopenText").Replace("%TIME%", Mathn.CeilToInt(timer.CurrentTime).ToString()).Color(VColor.ImpostorColor));
    }
    
    private static IEnumerator CoCallEmergencyMeeting(PlayerControl player)
    {
        yield return ManagedEffects.Wait(0.4f);
        if (MeetingHud.Instance.AsBoolFast()) yield break;
            
        player.CmdReportDeadBody(null);
    }

    void OnMeetingEnd(MeetingEndEvent ev)
    {
        if (!IsSpecialMeeting) return;
     
        LastMeetingDiscussionTime = 0f;
        LastMeetingVotingTime = 0f;
        
        IsAdjournment = false;
        IsSpecialMeeting = false;
    }
}

public class EvilJudge : DefinedSingleAbilityRoleTemplate<EvilJudge.Ability>, DefinedRole, IGuessed, IAssignableDocument
{
    private EvilJudge() : base("evilJudge", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, [NumOfAdjournOption, ReopenCountdownOption, RoleFilterOption])
    {
    }

    private static readonly IntegerConfiguration NumOfAdjournOption = NebulaAPI.Configurations.Configuration("options.role.evilJudge.numOfadjourn", (1, 5), 1);
    public static readonly FloatConfiguration ReopenCountdownOption = NebulaAPI.Configurations.Configuration("options.role.evilJudge.reopenCountdown", (20f, 180f, 5f), 30f, FloatConfigurationDecorator.Second);
    public static readonly SimpleRoleFilterConfiguration RoleFilterOption = new("options.role.evilJudge.sacrificialExileFilter")
    {
        RolePredicate = r => !r.IsSystemRole && r is not Jester,
        ScrollerTag = "evilJudgeFilter",
        InvertOption = true,
        PreviewOnlySpawnableRoles = true,
    };

    public bool HasAbility => true;
    public bool HasTips => true;

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/EvilJudgeIcon.png")?.AsImage(220f);

    public override Ability CreateAbility(Player player, int[] arguments) => new(player, arguments.Get(0, NumOfAdjournOption));
    public static readonly EvilJudge MyRole = new();

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerAbility, IGameOperator, IPlayerAbility
    {
        private int leftUses;

        private static Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/AdjournButton.png")?.AsImage(1100f);

        public Ability(GamePlayer player, int uses) : base(player)
        {
            leftUses = uses;

            if (AmOwner)
            {
                var adjournButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true);
                adjournButton.SetLabel("evilJudge.adjourn").SetLabelType(ModAbilityButton.LabelType.Impostor).BindKey(VirtualKeyInput.Ability).SetImage(buttonImage!);
                adjournButton.ShowUsesIcon(0, leftUses.ToString());
                adjournButton.Visibility = button => leftUses > 0 && !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                adjournButton.Availability = button => AmongUsUtil.InMeeting && APICompat.ModAbilityMeetingButton() && !ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment;
                adjournButton.OnClick = button =>
                {
                    RpcAdjourn.Invoke(MyPlayer);
                    leftUses--;
                    button.UpdateUsesIcon(leftUses.ToString());

                    NebulaAPI.CurrentGame?.CurrentMeeting?.EndVotingForcibly(false);
                };
            }
        }

        public static readonly RemoteProcess<GamePlayer> RpcAdjourn = new("EvilJudgeAdjourn", (player, _) =>
        {
            if (!MeetingHud.Instance.AsBoolFast(out var meetingHud)) return;
            MeetingHudExtension.ExileEvenIfTie = false;
            var gameObject = Object.Instantiate(meetingHud.judgeGavelPrefab, meetingHud.transform);

            var text = gameObject.transform.GetChild(0);
            text.GetComponent<TextTranslatorTMP>().Destroy();
            text.GetComponent<TextMeshPro>().text = Language.Translate("role.evilJudge.adjournText");

            var judgeMeetingManager = ModSingleton<JudgeMeetingManager>.Instance;
            judgeMeetingManager.MarkAsAdjournment();
            judgeMeetingManager.LastMeetingDiscussionTime = MeetingHudExtension.DiscussionTimer;
            judgeMeetingManager.LastMeetingVotingTime = MeetingHudExtension.VotingTimer;
        });
    }
}
