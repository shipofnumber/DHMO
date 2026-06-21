using Image = Virial.Media.Image;
using Object = UnityEngine.Object;

namespace DHMO.Roles.Crewmate;

public class Jailor : DefinedSingleAbilityRoleTemplate<Jailor.Ability>, DefinedSingleAssignable, DefinedAssignable, DefinedRole, HasCitation, IAssignableDocument
{
    private Jailor() : base("jailor", new(166, 166, 166), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
    [JailCooldownOption, JailDurationOption, NumOfExecuteOption, JailInARow, HasPrivateChat, CannotExecuteAfterCrewmate])
    {
    }

    static private readonly FloatConfiguration JailCooldownOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailCooldown", (5f, 30f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration JailDurationOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailDuration", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfExecuteOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfexecute", (1, 15), 3);
    static public readonly BoolConfiguration JailInARow = NebulaAPI.Configurations.Configuration("options.role.jailor.jailinArow", false);
    static public readonly BoolConfiguration HasPrivateChat = NebulaAPI.Configurations.Configuration("options.role.jailor.hasjailorchat", true);
    static private readonly BoolConfiguration CannotExecuteAfterCrewmate = NebulaAPI.Configurations.Configuration("options.role.jailor.cannotexecute", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfExecuteOption));

    private static readonly Image? jailImage = NebulaAPI.AddonAsset?.GetResource("Button/JailButton.png")?.AsImage(80f);
    private static readonly Image? executeImage = NebulaAPI.AddonAsset?.GetResource("Button/ExecuteButton.png")?.AsImage(170f);
    public static readonly Image? injailImage = NebulaAPI.AddonAsset?.GetResource("InJail.png")?.AsImage();
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/JailorIcon.png")?.AsImage(200f);
    public static readonly TranslatableTag execution = new("state.execution");
    public static readonly Jailor MyRole = new();

    bool IAssignableDocument.HasTips => true;

    public Citation? Citation => DHMOCitations.TownOfUsMira;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IBindPlayer, IGameOperator, IGameComponent, ILifespan
    {
        public GamePlayer? jailed = null;
        private static GameObject? jailCell;
        private int leftExecute = NumOfExecuteOption;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        void OnGameStarted(GameStartEvent ev)
        {
            jailed = null;
            Clear();
        }

        public Ability(GamePlayer player, bool isUsurped, int leftUses) : base(player, isUsurped)
        {
            leftExecute = leftUses;
            if (HasPrivateChat)
                PrivateChat.Register(MyRole.RoleColor, MyRole.RoleColor, $"Jailor{MyPlayer.PlayerId}", Language.Translate("chat.jailortext"), this, () => MeetingHud.Instance && jailed is not null && jailed.IsAlive && MyPlayer.IsAlive, (sender, receiver) => (sender == MyPlayer && IsJailed(receiver)) || (IsJailed(sender) && receiver == MyPlayer));
            if (AmOwner)
            {

                ObjectTracker<GamePlayer> tracker = ObjectTrackers.ForPlayer(
                    this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), null, false, false);

                var jailButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, VirtualKeyInput.Ability, JailCooldownOption, JailDurationOption, "jailor.jail", jailImage).SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);

                jailButton.Availability = _ => MyPlayer.CanMove && tracker.CurrentTarget != null && jailed == null && !IsOthersJailed(tracker.CurrentTarget);
                jailButton.Visibility = _ => MyPlayer.IsAlive;
                jailButton.OnEffectStart = _ => tracker.KeepAsLongAsPossible = true;
                jailButton.OnEffectEnd = _ =>
                {
                    if (tracker.CurrentTarget == null)
                    {
                        tracker.KeepAsLongAsPossible = false;
                        return;
                    }

                    tracker.KeepAsLongAsPossible = false;
                    if (!jailButton.EffectTimer!.IsProgressing)
                    {
                        if (!(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, tracker.CurrentTarget, new(RealPlayerOnly: true))).IsCanceled ?? false))
                            RpcJail.Invoke((MyPlayer, tracker.CurrentTarget, false));
                    }
                    jailButton.StartCoolDown();
                };

                jailButton.OnUpdate = _ =>
                {
                    if (jailButton.IsInEffect && tracker.CurrentTarget == null)
                        jailButton.InterruptEffect();
                };

                var executeButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetLabel("jailor.execute").SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);

                executeButton.Availability = _ => jailed is not null && leftExecute > 0 && AddonHelper.ModAbilityMeetingButton();
                executeButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && leftExecute > 0;
                executeButton.SetImage(executeImage!);
                executeButton.SetUsesIcon(MyRole.RoleColor, leftExecute.ToString(), out _, out var text);

                executeButton.OnClick = _ =>
                {
                    if (jailed == null) return;
                    leftExecute--;
                    MyPlayer?.MurderPlayer(jailed, execution, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
                    text.text = leftExecute.ToString();
                };
            }
        }

        [Local]
        void OnPlayerDie(PlayerMurderedEvent ev)
        {
            if (ev.Dead != jailed || ev.Dead.PlayerState != execution) return;
            if (ev.Murderer.TryGetAbility<Jailor.Ability>(out _) && ev.Dead.Role.Role.Category == RoleCategory.CrewmateRole && CannotExecuteAfterCrewmate)
                leftExecute = 0;
        }

        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            if (jailed is not null)
               if (ev.Player == MyPlayer || ev.Player == jailed)
                  RpcJail.Invoke((MyPlayer, jailed, true));
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MeetingHud.Instance && jailed is not null)
            {
                using (RPCRouter.CreateSection("JailorJailMeeting"))
                {
                    RpcInsulate.Invoke(jailed);
                    RpcJail.Invoke((MyPlayer, jailed, false));
                }
            }
        }

        void OnMeetingEnd(MeetingEndEvent _)
        {
            if (jailed is not null)
            {
                if (!JailInARow || jailed.IsDead)
                    RpcJail.Invoke((MyPlayer, jailed, true));
            }
        }

        public bool IsJailed(GamePlayer player)
        {
            if (MyPlayer.IsAlive && MyPlayer.IsActive)
                if (player == jailed) return true;
            return false;
        }

        public static bool IsOthersJailed(GamePlayer player)
        {
            bool isOthers = false;
            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.TryGetAbility<Jailor.Ability>(out var ability))
                {
                    isOthers = ability.IsJailed(player);
                    if (isOthers)
                        break;
                }
            }
            return isOthers;
        }

        public static void Clear()
        {
            if (jailCell != null)
            {
                var child = jailCell.transform.GetChild(0);
                child?.gameObject.Destroy();
                jailCell = null;
            }
        }

        internal static void GenCell(PlayerVoteArea voteArea)
        {
            if (voteArea == null) return;

            var confirmButton = voteArea.Buttons.transform.GetChild(0).gameObject;
            var parent = confirmButton.transform.parent.parent;

            var jailCellObj = Object.Instantiate(confirmButton, voteArea.transform);
            var cellRenderer = jailCellObj.GetComponent<SpriteRenderer>();
            if (injailImage != null)
                cellRenderer.sprite = injailImage.GetSprite();

            jailCellObj.transform.localPosition = new Vector3(-0.95f, 0f, -2f);
            jailCellObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            jailCellObj.layer = 5;
            jailCellObj.transform.parent = parent;

            var child = jailCellObj.transform.GetChild(0);
            child?.gameObject.Destroy();

            var passive = jailCellObj.GetComponent<PassiveButton>();
            passive.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();

            jailCell = jailCellObj;
        }

        private static readonly RemoteProcess<GamePlayer> RpcInsulate = new("InsulateJailor", (player, _) =>
        {
            MeetingHudExtension.AddSealedMask(1 << player.PlayerId);
            if (player.AmOwner) MeetingHudExtension.CanUseAbility = false;
            MeetingHud.Instance.ResetPlayerState();
        });

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer jailed, bool clear)> RpcJail = new("Jail", (message, _) =>
        {
            if (message.jailor.TryGetAbility<Jailor.Ability>(out var ability))
            {
                if (message.clear) Clear();
                else
                {
                    foreach (var voteArea in MeetingHud.Instance.playerStates)
                    {
                        if (voteArea.TargetPlayerId == message.jailed.PlayerId)
                            GenCell(voteArea);
                    }
                }
                ability.jailed = message.clear ? null : message.jailed;
            }
        });
    }
}