namespace DHMO.Roles;

public class LucidDreamer : DefinedSingleAbilityRoleTemplate<LucidDreamer.Ability>, DefinedRole
{
    private LucidDreamer() : base("lucidDreamer", new(176, 175, 255), RoleCategory.CrewmateRole, Crewmate.MyTeam, [NumOfLeaveOption, LeavingDuration, CanLeaveMultiple, MaxLeftVotingTimeForLeaving, CanDoTaskDuringLeaving, NumOfCanCompleteTasks]) 
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder?.Illustration = NebulaAPI.AddonAsset.GetResource("LucidDreamerImage.png")?.AsImage(300f);
    }
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfLeaveOption));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static internal readonly IntegerConfiguration NumOfLeaveOption = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfLeave", (1, 15), 3);
    static internal readonly FloatConfiguration LeavingDuration = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.leavingDuration", (0f, 120f, 5f), 30f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration CanLeaveMultiple = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.canLeaveMultiple", true);
    static internal readonly FloatConfiguration MaxLeftVotingTimeForLeaving = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.maxLeftVotingTimeForLeaving", (0f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration CanDoTaskDuringLeaving = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.canDoTaskDuringLeaving", true);
    static internal readonly IntegerConfiguration NumOfCanCompleteTasks = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfCanCompleteTasks", (1, 5), 2, () => CanDoTaskDuringLeaving);

    static public readonly LucidDreamer MyRole = new();
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("LucidDreamerIcon.png")?.AsImage(80f);
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        TimerImpl? leavingTime = null;
        private int leftLeave = NumOfLeaveOption;
        public int completedTasks = NumOfCanCompleteTasks;
        bool canLeave;

        private GameObject? gameObject = null;
        private Coroutine? coroutine;
        public Ability(GamePlayer player, bool isUsurped, int leave) : base(player, isUsurped)
        {
            leftLeave = leave;
            gameObject = null;
            if (AmOwner)
            {
                leavingTime = new TimerImpl(LeavingDuration).Register(this);

                if (CanDoTaskDuringLeaving)
                {
                    var modUseButton = new ModAbilityButtonImpl(alwaysShow: true).Register(this).KeyBind(NebulaInput.GetInput(VirtualKeyInput.Use));
                    modUseButton.Visibility = _ => !MyPlayer.IsDead && AddonHelper.IsOutMeeting() && coroutine == null;
                    modUseButton.Availability = _ => MyPlayer.CanMove && coroutine == null && !AmongUsUtil.MapIsOpen && MyPlayer.VanillaPlayer.closest != null && completedTasks > 0;
                    modUseButton.OnClick = _ =>
                    {
                        if (MyPlayer.VanillaPlayer.closest != null)
                        {
                            MyPlayer.VanillaPlayer.UseClosest();
                        }
                    };
                    modUseButton.OnUpdate = _ =>
                    {
                        ImageNames imageNames = HudManager.Instance.UseButton.currentTarget.UseIcon;
                        if (!HudManager.Instance.UseButton.fastUseSettings.ContainsKey(imageNames) || MyPlayer.VanillaPlayer.closest == null)
                        {
                            imageNames = ImageNames.UseButton;
                        }
                        var settings = HudManager.Instance.UseButton.fastUseSettings[imageNames];
                        modUseButton.SetSprite(settings.Image);
                        modUseButton.VanillaButton.graphic.SetCooldownNormalizedUvs();
                        modUseButton.VanillaButton.buttonLabelText.fontSharedMaterial = settings.FontMaterial;
                        modUseButton.VanillaButton.buttonLabelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(settings.Text, []);
                    };
                }

                var meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetColorLabel(MyRole.RoleColor);
                meetingButton.Visibility = _ => !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                meetingButton.Availability = _ => leftLeave > 0 && AddonHelper.ModAbilityMeetingButton() && coroutine == null && (CanLeaveMultiple || canLeave);
                meetingButton.SetAsUsurpableButton(this);
                meetingButton.SetUsesIcon(MyRole.RoleColor, leftLeave.ToString(), out _ , out var usesText);

                meetingButton.SetUsesIcon(MyRole.RoleColor, Mathn.CeilToInt(leavingTime?.CurrentTime ?? 0f).ToString(), out var timeObject, out var timeText, true);

                gameObject = timeObject; 
                timeObject.SetActive(false);

                meetingButton.OnClick = _ =>
                {
                    if (AddonHelper.IsOutMeeting())
                        LeaveOrJoinMeeting(false, usesText);
                    else
                        LeaveOrJoinMeeting(true, usesText);
                };
                meetingButton.OnUpdate = _ =>
                {
                    if (AddonHelper.IsOutMeeting())
                    {
                        timeText.SetText(Mathn.CeilToInt(leavingTime?.CurrentTime ?? 0f).ToString());
                        if (leavingTime != null)
                            if (((MeetingHudExtension.VotingTimer <= MaxLeftVotingTimeForLeaving) || !leavingTime.IsProgressing) && coroutine == null)
                            {
                                LeaveOrJoinMeeting(false, usesText);
                            }
                    }
                    meetingButton.SetLabel(AddonHelper.IsOutMeeting() ? "lucidDreamer.returnmeeting" : "lucidDreamer.leavemeeting");
                };

                if (NebulaAPI.CurrentGame is { } currentGame)
                {
                    GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
                    {
                        if (ev.Player == MyPlayer && AddonHelper.IsOutMeeting())
                            LeaveOrJoinMeeting(false, usesText);
                    }, currentGame);
                }
            }
        }

        void LeaveOrJoinMeeting(bool isLeaving, TextMeshPro text)
        {
            if (!isLeaving)
            {
                --leftLeave;
                text.SetText(leftLeave.ToString());
            }
            if (coroutine != null) return;
            coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(isLeaving).WrapToIl2Cpp());
        }

        private IEnumerator CoLeaveOrJoinMeeting(bool isLeaving)
        {
            if (Minigame.Instance)
                Minigame.Instance.CloseInternal();
            if (AmongUsUtil.MapIsOpen)
                MapBehaviour.Instance.Close();
            if (DestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen)
                DestroyableSingleton<HudManager>.Instance.GameMenu.Close();

            yield return HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new Vector3(isLeaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isLeaving;

            if (isLeaving)
            {
                leavingTime?.Start();
                gameObject?.SetActive(true);
            }
            else
            {
                if (!CanLeaveMultiple)
                    canLeave = false;

                leavingTime?.Pause();
                gameObject?.SetActive(false);
                leavingTime?.Reset();
            }

            yield return HudManager.Instance?.CoFadeFullScreen(Color.black, Color.clear, 1f, false);
            coroutine = null;
        }

        [OnlyMyPlayer]
        void OnPlayerStepSound(PlayerCheckPlayFootSoundEvent ev)
        {
            if (AddonHelper.IsOutMeeting())
                ev.PlayFootSound = false;
        }

        [Local]
        void OnMeetingPreStart(MeetingPreStartEvent _)
        {
            if (!CanLeaveMultiple)
                canLeave = true;

            completedTasks = NumOfCanCompleteTasks;
        }

        [OnlyMyPlayer]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent _)
        {
            if (AddonHelper.IsOutMeeting() && !MyPlayer.IsDead)
                --completedTasks;
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && !MyPlayer.IsDead)
                ev.UpdateSaturation(0.1f, true);
        }

        bool IPlayerAbility.EyesightIgnoreWalls => AddonHelper.IsOutMeeting() && MyPlayer.IsAlive;
    }
}