using Image = Virial.Media.Image;
using Object = UnityEngine.Object;

namespace DHMO.Roles;

[NebulaRPCHolder]
public class Jailor : DefinedSingleAbilityRoleTemplate<Jailor.Ability>, DefinedRole, HasCitation
{
    private Jailor() : base("jailor", new(166, 166, 166), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
    [JailCooldownOption, JailDurationOption, NumOfExecuteOption, JailInARow, HasPrivateChat, CannotExecuteAfterCrewmate])
    {
    }

    static private readonly FloatConfiguration JailCooldownOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailCooldown", (5f, 30f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration JailDurationOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailDuration", (0f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfExecuteOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfexecute", (1, 15), 3);
    static public readonly BoolConfiguration JailInARow = NebulaAPI.Configurations.Configuration("options.role.jailor.jailinArow", false);
    static public readonly BoolConfiguration HasPrivateChat = NebulaAPI.Configurations.Configuration("options.role.jailor.hasjailorchat", true);
    static private readonly BoolConfiguration CannotExecuteAfterCrewmate = NebulaAPI.Configurations.Configuration("options.role.jailor.cannotexecute", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfExecuteOption));

    private static readonly Image? jailImage = NebulaAPI.AddonAsset?.GetResource("jail.png")?.AsImage(80f);
    private static readonly Image? executeImage = NebulaAPI.AddonAsset?.GetResource("execute.png")?.AsImage(170f);
    public static readonly Image? injailImage = NebulaAPI.AddonAsset?.GetResource("InJail.png")?.AsImage();
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Jailor.png")?.AsImage(200f);
    public static readonly TranslatableTag execution = new("state.execution");
    public static readonly Jailor MyRole = new();

    public Citation? Citation => DHMOCitations.TownOfUsMira;

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IBindPlayer, IGameOperator, IGameComponent, ILifespan
    {
        public static ModAbilityButton? jailButton, executeButton;
        public static GamePlayer? Jailed;
        private static GameObject? jailCell;
        private int leftExecute = NumOfExecuteOption;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        void OnGameStarted(GameStartEvent ev)
        {
            RpcSyncJailed.Invoke((MyPlayer.PlayerId, byte.MaxValue));
            Clear();
        }

        public Ability(GamePlayer player, bool isUsurped, int leftUses) : base(player, isUsurped)
        {
            leftExecute = leftUses;
            if (AmOwner)
            {
                ObjectTracker<GamePlayer> tracker = ObjectTrackers.ForPlayer(
                    this, null, base.MyPlayer,
                    p => ObjectTrackers.StandardPredicate(p),
                    MyRole.RoleColor.ToUnityColor(), false, false);

                jailButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, VirtualKeyInput.Ability, JailCooldownOption, JailDurationOption, "jailor.jail", jailImage).SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);

                jailButton.Availability = _ => MyPlayer.CanMove && tracker.CurrentTarget != null;
                jailButton.Visibility = _ => !MyPlayer.IsDead;
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
                        {
                            RpcSyncJailed.Invoke((MyPlayer.PlayerId, tracker.CurrentTarget.PlayerId));
                        }
                    }
                    jailButton.StartCoolDown();
                };

                jailButton.OnUpdate = _ =>
                {
                    if (jailButton.IsInEffect && tracker.CurrentTarget == null)
                        jailButton.InterruptEffect();
                };

                executeButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetLabel("jailor.execute").SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);

                executeButton.Availability = _ => Jailed != null && leftExecute > 0 && AddonHelper.ModAbilityMeetingButton();
                executeButton.Visibility = _ => !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                executeButton.SetImage(executeImage!);
                executeButton.ShowUsesIcon(3, leftExecute.ToString());

                executeButton.OnClick = _ =>
                {
                    if (Jailed == null) return;
                    leftExecute--;
                    MyPlayer?.MurderPlayer(Jailed, execution, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
                    executeButton.UpdateUsesIcon(leftExecute.ToString());
                };
            }
        }

        [Local]
        void OnPlayerDie(PlayerMurderedEvent ev)
        {
            if (ev.Dead != Jailed || ev.Dead.PlayerState != execution) return;
            if (ev.Murderer.TryGetAbility<Jailor.Ability>(out _) && ev.Dead.Role.Role.Category == RoleCategory.CrewmateRole && CannotExecuteAfterCrewmate)
                leftExecute = 0;
        }

        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player == MyPlayer || ev.Player == Jailed)
                RpcSyncJailed.Invoke((MyPlayer.PlayerId, byte.MaxValue));
        }

        void OnMeetingStart(MeetingStartEvent _)
        {
            if (Jailed != null)
            {
                RpcInsulate.Invoke(Jailed);
                foreach (var voteArea in MeetingHud.Instance.playerStates)
                {
                    if (voteArea.TargetPlayerId == Jailed.PlayerId)
                        GenCell(voteArea);
                }
            }
        }

        void OnMeetingEnd(MeetingEndEvent _)
        {
            if (!JailInARow)
            {
                Clear();
                if (AmOwner) RpcSyncJailed.Invoke((MyPlayer.PlayerId, byte.MaxValue));
            }
            else if (Jailed != null && Jailed.IsDead)
            {
                Clear();
                if (AmOwner) RpcSyncJailed.Invoke((MyPlayer.PlayerId, byte.MaxValue));
            }
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

        private static readonly RemoteProcess<(byte jailorId, byte jailedId)> RpcSyncJailed = new("SyncJailed",
            (message, _) =>
            {
                var jailor = GamePlayer.GetPlayer(message.jailorId);
                if (message.jailedId == byte.MaxValue)
                {
                    Jailor.Ability.Jailed = null;
                    return;
                }
                var jailed = GamePlayer.GetPlayer(message.jailedId);
                if (jailor != null && jailor.TryGetAbility<Jailor.Ability>(out var ability))
                {
                    Jailor.Ability.Jailed = jailed;
                }
                else
                {
                    Jailor.Ability.Jailed = null;
                }
            });
    }
}