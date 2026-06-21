namespace DHMO.Roles.Crewmate;

public class LucidDreamer : DefinedSingleAbilityRoleTemplate<LucidDreamer.Ability>, DefinedRole, HasCitation, IAssignableDocument
{
    private LucidDreamer() : base("lucidDreamer", new(176, 175, 255), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam, [NumOfLeaveOption, LeavingDuration, CanLeaveMultiple, MaxLeftVotingTimeForLeaving, NumOfCanCompleteTasks]) 
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

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonImage!, "role.lucidDreamer.ability.leave");
    }

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
        TimerImpl? leavingTime = null;
        private int leftLeave = NumOfLeaveOption;
        public int canCompleteTasks = NumOfCanCompleteTasks;
        bool canLeave;

        private GameObject? gameObject = null;
        private Coroutine? coroutine;
        public Ability(GamePlayer player, bool isUsurped, int leave) : base(player, isUsurped)
        {
            leftLeave = leave;
            gameObject = null;

            GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
            {
                if (ev.Player == MyPlayer && AddonHelper.IsOutMeeting())
                    NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
            }, this);
            GameOperatorManager.Instance?.Subscribe<MeetingPreStartEvent>(ev =>
            {
                RpcCamouflage.Invoke((MyPlayer, true));
            }, this);
            GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev =>
            {
                RpcCamouflage.Invoke((MyPlayer, false));
            }, this);
            GameOperatorManager.Instance?.RegisterOnReleased(() =>
            {
                if (AddonHelper.IsOutMeeting())
                    NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
            }, this);

            if (AmOwner)
            {
                leavingTime = new TimerImpl(LeavingDuration).Register(this);

                var modUseButton = new ModAbilityButtonImpl(alwaysShow: true).Register(this);
                modUseButton.Visibility = _ => MyPlayer.IsAlive && AddonHelper.IsOutMeeting();
                modUseButton.Availability = _ => MyPlayer.CanMove && MyPlayer.VanillaPlayer.closest != null && canCompleteTasks > 0 && coroutine == null;
                modUseButton.OnClick = _ =>
                {
                    if (MyPlayer.VanillaPlayer.closest != null)
                        MyPlayer.VanillaPlayer.UseClosest();
                };
                modUseButton.OnUpdate = _ =>
                {
                    if (NebulaAPI.CurrentGame is null || DestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings is null) return;
                    var vanillaUseButton = modUseButton.VanillaButton;
                    ImageNames imageNames = DestroyableSingleton<HudManager>.Instance.UseButton.currentTarget.UseIcon;
                    if (!DestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings.ContainsKey(imageNames) || MyPlayer.VanillaPlayer.closest == null)
                    {
                        imageNames = ImageNames.UseButton;
                    }
                    var settings = DestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings[imageNames];
                    if (settings != null)
                    {
                        modUseButton.SetSprite(settings.Image);
                        vanillaUseButton.graphic.SetCooldownNormalizedUvs();
                        vanillaUseButton.buttonLabelText.fontSharedMaterial = settings.FontMaterial;
                        vanillaUseButton.buttonLabelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(settings.Text, []);
                    }

#if PC
                    if (Input.GetKeyDown(AddonHelper.GetKeyCode(6)))
                        modUseButton.DoClick();
#endif
                };

                var meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetColorLabel(MyRole.RoleColor).SetImage(buttonImage!);
                meetingButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting;
                meetingButton.Availability = _ => leftLeave > 0 && AddonHelper.ModAbilityMeetingButton() && coroutine == null && (CanLeaveMultiple || canLeave) && MeetingHudExtension.VotingTimer > MaxLeftVotingTimeForLeaving;
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
                            if (((MeetingHudExtension.VotingTimer <= MaxLeftVotingTimeForLeaving) || !leavingTime.IsProgressing || canCompleteTasks <= 0) && coroutine == null)
                                LeaveOrJoinMeeting(false, usesText);
                    }
                    meetingButton.SetLabel(AddonHelper.IsOutMeeting() ? "lucidDreamer.returnmeeting" : "lucidDreamer.leavemeeting");
                };
            }
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
            if (Minigame.Instance)
                Minigame.Instance.ForceClose();
            if (AmongUsUtil.MapIsOpen)
                MapBehaviour.Instance.Close();
            if (DestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen)
                DestroyableSingleton<HudManager>.Instance.GameMenu.Close();

            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new Vector3(isLeaving ? 17f : 0f, 0f);
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

            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.black, Color.clear, 1f, false);
            coroutine = null;
        }

        [OnlyMyPlayer]
        void OnPlayerStepSound(PlayerCheckPlayFootSoundEvent ev) => ev.PlayFootSound &= !AddonHelper.IsOutMeeting();

        [Local]
        void OnMeetingPreStart(MeetingPreStartEvent _)
        {
            if (!CanLeaveMultiple)
                canLeave = true;

            canCompleteTasks = NumOfCanCompleteTasks;
        }

        [OnlyMyPlayer]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent _)
        {
            if (AddonHelper.IsOutMeeting() && MyPlayer.IsAlive)
                --canCompleteTasks;
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && MyPlayer.IsAlive)
                ev.UpdateSaturation(0f, true);
        }

        bool IPlayerAbility.EyesightIgnoreWalls => AddonHelper.IsOutMeeting() && MyPlayer.IsAlive;

        public static RemoteProcess<(GamePlayer player, bool on)> RpcCamouflage = new("LucidDreamerCamouflage", (message, _) =>
        {
            if (NebulaGameManager.Instance is null || GamePlayer.LocalPlayer is null) return;
            if (GamePlayer.LocalPlayer == message.player || GamePlayer.LocalPlayer.Role is not global::DHMO.Roles.Neutral.Raven.Instance) return;
            var tag = $"LucidDreamer{GamePlayer.LocalPlayer?.PlayerId}";
            if (message.on)
                message.player?.Unbox().AddOutfit(new OutfitCandidate(NebulaGameManager.Instance.UnknownOutfit, tag, 50, true));
            else
                message.player?.Unbox().RemoveOutfit(tag);
        });
    }
}