namespace DHMO.Roles.Crewmate;

public class LucidDreamer : DefinedSingleAbilityRoleTemplate<LucidDreamer.Ability>, DefinedRole, HasCitation, IAssignableDocument
{
    private LucidDreamer() : base("lucidDreamer", new(176, 175, 255), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [NumOfLeaveOption, LeavingDuration, CanLeaveMultiple, MaxLeftVotingTimeForLeaving, NumOfCanCompleteTasks])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder?.Illustration = NebulaAPI.AddonAsset.GetResource("Image/LucidDreamerImage.png")?.AsImage(300f);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfLeaveOption));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static internal readonly IntegerConfiguration NumOfLeaveOption = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfLeave", (1, 15), 3);
    static internal readonly FloatConfiguration LeavingDuration = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.leavingDuration", (0f, 120f, 5f), 30f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration CanLeaveMultiple = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.canLeaveMultiple", true);
    static internal readonly FloatConfiguration MaxLeftVotingTimeForLeaving = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.maxLeftVotingTimeForLeaving", (0f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static internal readonly IntegerConfiguration NumOfCanCompleteTasks = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfCanCompleteTasks", (1, 5), 2);

    static public readonly LucidDreamer MyRole = new();
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield return new(buttonImage!, "role.lucidDreamer.ability.leave"); }
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%MAXLEAVINGTIME%", MaxLeftVotingTimeForLeaving.GetValue().ToString());
        yield return new("%NUM%", NumOfCanCompleteTasks.GetValue().ToString());
    }

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/LucidDreamerIcon.png")?.AsImage(80f);
    private static Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("Button/LucidDreamerMeetingButton.png")?.AsImage();
    public Citation? Citation => DHMOCitations.GGD;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        TimerImpl? leavingTime;
        private int leftLeave = NumOfLeaveOption;
        public int canCompleteTasks = NumOfCanCompleteTasks;
        bool canLeave;
        private GameObject? gameObject;
        private Coroutine? coroutine;

        public Ability(GamePlayer player, bool isUsurped, int leave) : base(player, isUsurped)
        {
            leftLeave = leave;
            GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
            {
                if (ev.Player == MyPlayer && AddonHelper.IsOutMeeting())
                    NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
            }, this);
            GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev => RpcCamouflage.Invoke((MyPlayer, false)), this);
            GameOperatorManager.Instance?.RegisterOnReleased(() =>
            {
                if (AddonHelper.IsOutMeeting())
                    NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
            }, this);

            if (!AmOwner) return;
            leavingTime = new TimerImpl(LeavingDuration).Register(this);

            var meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                .BindKey(VirtualKeyInput.SidekickAction).SetColorLabel(MyRole.RoleColor).SetImage(buttonImage!);

            meetingButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting;
            meetingButton.Availability = _ => leftLeave > 0 && AddonHelper.ModAbilityMeetingButton() && coroutine == null && (CanLeaveMultiple || canLeave) && MeetingHudExtension.VotingTimer > MaxLeftVotingTimeForLeaving;
            meetingButton.SetAsUsurpableButton(this);

            meetingButton.SetUsesIcon(MyRole.Color, leftLeave.ToString(), out _, out var usesText);
            meetingButton.SetUsesIcon(MyRole.Color, Mathn.CeilToInt(leavingTime?.CurrentTime ?? 0f).ToString(), out gameObject, out var timeText, true);
            gameObject.SetActive(false);

            meetingButton.OnClick = _ => LeaveOrJoinMeeting(!AddonHelper.IsOutMeeting(), usesText);
            meetingButton.OnUpdate = _ =>
            {
                if (AddonHelper.IsOutMeeting())
                {
                    timeText.SetText(Mathn.CeilToInt(leavingTime?.CurrentTime ?? 0f).ToString());
                    if (leavingTime != null && (MeetingHudExtension.VotingTimer <= MaxLeftVotingTimeForLeaving || !leavingTime.IsProgressing || canCompleteTasks <= 0) && coroutine == null)
                        LeaveOrJoinMeeting(false, usesText);
                }
                meetingButton.SetLabel(AddonHelper.IsOutMeeting() ? "lucidDreamer.returnmeeting" : "lucidDreamer.leavemeeting");
            };
        }

        void LeaveOrJoinMeeting(bool isLeaving, TextMeshPro text)
        {
            if (!isLeaving)
            {
                --leftLeave;
                text.text = leftLeave.ToString();
            }
            if (coroutine != null) return;
            coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(isLeaving).WrapToIl2Cpp());
        }

        private IEnumerator CoLeaveOrJoinMeeting(bool isLeaving)
        {
            AmongUsLLImpl.TryGetHudManager(out var hud);
            if (Minigame.Instance.AsBoolFast()) Minigame.Instance.ForceClose();
            if (AmongUsUtil.MapIsOpen) MapBehaviour.Instance.Close();
            if (hud.GameMenu.IsOpen) hud.GameMenu.Close();

            yield return hud.CoFadeFullScreen(VColor.Clear, VColor.Black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new VVector3(isLeaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isLeaving;

            if (isLeaving)
            {
                leavingTime?.Reset().Start(); 
                gameObject?.SetActive(true);
            }
            else
            {
                if (!CanLeaveMultiple)
                    canLeave = false;

                leavingTime?.Pause().Reset();
                gameObject?.SetActive(false);
            }

            yield return hud.CoFadeFullScreen(VColor.Black, VColor.Clear, 1f, false);
            coroutine = null;
        }

        [OnlyMyPlayer]
        void OnPlayerStepSound(PlayerCheckPlayFootSoundEvent ev) => ev.PlayFootSound &= !AddonHelper.IsOutMeeting();

        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            RpcCamouflage.Invoke((MyPlayer, true));
            if (!MyPlayer.AmOwner) return;
            if (!CanLeaveMultiple)
                canLeave = true;

            canCompleteTasks = NumOfCanCompleteTasks;
        }

        [Local, OnlyMyPlayer]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && MyPlayer.IsAlive) --canCompleteTasks;
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && MyPlayer.IsAlive) ev.UpdateSaturation(0f, true);
        }

        bool IPlayerAbility.EyesightIgnoreWalls => AddonHelper.IsOutMeeting() && MyPlayer.IsAlive;

        public static RemoteProcess<(GamePlayer player, bool on)> RpcCamouflage = new("LucidDreamerCamouflage", (message, _) =>
        {
            if (NebulaGameManager.Instance is null || GamePlayer.LocalPlayer is null) return;
            if (message.player.AmOwner || GamePlayer.LocalPlayer.Role is not Raven.Instance) return;

            var tag = $"LucidDreamer{GamePlayer.LocalPlayer.PlayerId}";
            if (message.on)
                message.player?.Unbox().AddOutfit(new OutfitCandidate(NebulaGameManager.Instance.UnknownOutfit, tag, 50, true));
            else
                message.player?.Unbox().RemoveOutfit(tag);
        });
    }
}