namespace DHMO.Roles;

public class LucidDreamer : DefinedSingleAbilityRoleTemplate<LucidDreamer.Ability>, DefinedRole
{
    private LucidDreamer() : base("lucidDreamer", new(176, 175, 255), RoleCategory.CrewmateRole, Crewmate.MyTeam, [NumOfLeaveOption, LeavingDuration, NumOfCanCompleteTasks, MaxLeftVotingTimeForLeaving]) 
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder?.Illustration = NebulaAPI.AddonAsset.GetResource("LucidDreamerImage.png")?.AsImage(300f);
    }
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfLeaveOption));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static internal readonly IntegerConfiguration NumOfLeaveOption = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfLeave", (1, 15), 3);
    static internal readonly FloatConfiguration LeavingDuration = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.leavingDuration", (0f, 120f, 5f), 30f, FloatConfigurationDecorator.Second);
    static internal readonly IntegerConfiguration NumOfCanCompleteTasks = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.numOfCanCompleteTasks", (1, 5), 2);
    static internal readonly FloatConfiguration MaxLeftVotingTimeForLeaving = NebulaAPI.Configurations.Configuration("options.role.lucidDreamer.maxLeftVotingTimeForLeaving", (0f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);

    static public readonly LucidDreamer MyRole = new();
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("LucidDreamerIcon.png")?.AsImage(80f);
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public float leavingTime = LeavingDuration;
        private int leftLeave = NumOfLeaveOption;
        public int completedTasks = NumOfCanCompleteTasks;

        private Coroutine? coroutine;
        public Ability(GamePlayer player, bool isUsurped, int leave) : base(player, isUsurped)
        {
            leftLeave = leave;
            if (AmOwner)
            {
                var meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetColorLabel(MyRole.RoleColor);
                meetingButton.Visibility = _ => !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                meetingButton.Availability = _ => leftLeave > 0 && AddonHelper.ModAbilityMeetingButton() && coroutine == null;
                meetingButton.SetUsesIcon(leftLeave.ToString());
                meetingButton.OnClick = _ =>
                {
                    if (IsOutMeeting())
                    {
                        NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                        --leftLeave;
                        meetingButton.UpdateUsesText(leftLeave.ToString());
                        return;
                    }
                    if (coroutine != null) return;
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(!IsOutMeeting()).WrapToIl2Cpp());

                };
                meetingButton.OnUpdate = _ =>
                {
                    if (IsOutMeeting() && MeetingHudExtension.VotingTimer <= MaxLeftVotingTimeForLeaving && coroutine == null)
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                    meetingButton.SetLabel(IsOutMeeting() ? "lucidDreamer.returnmeeting" : "lucidDreamer.leavemeeting");
                };
            }
        }

        public static bool IsOutMeeting() => MeetingHud.Instance && MeetingHud.Instance.gameObject.transform.localPosition.x > 15;
        private IEnumerator CoLeaveOrJoinMeeting(bool isleaving)
        {
            yield return HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new Vector3(isleaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isleaving;

            if (isleaving)
            {
                if (GameOperatorManager.Instance is null) yield break;
                GameOperatorManager.Instance.Subscribe<GameUpdateEvent>(ev =>
                {
                    if (IsOutMeeting())
                    {
                        if (NebulaGameManager.Instance is null) return;
                        MyPlayer.VanillaCosmetics.TogglePet(false);
                        PlayerControl.LocalPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
                    }
                    else if ((MyPlayer.IsDead && IsOutMeeting()) || !IsOutMeeting())
                    {
                        MyPlayer.VanillaCosmetics.TogglePet(true);
                        PlayerControl.LocalPlayer.gameObject.layer = PlayerControl.LocalPlayer.Data.IsDead ? LayerExpansion.GetGhostLayer() : LayerExpansion.GetPlayersLayer();
                    }
                }, this);
            }

            yield return HudManager.Instance.CoFadeFullScreen(Color.black, Color.clear, 1f, false);
            coroutine = null;
        }

        [Local]
        void OnMeetingPreStart(MeetingPreStartEvent ev) => completedTasks = NumOfCanCompleteTasks;

        [OnlyMyPlayer]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (IsOutMeeting() && !MyPlayer.IsDead)
                --completedTasks;
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if (IsOutMeeting() && !MyPlayer.IsDead)
            {
                ev.UpdateHue(180f);
                ev.UpdateSaturation(0f, true);
            }
        }

        bool IPlayerAbility.EyesightIgnoreWalls => IsOutMeeting() && MyPlayer.IsAlive;
    }
}