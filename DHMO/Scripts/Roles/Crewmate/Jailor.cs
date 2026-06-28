using Image = Virial.Media.Image;

namespace DHMO.Roles.Crewmate;

public class Jailor : DefinedSingleAbilityRoleTemplate<Jailor.Ability>, DefinedRole, HasCitation, IAssignableDocument
{
    private Jailor() : base("jailor", new(166, 166, 166), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [JailCooldownOption, JailDurationOption, NumOfJailOption, JailInARow, CannotJailAfterCrewmate, HasPrivateChat])
    { }

    static private readonly FloatConfiguration JailCooldownOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailCooldown", (5f, 30f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration JailDurationOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailDuration", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfJailOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfjail", (1, 5), 3);
    static public readonly BoolConfiguration JailInARow = NebulaAPI.Configurations.Configuration("options.role.jailor.jailinArow", false);
    static private readonly BoolConfiguration CannotJailAfterCrewmate = NebulaAPI.Configurations.Configuration("options.role.jailor.canNotjail", true);
    static public readonly BoolConfiguration HasPrivateChat = NebulaAPI.Configurations.Configuration("options.role.jailor.hasjailorchat", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfJailOption));

    private static readonly Image? jailImage = NebulaAPI.AddonAsset?.GetResource("Button/JailButton.png")?.AsImage(80f);
    private static readonly Image? executeImage = NebulaAPI.AddonAsset?.GetResource("Button/ExecuteButton.png")?.AsImage(170f);
    public static readonly Image? injailImage = NebulaAPI.AddonAsset?.GetResource("InJail.png")?.AsImage();

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/JailorIcon.png")?.AsImage(200f);
    public static readonly TranslatableTag execution = new("state.execution");
    public static readonly Jailor MyRole = new();
    bool IAssignableDocument.HasTips => true;
    public Citation? Citation => DHMOCitations.TownOfUsMira;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IBindPlayer, IGameOperator, ILifespan
    {
        public GamePlayer? jailed = null;
        private GameObject? jailCell;
        private int leftJail = NumOfJailOption;
        TextMeshPro? tmPro;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        public Ability(GamePlayer player, bool isUsurped, int leftUses) : base(player, isUsurped)
        {
            leftJail = leftUses;
            GameOperatorManager.Instance?.Subscribe(this, this, () => RpcJail.Invoke((MyPlayer, null!, true)));

            if (HasPrivateChat)
                PrivateChat.RegisterPublicChannel(MyRole.Color, MyRole.Color, $"Jailor{MyPlayer.PlayerId}", Language.Translate("chat.jailortext"), this,
                    () => MeetingHud.Instance.AsBoolFast() && jailed != null && jailed.IsAlive && (MyPlayer.AmOwner || jailed.AmOwner), true,
                    text => RpcSendChat.Invoke((MyPlayer, GamePlayer.LocalPlayer!, text)));

            if (AmOwner)
            {
                ObjectTracker<GamePlayer> tracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), null, false, false);

                var jailButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, VirtualKeyInput.Ability, JailCooldownOption, JailDurationOption, "jailor.jail", jailImage)
                    .SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                jailButton.Availability = _ => MyPlayer.CanMove && tracker.CurrentTarget != null && !IsOthersJailed(tracker.CurrentTarget) && jailed == null;
                jailButton.Visibility = _ => MyPlayer.IsAlive;
                jailButton.OnEffectStart = _ => tracker.KeepAsLongAsPossible = true;
                jailButton.SetUsesIcon(MyRole.Color, leftJail.ToString(), out _, out tmPro);

                jailButton.OnEffectEnd = _ =>
                {
                    if (tracker.CurrentTarget == null) { tracker.KeepAsLongAsPossible = false; return; }
                    tracker.KeepAsLongAsPossible = false;
                    if (!jailButton.EffectTimer!.IsProgressing)
                    {
                        --leftJail;
                        tmPro.text = leftJail.ToString();
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

                var executeButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction).SetLabel("jailor.execute")
                    .SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                executeButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && leftJail > 0 && jailed != null;
                executeButton.Availability = _ => jailed!.IsAlive && AddonHelper.ModAbilityMeetingButton();
                executeButton.SetImage(executeImage!);
                executeButton.OnClick = _ =>
                {
                    if (jailed == null) return;
                    MyPlayer.MurderPlayer(jailed, execution, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
                };
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Dead != jailed || ev.Dead.PlayerState != execution) return;
            if (ev.Murderer == MyPlayer && ev.Dead.Role.Role.Category == RoleCategory.CrewmateRole && CannotJailAfterCrewmate)
            {
                leftJail = 0;
                tmPro?.text = leftJail.ToString();
            }
        }

        void OnPlayerDieOrDisconnect(PlayerDieOrDisconnectEvent ev)
        {
            if (jailed == null) return;
            if (ev.Player == MyPlayer || ev.Player == jailed) 
                RpcJail.Invoke((MyPlayer, jailed, true));
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            Clear();
            if (jailed == null) return;
            RpcInsulate.Invoke(jailed);
            foreach (var voteArea in MeetingHud.Instance.playerStates)
                if (jailed.PlayerId == voteArea.TargetPlayerId) GenCell(voteArea);
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (jailed == null) return;
            if (!JailInARow)
                RpcJail.Invoke((MyPlayer, jailed, true));
            else
            {
                --leftJail;
                tmPro?.text = leftJail.ToString();
            }
        }

        public bool IsJailed(GamePlayer player) => MyPlayer.IsAlive && player == jailed;

        public static bool IsOthersJailed(GamePlayer player)
        {
            foreach (var p in GamePlayer.AllPlayers)
                if (p.TryGetAbility<Jailor.Ability>(out var ability) && ability.IsJailed(player))
                    return true;
            return false;
        }

        public void Clear()
        {
            if (jailCell.AsBoolFast()) 
                jailCell?.Destroy();
        }

        internal void GenCell(PlayerVoteArea voteArea)
        {
            var cellRenderer = UnityHelper.CreateSpriteRenderer("JailCell", voteArea.transform, new VVector3(-0.95f, 0f, -2f), 5);
            cellRenderer.sprite = injailImage?.GetSprite();
            cellRenderer.transform.localScale = new VVector3(0.6f, 0.6f, 0.6f);
            jailCell = cellRenderer.gameObject;
        }

        private static readonly RemoteProcess<GamePlayer> RpcInsulate = new("InsulateJailor", (player, _) =>
        {
            MeetingHudExtension.AddSealedMask(1 << player.PlayerId);
            if (player.AmOwner) MeetingHudExtension.CanUseAbility = false;
            MeetingHud.Instance.ResetPlayerState();
        });

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer jailed, bool clear)> RpcJail = new("JailorJail", (message, _) =>
        {
            if (!message.jailor.TryGetAbility<Jailor.Ability>(out var ability)) return;
            ability.jailed = message.clear ? null : message.jailed;
        });

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer sender, string text)> RpcSendChat = new("JailorSendChat", (message, _) =>
        {
            if (NebulaGameManager.Instance == null || !HasPrivateChat || GamePlayer.LocalPlayer == null || !message.jailor.TryGetAbility<Jailor.Ability>(out var ability) || ability.jailed == null) return;
            if (GamePlayer.LocalPlayer != message.jailor && GamePlayer.LocalPlayer != ability.jailed && !NebulaGameManager.Instance!.CanSeeAllInfo) return;

            var chat = AmongUsLLImpl.HudManagerBridge.Chat;
            VColor color = Jailor.MyRole.Color;
            string tag = Language.Translate("chat.jailortext");
            GamePlayer sender = message.sender;

            if (sender == message.jailor && GamePlayer.LocalPlayer == ability.jailed && !NebulaGameManager.Instance.CanSeeAllInfo)
                chat.AddCustomChat(sender.VanillaPlayer, ability.jailed.VanillaPlayer, $"{(Jailor.MyRole as DefinedAssignable).DisplayName}({tag})".Color(color), message.text);
            else
                chat.AddCustomChat(sender.VanillaPlayer, sender.VanillaPlayer, $"{sender.Name}{$"({tag})".Color(color)}", message.text);
        });
    }
}