using Image = Virial.Media.Image;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DHMO.Roles;

[NebulaRPCHolder]
public class Jailor : DefinedSingleAbilityRoleTemplate<Jailor.Ability>, DefinedRole, HasCitation
{
    private Jailor() : base("jailor", new(166, 166, 166), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [JailCooldownOption, JailDurationOption, NumOfExecuteOption, JailInARow, HasPrivateChat, CannotExecuteAfterCrewmate])
    {
    }

    static private readonly FloatConfiguration JailCooldownOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailCooldown", (5f,30f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration JailDurationOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailDuration", (0f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfExecuteOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfexecute", (1, 15), 3);
    static public readonly BoolConfiguration JailInARow = NebulaAPI.Configurations.Configuration("options.role.jailor.jailinArow", false);
    static public readonly BoolConfiguration HasPrivateChat = NebulaAPI.Configurations.Configuration("options.role.jailor.hasjailorchat", true);
    static private readonly BoolConfiguration CannotExecuteAfterCrewmate = NebulaAPI.Configurations.Configuration("options.role.jailor.cannotexecute", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfExecuteOption));

    private static readonly RemoteProcess<GamePlayer> RpcInsulate = new("InsulateMod", (player, _) =>
    {
        MeetingHudExtension.AddSealedMask(1 << player.PlayerId);
        if (player.AmOwner) MeetingHudExtension.CanUseAbility = false;
        MeetingHud.Instance.ResetPlayerState();
    });

    private static readonly Image? jailImage = NebulaAPI.AddonAsset?.GetResource("jail.png")?.AsImage(80f);
    private static readonly Image? executeImage = NebulaAPI.AddonAsset?.GetResource("execute.png")?.AsImage(170f);
    public static Image? injailImage = NebulaAPI.AddonAsset?.GetResource("InJail.png")?.AsImage();
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Jailor.png")?.AsImage(200f);
    public static TranslatableTag execution = new("state.execution");
    public static Jailor MyRole = new();

    public Citation? Citation => DHMOCitations.TownOfUsMira;

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public static ModAbilityButton? jailButton, executeButton;
        public static GamePlayer? Jailed;
        private static GameObject? jailCell;
        private int leftExecute = NumOfExecuteOption;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        void OnGameStarted(GameStartEvent ev)
        {
             Jailed = null;
             Clear();
        }

        public Ability(GamePlayer player, bool isUsurped, int leftUses) : base(player, isUsurped)
        {
            this.leftExecute = leftUses;
            if (AmOwner)
            {
                ObjectTracker<GamePlayer> tracker = ObjectTrackers.ForPlayer(
                    this, null, base.MyPlayer,
                    p => ObjectTrackers.StandardPredicate(p),
                    MyRole.RoleColor.ToUnityColor(), false, false
                );
                jailButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, VirtualKeyInput.Ability, JailCooldownOption, JailDurationOption, "jailor.jail", jailImage).SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                jailButton.Availability = _ => MyPlayer.CanMove && tracker.CurrentTarget != null;
                jailButton.Visibility = _ => !MyPlayer.IsDead;
                jailButton.OnEffectStart = _ => tracker.KeepAsLongAsPossible = true;
                jailButton.OnEffectEnd = _ =>
                {
                    if (tracker.CurrentTarget == null) return;
                    tracker.KeepAsLongAsPossible = false;
                    if (!jailButton.EffectTimer!.IsProgressing)
                    {
                        if (!(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, tracker.CurrentTarget, new(RealPlayerOnly: true))).IsCanceled ?? false))
                        {
                            Jailed = null;
                            var player = tracker.CurrentTarget;
                            Jailed = player;
                        }
                    }
                    jailButton.StartCoolDown();
                };

                jailButton.OnUpdate = _ => {
                    if (!jailButton.IsInEffect) return;
                    if (tracker.CurrentTarget == null) jailButton.InterruptEffect();
                };

                executeButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetLabel("jailor.execute").SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                executeButton.Availability = _ => Jailed != null && leftExecute > 0;
                executeButton.Visibility = _ => !MyPlayer.IsDead && MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Discussion && MeetingHud.Instance.state != MeetingHud.VoteStates.Results;
                executeButton.SetImage(executeImage!);
                executeButton.ShowUsesIcon(3, leftExecute.ToString());

                executeButton.OnClick = _ =>
                {
                    leftExecute--;
                    var jailed = Ability.Jailed;
                    MyPlayer?.MurderPlayer(jailed!, execution, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
                    executeButton?.UpdateUsesIcon(leftExecute.ToString());
                };
            }
        }

        [Local]
        void OnPlayerDie(PlayerMurderedEvent ev)
        {
            var jailed = Ability.Jailed;
            if (ev.Dead == jailed && ev.Dead.PlayerState == execution && ev.Murderer.TryGetAbility<Jailor.Ability>(out _))
            {
                if (ev.Dead.Role.Role.Category == RoleCategory.CrewmateRole && CannotExecuteAfterCrewmate)
                {
                    leftExecute = 0;
                }
            }
        }

        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            var jailed = Ability.Jailed;
            if (ev.Player == MyPlayer || ev.Player == jailed)
            {
                jailed = null;
            }
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            var jailed = Ability.Jailed;
            if (jailed != null)
            {
                RpcInsulate.Invoke(jailed);
                RpcSend.Invoke(jailed);
            }
        }

        void OnEndMeeting(MeetingEndEvent ev)
        {
            if (JailInARow == false)
            {
                Ability.Jailed = null;
                Clear();
            }
            if (Ability.Jailed!.IsDead)
            {
                Ability.Jailed = null;
                Clear();
            }
        }
        public static void Clear()
        {
            jailCell?.transform.GetChild(0).gameObject.Destroy();
        }
        internal static void GenCell(PlayerVoteArea voteArea)
        {
            var confirmButton = voteArea.Buttons.transform.GetChild(0).gameObject;
            var parent = confirmButton.transform.parent.parent;

            var jailCellObj = Object.Instantiate(confirmButton, voteArea.transform);

            var cellRenderer = jailCellObj.GetComponent<SpriteRenderer>();
            if(injailImage is not null)    
            cellRenderer.sprite = injailImage.GetSprite();

            jailCellObj.transform.localPosition = new Vector3(-0.95f, 0f, -2f);
            jailCellObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            jailCellObj.layer = 5;
            jailCellObj.transform.parent = parent;
            jailCellObj.transform.GetChild(0).gameObject.Destroy();

            var passive = jailCellObj.GetComponent<PassiveButton>();
            passive.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();

            jailCell = jailCellObj;
        }

        static RemoteProcess<GamePlayer> RpcSend = new("JailorSendRpc", (msg, __) =>
        {
            foreach (var voteArea in MeetingHud.Instance.playerStates)
            {
                if (msg is not null && msg.PlayerId == voteArea.TargetPlayerId)
                {
                    GenCell(voteArea);
                }
            }
        });
    }
}