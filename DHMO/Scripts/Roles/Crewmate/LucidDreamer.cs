using DHMO.Roles.Abilities;

namespace DHMO.Roles.Crewmate;

public class LucidDreamer : DefinedSingleAbilityRoleTemplate<LucidDreamer.Ability>, DefinedRole, HasCitation, IAssignableDocument
{
    private LucidDreamer() : base("lucidDreamer", new(176, 175, 255), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [NumOfLeaveOption, CanLeaveMultiple, MaxLeftVotingTimeForLeaving, NumOfCanCompleteTasks])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder?.Illustration = NebulaAPI.AddonAsset.GetResource("Image/LucidDreamerImage.png")?.AsImage(300f);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfLeaveOption));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static internal readonly IntegerConfiguration NumOfLeaveOption = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfLeave", (1, 15), 3);
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
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IGameOperator
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        ModAbilityButton? meetingButton;
        private int leftLeave = NumOfLeaveOption;
        public int canCompleteTasks = NumOfCanCompleteTasks;
        bool canLeave;
        private Coroutine? coroutine;

        public Ability(GamePlayer player, bool isUsurped, int leave) : base(player, isUsurped)
        {
            leftLeave = leave;

            if (AmOwner)
            {
                GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
                {
                    if (ev.Player == MyPlayer && AddonHelper.IsOutMeeting())
                        LeaveOrJoinMeeting(false);
                }, this);

                var modUseButton = new ModAbilityButtonImpl(alwaysShow: true).KeyBind(VirtualKeyInput.Use).Register(this);
                modUseButton.Visibility = _ => MyPlayer.IsAlive && AddonHelper.IsOutMeeting() && coroutine == null;
                modUseButton.Availability = _ => MyPlayer.CanMove && MyPlayer.VanillaPlayer.closest != null && canCompleteTasks > 0;
                modUseButton.OnClick = _ => MyPlayer.VanillaPlayer.UseClosest();
                modUseButton.RelatedAbility = this;

                modUseButton.OnUpdate = button =>
                {
                    var bridge = AmongUsLLImpl.HudManagerBridge;
                    ImageNames imageNames;

                    if (bridge.UseButton.currentTarget == null)
                        imageNames = ImageNames.UseButton;
                    else
                        imageNames = bridge.UseButton.currentTarget.UseIcon;

                    if (!bridge.UseButtonVanillaSettings.ContainsKey(imageNames))
                        imageNames = ImageNames.UseButton;

                    var settings = bridge.UseButtonVanillaSettings[imageNames];

                    if (settings.AsBoolFast())
                    {
                        button.SetSprite(settings.Image);
                        button.VanillaButton.buttonLabelText.SetSharedMaterial(settings.FontMaterial);
                        button.SetRawLabel(VanillaTranslationCache.GetString(settings.Text));
                    }
                };

                meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction)
                    .SetColorLabel(MyRole.RoleColor)
                    .SetImage(buttonImage!)
                    .SetAsUsurpableButton(this);

                meetingButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && leftLeave > 0;
                meetingButton.Availability = _ => AddonHelper.ModAbilityMeetingButton() && coroutine == null && (CanLeaveMultiple || canLeave) && MeetingHudExtension.VotingTimer > MaxLeftVotingTimeForLeaving;
                meetingButton.ShowUsesIcon(3, leftLeave.ToString());

                meetingButton.OnClick = button => LeaveOrJoinMeeting(!AddonHelper.IsOutMeeting());
                meetingButton.OnUpdate = button =>
                {
                    if (AddonHelper.IsOutMeeting())
                    {
                        if ((MeetingHudExtension.VotingTimer <= MaxLeftVotingTimeForLeaving || canCompleteTasks <= 0) && coroutine == null)
                            LeaveOrJoinMeeting(false);
                    }
                    button.SetLabel(AddonHelper.IsOutMeeting() ? "lucidDreamer.returnmeeting" : "lucidDreamer.leavemeeting");
                };
            }
        }

        void IGameOperator.OnReleased()
        {
            if (AddonHelper.IsOutMeeting())
                LeaveOrJoinMeeting(false);
        }

        void LeaveOrJoinMeeting(bool isLeaving)
        {
            if (!isLeaving)
            {
                --leftLeave;
                meetingButton?.UpdateUsesIcon(leftLeave.ToString());
            }

            if (coroutine != null) return;
            coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(isLeaving).WrapToIl2Cpp());
        }

        private IEnumerator CoLeaveOrJoinMeeting(bool isLeaving)
        {
            yield return AmongUsLLImpl.TryGetHudManager(out var hud);

            if (Minigame.Instance.AsBoolFast(out var minigame)) minigame.ForceClose();
            if (AmongUsUtil.MapIsOpen) MapBehaviour.Instance.Close();
            if (hud.GameMenu.IsOpen) hud.GameMenu.Close();

            yield return hud.CoFadeFullScreen(VColor.Clear.ToUnityColor(), VColor.Black.ToUnityColor(), 1f, false);
            MeetingHud.Instance.ModGameObject(false).LocalPosition = new VVector3(isLeaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isLeaving;

            if (!isLeaving)
            {
                if (!CanLeaveMultiple)
                    canLeave = false;
            }

            yield return hud.CoFadeFullScreen(VColor.Black.ToUnityColor(), VColor.Clear.ToUnityColor(), 1f, false);
            coroutine = null;
        }

        [OnlyMyPlayer]
        void OnPlayerStepSound(PlayerCheckPlayFootSoundEvent ev) => ev.PlayFootSound &= !AddonHelper.IsOutMeeting();

        [Local]
        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            if (!CanLeaveMultiple)
                canLeave = true;

            canCompleteTasks = NumOfCanCompleteTasks;
        }

        [OnlyMyPlayer]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && MyPlayer.IsAlive) --canCompleteTasks;
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if (MyPlayer.AmOwner && AddonHelper.IsOutMeeting() && MyPlayer.IsAlive) ev.UpdateSaturation(0f, true);
        }

        bool IPlayerAbility.EyesightIgnoreWalls => AddonHelper.IsOutMeeting() && MyPlayer.IsAlive;
    }
}