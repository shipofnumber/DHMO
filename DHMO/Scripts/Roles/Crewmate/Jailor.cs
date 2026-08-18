using Image = Virial.Media.Image;

namespace DHMO.Roles.Crewmate;

public class Jailor : DefinedSingleAbilityRoleTemplate<Jailor.Ability>, DefinedRole, HasCitation, IAssignableDocument
{
    private Jailor() : base("jailor", new(166, 166, 166), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [JailCooldownOption, JailDurationOption, JailInARow, LimitAbilityUsesOption, NumOfJailOption, NumOfExecuteOption, MaxLeftVotingTimeForExecuting, CanExecuteLoversOption, HasPrivateChat])
    { }

    static private readonly FloatConfiguration JailCooldownOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailCooldown", (5f, 30f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration JailDurationOption = NebulaAPI.Configurations.Configuration("options.role.jailor.jailDuration", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second);
    static public readonly BoolConfiguration JailInARow = NebulaAPI.Configurations.Configuration("options.role.jailor.jailinArow", false);
    private static readonly ValueConfiguration<int> LimitAbilityUsesOption = NebulaAPI.Configurations.Configuration("options.role.jailor.limitAbilityUsesOf", ["options.role.jailor.limitAbilityUsesOf.jail", "options.role.jailor.limitAbilityUsesOf.execute"], 0);
    static private readonly IntegerConfiguration NumOfJailOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfjail", (1, 10), 5, () => LimitAbilityUsesOption.GetValue() == 0);
    static private readonly IntegerConfiguration NumOfExecuteOption = NebulaAPI.Configurations.Configuration("options.role.jailor.numOfexecute", (1, 5), 3, () => LimitAbilityUsesOption.GetValue() == 1);
    static internal readonly FloatConfiguration MaxLeftVotingTimeForExecuting = NebulaAPI.Configurations.Configuration("options.role.jailor.maxLeftVotingTimeForExecuting", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration CanExecuteLoversOption = NebulaAPI.Configurations.Configuration("options.role.jailor.canExecuteLovers", false);
    static public readonly BoolConfiguration HasPrivateChat = NebulaAPI.Configurations.Configuration("options.role.jailor.hasJailorchat", true, () => NebulaAPI.GetAddon("Plan17ResourcesPlana") != null);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, GetAbilityUses()));

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
    }

    public Citation? Citation => DHMOCitations.TownOfUsMira;

    private static bool IsLimiltJailUses() => LimitAbilityUsesOption.GetValue() == 0;
    private static int GetAbilityUses() => IsLimiltJailUses() ? NumOfJailOption : NumOfExecuteOption;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IBindPlayer, IGameOperator, ILifespan
    {
        ModAbilityButton? jailButton, executeButton;
        public GamePlayer? Jailed { get; private set; } = null;
        EditableBitMask<GamePlayer> hasJailed = BitMasks.AsPlayer();

        bool isMissed;
        PoolablePlayer? jailIcon = null;
        private GameObject? jailCell;
        private int leftUses = GetAbilityUses();
        int[] IPlayerAbility.AbilityArguments => [leftUses, IsUsurped.AsInt()];

        public Ability(GamePlayer player, bool isUsurped, int uses) : base(player, isUsurped)
        {
            this.leftUses = uses;

            if (HasPrivateChat)
            {
                PrivateChat.RegisterPublicChannel(MyRole.Color, MyRole.Color, $"Jailor{MyPlayer.PlayerId}", Language.Translate("chat.jailortext"), this,
                    () => AmongUsUtil.InMeeting && Jailed != null && Jailed.IsAlive && (MyPlayer.AmOwner || Jailed.AmOwner), true,
                    text => RpcSendChat.Invoke((MyPlayer, GamePlayer.LocalPlayer ?? MyPlayer, text)));
            }

            if (AmOwner)
            {
                ObjectTracker<GamePlayer> tracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), null, false, false);

                jailButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, VirtualKeyInput.Ability, JailCooldownOption, JailDurationOption, "jailor.jail", jailImage)
                    .SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.UnityColor);
                jailButton.Availability = button => MyPlayer.CanMove && tracker.CurrentTarget != null;
                jailButton.Visibility = button => MyPlayer.IsAlive && leftUses > 0;

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

                            var outfit = tracker.CurrentTarget.GetOutfit(OutfitPriority.TransformedThrethold);
                            jailIcon?.Destroy();
                            if (outfit == null) return;
                            jailIcon = AmongUsUtil.GetPlayerIcon(outfit.outfit, ((ModAbilityButtonImpl)button).VanillaButton.transform, new VVector3(0.4f, -0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
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
                executeButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && leftUses > 0 && Jailed != null;
                executeButton.Availability = _ => Jailed != null && Jailed.IsAlive && APICompat.ModAbilityMeetingButton() && MeetingHudExtension.VotingTimer >= MaxLeftVotingTimeForExecuting && MyPlayer.CanKill(Jailed);
                executeButton.SetImage(executeImage!);

                executeButton.OnClick = button =>
                {
                    if (Jailed == null) return;
                    MyPlayer.MurderPlayer(Jailed, execution, execution, KillParameter.MeetingKill, KillCondition.TargetAlive);
                };

                if (IsLimiltJailUses())
                    jailButton.ShowUsesIcon(3, leftUses.ToString());
                else
                    executeButton.ShowUsesIcon(3, leftUses.ToString());
            }
        }

        void EditGuessable(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (ev.Guesser == MyPlayer && hasJailed.Test(ev.Target) && ev.Target.IsAlive)
                ev.CanGuess = false;
        }

        private bool CanExecute(GamePlayer target)
        {
            bool canExecute;
            if (target.Role.Role == Madmate.MyRole) canExecute = true;
            else if (target.Role is JekyllAndHyde.Instance jah && !jah.AmJekyll) canExecute = true;
            else if (target.TryGetModifier<Lover.Instance>(out _) && CanExecuteLoversOption) canExecute = true;
            else if (target.Role.Role.Category == RoleCategory.CrewmateRole) canExecute = false;
            else canExecute = true;

            return GameOperatorManager.Instance?.Run(new SheriffCheckKillEvent(MyPlayer, target, canExecute)).CanKill ?? canExecute;
        }

        [Local]
        void CheckPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Dead.PlayerId != Jailed?.PlayerId || !AmongUsUtil.InMeeting) return;
            if (ev.Murderer == MyPlayer && ev.Dead.PlayerState == execution)
            {
                var canExecute = this.CanExecute(ev.Dead);

                if (!canExecute && MyPlayer.IsTrueCrewmate)
                {
                    leftUses = 0;
                    isMissed = true;
                    AmongUsUtil.PlayFlash(VColor.Red);
                    RpcInsulate.Invoke(MyPlayer);
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
            if (ev.Player == MyPlayer || ev.Player == Jailed)
                Jailed = null;
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            Clear();
            if (Jailed == null) return;

            if (AmOwner && isMissed) RpcInsulate.Invoke(MyPlayer);
            MeetingHudExtension.AddSealedMask(1 << Jailed.PlayerId);
            if (Jailed.AmOwner) MeetingHudExtension.CanUseAbility = false;

            foreach (var voteArea in MeetingHud.Instance.playerStates.GetFastEnumerator())
            {
                if (Jailed.PlayerId == voteArea.TargetPlayerId)
                {
                    var voteAreaTransform = voteArea.ModGameObject().GetUnityTransform();
                    GenCell(voteAreaTransform);
                }
            }
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (Jailed == null) return;

            if (!JailInARow)
            {
                Jailed = null;
                if (AmOwner) jailIcon?.Destroy();
                return;
            }

            if (!AmOwner) return;
            if (IsLimiltJailUses() && leftUses > 0)
            {
                leftUses--;
                jailButton?.UpdateUsesIcon(leftUses.ToString());
            }
        }

        public void Clear()
        {
            if (jailCell.AsBoolFast(out var cell)) cell.Destroy();
        }

        internal void GenCell(Transform voteArea)
        {
            var cellRenderer = UnityHelper.CreateSpriteRenderer("JailCell", voteArea, new VVector3(-0.95f, 0f, -2f), 5);
            cellRenderer.sprite = injailImage?.GetSprite();
            cellRenderer.ModGameObject(false).LocalScale = new VVector3(0.6f, 0.6f, 0.6f);
            jailCell = cellRenderer.gameObject;
        }

        private static readonly RemoteProcess<GamePlayer> RpcInsulate = new("InsulateJailor", (msg, _) => MeetingHudExtension.RemoveCanVoteMask(1 << msg.PlayerId));

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer jailed)> RpcJail = new("JailorJail", (message, _) =>
        {
            if (!message.jailor.TryGetAbility<Jailor.Ability>(out var ability)) return;
            ability.Jailed = message.jailed;
        });

        private static readonly RemoteProcess<(GamePlayer jailor, GamePlayer sender, string text)> RpcSendChat = new("JailorSendChat", (message, _) =>
        {
            if (string.IsNullOrEmpty(message.text) || NebulaGameManager.Instance == null || !message.jailor.TryGetAbility<Jailor.Ability>(out var ability) || ability.Jailed == null) return;
            if (!message.jailor.AmOwner && !ability.Jailed.AmOwner && !NebulaGameManager.Instance.CanSeeAllInfo) return;

            var chat = AmongUsLLImpl.HudManagerBridge.Chat;
            string tag = Language.Translate("chat.jailor").Color(Jailor.MyRole.Color);
            GamePlayer sender = message.sender;

            if (sender == message.jailor && ability.Jailed.AmOwner && !NebulaGameManager.Instance.CanSeeAllInfo)
                chat.AddCustomChat(sender.VanillaPlayer, ability.Jailed.VanillaPlayer, $"{(Jailor.MyRole as DefinedAssignable).DisplayColoredName}{tag}", message.text);
            else
                chat.AddCustomChat(sender.VanillaPlayer, sender.VanillaPlayer, $"{sender.Name}{tag}", message.text);

            if (message.text.IndexOf("who", StringComparison.OrdinalIgnoreCase) >= 0)
                Assets.CoreScripts.UnityTelemetry.Instance.SendWho();
        });
    }
}