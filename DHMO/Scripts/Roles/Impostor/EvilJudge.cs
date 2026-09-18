using Image = Virial.Media.Image;
using Object = UnityEngine.Object;

namespace DHMO.Roles.Impostor;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class JudgeMeetingManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static JudgeMeetingManager() => DIManager.Instance.RegisterModule(() => new DGameManager());
    public JudgeMeetingManager() => ModSingleton<JudgeMeetingManager>.Instance = this;

    protected override void OnInjected(Game container) => this.Register(container);
    
    public bool IsAdjournment { get; private set; }
    public bool IsSacrificialExile { get; private set; }
    public bool IsSpecialMeeting { get; private set; }

    private GamePlayer? willSacrificialExile = null;
    
    public void MarkAsAdjournment(bool value = true) => IsAdjournment = value;
    public void MarkAsSpecialMeeting(bool value = true) => IsSpecialMeeting = value;
    
    void OnVoteDisclosed(MeetingVoteDisclosedEvent ev)
    {
        var judges = GamePlayer.AllPlayers.Where(p => p.Role.GetAbility<EvilJudge.Ability>() != null).ToArray();
        if (judges.Length == 0) return;
        
        var judgesId = judges.Select(j => j.PlayerId);
        
        var voteStates = ev.VoteStates.Where(v => v.SkippedVote && !judgesId.Contains(v.VoterId)).Select(v => GamePlayer.GetPlayer(v.VoterId)).NotNull().ToArray();
        if (voteStates.Length < GamePlayer.AllPlayers.Count(p => p.IsAlive) / 2) return;
        
        var skippedPlayer = voteStates.Where(p => !judges.Any(j => j.IsSameSideOf(p)) && EvilJudge.RoleFilterOption.Contains(p.Role.Role) && p.Role.Role is not Jester).ToArray();
        if (skippedPlayer.Length <= 0) return;

        IsSacrificialExile = true;
        willSacrificialExile = skippedPlayer[System.Random.Shared.Next(skippedPlayer.Length)];
    }

    [EventPriority(-100000)]
    void MeetingFixExile(MeetingFixExiledHostEvent ev)
    {
        if (!IsSacrificialExile) return;

        if (ev.WillBeExiled(willSacrificialExile) || ev.RawExiled != byte.MaxValue)
        {
            this.IsSacrificialExile = false;
        }
        else
        {
            ev.exiled = willSacrificialExile?.PlayerId ?? ev.exiled;
        }
    }

    void OnExileSceneStart(ExileSceneStartEvent ev)
    {
        if (!ExileController.Instance.AsBoolFast(out var exileController)) return;

        exileController.judgeText.GetComponent<TextTranslatorTMP>().Destroy();
        exileController.judgeText.text = Language.Translate("role.evilJudge.sacrificialExileText");
        exileController.judgeText.gameObject.SetActive(IsSacrificialExile);
    }

    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        IsSacrificialExile = false;
        willSacrificialExile = null;
    }

    void OnMeetingEnd(MeetingEndEvent ev)
    {
        if (!IsSpecialMeeting) return;
        
        IsAdjournment = false;
        IsSpecialMeeting = false;
    }
}

public class EvilJudge : DefinedSingleAbilityRoleTemplate<EvilJudge.Ability>, DefinedRole, IGuessed, IAssignableDocument
{
    private EvilJudge() : base("evilJudge", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, [AdjournCooldownOption, NumOfAdjournOption, ReopenCountdownOption, RoleFilterOption])
    {
    }

    public static readonly FloatConfiguration AdjournCooldownOption = NebulaAPI.Configurations.Configuration("options.role.evilJudge.adjournCooldown", (15f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
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

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/EvilJudgeIcon.png")?.AsImage(210f);

    public override Ability CreateAbility(Player player, int[] arguments) => new(player, arguments.Get(0, NumOfAdjournOption));
    public static readonly EvilJudge MyRole = new();

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerAbility, IGameOperator, IPlayerAbility
    {
        public GamePlayer? Reporter { get; private set; }
        public GamePlayer? Reported { get; private set; }

        private GameTimer? reopenTimer;
        private static GamePlayer? judge;
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
                adjournButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, AdjournCooldownOption);
                adjournButton.Visibility = button => leftUses > 0 && !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                adjournButton.OnMeeting = button => button.StartCoolDown();
                adjournButton.Availability = button => AmongUsUtil.InMeeting && MeetingHud.Instance.CurrentState is not MeetingHud.MeetingStates.Animating and not MeetingHud.MeetingStates.Results and not MeetingHud.MeetingStates.Proceeding && !ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment;
                adjournButton.OnClick = button =>
                {
                    RpcAdjourn.Invoke(MyPlayer);
                    leftUses--;
                    button.UpdateUsesIcon(leftUses.ToString());

                    NebulaAPI.CurrentGame?.CurrentMeeting?.EndVotingForcibly(false);
                };
            }
        }

        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            if (ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment) return;

            Reporter = ev.Reporter;
            Reported = ev.Reported;
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment && ModSingleton<JudgeMeetingManager>.Instance.IsSpecialMeeting && MyPlayer.IsSameSideOf(GamePlayer.LocalPlayer!))
            {
                GamePlayer.AllPlayers.Do(p => MeetingHudExtension.RemoveUsingAbilityMask(1 << p.PlayerId));
            }
        }

        void CheckCanPushEmergencyButton(CheckCanPushEmergencyButtonEvent ev)
        {
            if (ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment && !MeetingHud.Instance.AsBoolFast())
                ev.DenyButton("role.evilJudge.presentPushEmergencyMeetingText");
        }

        void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
        {
            if (!ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment) return;
            reopenTimer ??= new TimerImpl(ReopenCountdownOption).RegisterPermanently().SetPredicate(() => !AmongUsUtil.InMeeting).Start();
        }

        void AppendTaskPanel(PlayerTaskTextLocalEvent ev)
        {
            if (!ModSingleton<JudgeMeetingManager>.Instance.IsAdjournment || reopenTimer == null) return;

            if (judge == MyPlayer && reopenTimer.CurrentTime <= 0f)
            {
                if (Reporter == null || Reporter.IsDead || !(Reported?.IsDead ?? true))
                {
                    ModSingleton<JudgeMeetingManager>.Instance.MarkAsAdjournment(false);
                    reopenTimer.Release();
                    reopenTimer = null;
                    return;
                }

                if (!MeetingHud.Instance.AsBoolFast())
                {
                    if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
                        Reporter.RequestEmergencyMeeting(true, false);

                    reopenTimer.Release();
                    reopenTimer = null;
                    ModSingleton<JudgeMeetingManager>.Instance.MarkAsSpecialMeeting();

                    return;
                }
            }

            ev.AppendText(Language.Translate("role.evilJudge.reopenText").Replace("%TIME%", reopenTimer.TimerText).Color(VColor.ImpostorColor));
        }

        public static readonly RemoteProcess<GamePlayer> RpcAdjourn = new("EvilJudgeAdjourn", (player, _) =>
        {
            if (!MeetingHud.Instance.AsBoolFast(out var meetingHud)) return;
            MeetingHudExtension.ExileEvenIfTie = false;
            var gameObject = Object.Instantiate(meetingHud.judgeGavelPrefab, meetingHud.transform);

            judge = player;

            var text = gameObject.transform.GetChild(0);
            text.GetComponent<TextTranslatorTMP>().Destroy();
            text.GetComponent<TextMeshPro>().text = Language.Translate("role.evilJudge.adjournText");

            ModSingleton<JudgeMeetingManager>.Instance.MarkAsAdjournment();
        });
    }
}
