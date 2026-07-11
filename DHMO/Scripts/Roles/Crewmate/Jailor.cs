using Image = Virial.Media.Image;

namespace DHMO.Roles.Crewmate;

public class Jailor : DefinedSingleAbilityRoleTemplate<Jailor.Ability>, DefinedRole, HasCitation, IAssignableDocument
{
    private Jailor() : base("jailor", new(166, 166, 166), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [JailCooldownOption, JailDurationOption, JailInARow, LimitAbilityUsesOption, NumOfJailOption, NumOfExecuteOption, MaxLeftVotingTimeForExecuting, CanExecuteLoversOption, CannotJailAfterCrewmate, HasPrivateChat])
    { }

    static private readonly FloatConfiguration JailCooldownOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailCooldown", (5f, 30f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration JailDurationOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailDuration", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static public readonly BoolConfiguration JailInARow = NebulaAPI.Configurations.Configuration("options.role.jailor.jailinArow", false);
    private static readonly ValueConfiguration<int> LimitAbilityUsesOption = NebulaAPI.Configurations.Configuration("options.role.jailor.limitAbilityUsesOf", ["options.role.jailor.limitAbilityUsesOf.jail", "options.role.jailor.limitAbilityUsesOf.execute"], 0);
    static private readonly IntegerConfiguration NumOfJailOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfjail", (1, 10), 5, () => LimitAbilityUsesOption.GetValue() == 0);
    static private readonly IntegerConfiguration NumOfExecuteOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfexecute", (1, 5), 3, () => LimitAbilityUsesOption.GetValue() == 1);
    static internal readonly FloatConfiguration MaxLeftVotingTimeForExecuting = NebulaAPI.Configurations.Configuration("options.role.jailor.maxLeftVotingTimeForExecuting", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration CanExecuteLoversOption = NebulaAPI.Configurations.Configuration("options.role.jailor.canExecuteLovers", false);
    static private readonly BoolConfiguration CannotJailAfterCrewmate = NebulaAPI.Configurations.Configuration("options.role.jailor.cannotJailAfterExecuteCrew", true);
    static public readonly BoolConfiguration HasPrivateChat = NebulaAPI.Configurations.Configuration("options.role.jailor.hasJailorchat", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.Get(0, GetAbilityUses()));

    private static readonly Image? jailImage = NebulaAPI.AddonAsset?.GetResource("Button/JailButton.png")?.AsImage(80f);
    private static readonly Image? executeImage = NebulaAPI.AddonAsset?.GetResource("Button/ExecuteButton.png")?.AsImage(170f);
    public static readonly Image? injailImage = NebulaAPI.AddonAsset?.GetResource("InJail.png")?.AsImage();

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/JailorIcon.png")?.AsImage(200f);
    public static readonly TranslatableTag execution = new("state.execution");
    public static readonly Jailor MyRole = new();
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(jailImage!, "role.jailor.ability.jail");
        yield return new(executeImage!, "role.jailor.ability.execute");
        yield return new(injailImage!, "role.jailor.ability.inJail");
    }
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%JAILINAROW%", Language.Translate(JailInARow ? "role.jailor.ability.jailinArow" : "role.jailor.ability.clearJailed"));
        yield return new("%CANEXECUTELOVER%", CanExecuteLoversOption ? Language.Translate("role.jailor.ability.canExecuteLover") : "");
        yield return new("%CANNOTJAIL%", CannotJailAfterCrewmate ? Language.Translate("role.jailor.ability.cannotJailAfterExecuteCrew") : "");
    }

    public Citation? Citation => DHMOCitations.TownOfUsMira;

    private static bool IsLimiltJailUses()
    {
        if (LimitAbilityUsesOption.GetValue() == 0) return true;
        else
            return false;
    }

    private static int GetAbilityUses() => IsLimiltJailUses() ? NumOfJailOption : NumOfExecuteOption;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerAbility, IPlayerAbility, IBindPlayer, IGameOperator, ILifespan
    {
        ModAbilityButton? jailButton, executeButton;
        public GamePlayer? jailed { get; private set; } = null;
        private EditableBitMask<GamePlayer> hasJailed = BitMasks.AsPlayer();

        private GameObject? jailCell;
        private int leftUses = GetAbilityUses();

        public Ability(GamePlayer player, int uses) : base(player)
        {
            this.leftUses = uses;

            if (HasPrivateChat)
                PrivateChat.RegisterPublicChannel(MyRole.Color, MyRole.Color, $"Jailor{MyPlayer.PlayerId}", Language.Translate("chat.jailortext"), this,
                    () => AmongUsUtil.InMeeting && jailed != null && jailed.IsAlive && (MyPlayer.AmOwner || jailed.AmOwner), true,
                    text => RpcSendChat.Invoke((MyPlayer, GamePlayer.LocalPlayer ?? MyPlayer, text)));

            if (AmOwner)
            {
                ObjectTracker<GamePlayer> tracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), null, false, false);

                jailButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, VirtualKeyInput.Ability, JailCooldownOption, JailDurationOption, "jailor.jail", jailImage)
                    .SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                jailButton.Availability = _ => MyPlayer.CanMove && tracker.CurrentTarget != null;
                jailButton.Visibility = _ => MyPlayer.IsAlive && leftUses > 0;

                if (IsLimiltJailUses())
                    jailButton.ShowUsesIcon(3, leftUses.ToString());

                jailButton.OnEffectStart = _ => tracker.KeepAsLongAsPossible = true;
                jailButton.OnEffectEnd = button =>
                {
                    tracker.KeepAsLongAsPossible = false;
                    if (tracker.CurrentTarget == null) return;

                    if (!button.EffectTimer?.IsProgressing ?? false)
                    {
                        if (!(GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, tracker.CurrentTarget, new(RealPlayerOnly: true))).IsCanceled ?? false))
                        {
                            if (IsLimiltJailUses())
                            {
                                --leftUses;
                                button.UpdateUsesIcon(leftUses.ToString());
                            }

                            hasJailed.Add(tracker.CurrentTarget);
                            RpcJail.Invoke((MyPlayer, tracker.CurrentTarget));
                        }
                    }
                    button.StartCoolDown();
                };

                jailButton.OnUpdate = button =>
                {
                    if (button.IsInEffect && tracker.CurrentTarget == null)
                        button.InterruptEffect();
                };

                executeButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction).SetLabel("jailor.execute")
                    .SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                executeButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && leftUses > 0 && jailed != null;
                executeButton.Availability = _ => jailed != null && jailed.IsAlive && AddonHelper.ModAbilityMeetingButton() && MeetingHudExtension.VotingTimer >= MaxLeftVotingTimeForExecuting && MyPlayer.CanKill(jailed);
                executeButton.SetImage(executeImage!);

                if (!IsLimiltJailUses())
                    executeButton.ShowUsesIcon(3, leftUses.ToString());

                executeButton.OnClick = button =>
                {
                    if (jailed == null) return;
                    MyPlayer.MurderPlayer(jailed, execution, execution, KillParameter.MeetingKill, KillCondition.TargetAlive);
                };
            }
        }

        void EditGuessable(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (ev.Guesser == MyPlayer && hasJailed.Test(ev.Target) && ev.Target.IsAlive) 
                ev.CanGuess = false;
        }

        void CheckPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Dead.PlayerId != jailed?.PlayerId || !AmongUsUtil.InMeeting) return;
            if (ev.Murderer == MyPlayer && ev.Dead.PlayerState == execution)
            {
                bool canExecute = false;

                if (!ev.Dead.IsTrueCrewmate) canExecute = true;
                else if (ev.Dead.Role is JekyllAndHyde.Instance jah && !jah.AmJekyll) canExecute = true;
                else if (ev.Dead.TryGetModifier<Lover.Instance>(out _) && CanExecuteLoversOption) canExecute = true;

                if (!MyPlayer.AmOwner) return;
                if (!canExecute && MyPlayer.IsTrueCrewmate)
                {
                    leftUses = 0;
                    AmongUsUtil.PlayFlash(VColor.Red);
                }
                else
                {
                    if (!IsLimiltJailUses()) return;
                    --leftUses;
                }

                if (IsLimiltJailUses())
                    jailButton?.UpdateUsesIcon(leftUses.ToString());
                else
                    executeButton?.UpdateUsesIcon(leftUses.ToString());
            }
        }

        void OnPlayerDieOrDisconnect(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player == MyPlayer || ev.Player == jailed) 
                RpcJail.Invoke((MyPlayer, null));
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            Clear();
            if (jailed == null) return;

            RpcInsulate.Invoke(jailed);

            foreach (var voteArea in MeetingHud.Instance.playerStates)
            {
                if (jailed.PlayerId == voteArea.TargetPlayerId)
                {
                    var voteAreaTransform = voteArea.ModGameObject().GetUnityTransform();
                    GenCell(voteAreaTransform);
                }
            }
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (jailed == null) return;

            if (!JailInARow)
                RpcJail.Invoke((MyPlayer, null));
            else
            {
                if (IsLimiltJailUses() && leftUses > 0)
                {
                    --leftUses;
                    jailButton?.UpdateUsesIcon(leftUses.ToString());
                }
            }
        }

        public void Clear()
        {
            if (jailCell.AsBoolFast()) 
                jailCell?.Destroy();
        }

        internal void GenCell(Transform voteArea)
        {
            var cellRenderer = UnityHelper.CreateSpriteRenderer("JailCell", voteArea, new VVector3(-0.95f, 0f, -2f), 5);
            cellRenderer.sprite = injailImage?.GetSprite();
            cellRenderer.ModGameObject(false).LocalScale = new VVector3(0.6f, 0.6f, 0.6f);
            jailCell = cellRenderer.gameObject;
        }

        private static readonly RemoteProcess<GamePlayer> RpcInsulate = new("InsulateJailor", (player, _) =>
        {
            MeetingHudExtension.AddSealedMask(1 << player.PlayerId);
            if (player.AmOwner) MeetingHudExtension.CanUseAbility = false;
            MeetingHud.Instance.ResetPlayerState();
        });

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer? jailed)> RpcJail = new("JailorJail", (message, _) =>
        {
            if (!message.jailor.TryGetAbility<Jailor.Ability>(out var ability)) return;
            ability.jailed = message.jailed;
        });

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer sender, string text)> RpcSendChat = new("JailorSendChat", (message, _) =>
        {
            if (string.IsNullOrEmpty(message.text) || NebulaGameManager.Instance == null || !message.jailor.TryGetAbility<Jailor.Ability>(out var ability) || ability.jailed == null) return;
            if (message.jailor.AmOwner && ability.jailed.AmOwner && !NebulaGameManager.Instance.CanSeeAllInfo) return;

            var chat = AmongUsLLImpl.HudManagerBridge.Chat;
            VColor color = Jailor.MyRole.Color;
            string tag = Language.Translate("chat.jailortext");
            GamePlayer sender = message.sender;

            if (sender == message.jailor && GamePlayer.LocalPlayer == ability.jailed && !NebulaGameManager.Instance.CanSeeAllInfo)
                chat.AddCustomChat(sender.VanillaPlayer, ability.jailed.VanillaPlayer, $"{(Jailor.MyRole as DefinedAssignable).DisplayName}({tag})".Color(color), message.text);
            else
                chat.AddCustomChat(sender.VanillaPlayer, sender.VanillaPlayer, $"{sender.Name}{$"({tag})".Color(color)}", message.text);

            if (message.text.IndexOf("who", StringComparison.OrdinalIgnoreCase) >= 0)
                Assets.CoreScripts.UnityTelemetry.Instance.SendWho();
        });
    }
}