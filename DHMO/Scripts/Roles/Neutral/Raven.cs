using Image = Virial.Media.Image;
using Random = UnityEngine.Random;

namespace DHMO.Roles.Neutral;

[NebulaRPCHolder]
public class Raven : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder, IAssignableDocument
{
    public static readonly Nebula.Roles.Team RavenTeam = new("teams.raven", new(49, 36, 82), TeamRevealType.OnlyMe);
    private Raven() : base("raven", RavenTeam.Color, RoleCategory.NeutralRole, RavenTeam,
        [KillCooldown, CanMultipleKills, MultipleKillsCount, HasDeadBodyArrow, new GroupConfiguration("options.role.raven.group.ravenTime", [InvokeRavenTime, RavenTimeAliveNum, RavenTimeDuration, TaskPhaseRestartRavenTimeDisperse], RavenTeam.Color.RGBMultiplied(0.65f))])
    { }

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    private static readonly IRelativeCooldownConfiguration KillCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.raven.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    private static readonly BoolConfiguration CanMultipleKills = NebulaAPI.Configurations.Configuration("options.role.raven.canMultipleKills", false);
    internal static readonly IntegerConfiguration MultipleKillsCount = NebulaAPI.Configurations.Configuration("options.role.raven.multipleKillsCount", (2, 5), 3, () => CanMultipleKills);
    private static readonly BoolConfiguration HasDeadBodyArrow = NebulaAPI.Configurations.Configuration("options.role.raven.hasDeadBodyArrow", true);
    internal static readonly BoolConfiguration InvokeRavenTime = NebulaAPI.Configurations.Configuration("options.role.raven.ravenTime", true);
    internal static readonly IntegerConfiguration RavenTimeAliveNum = NebulaAPI.Configurations.Configuration("options.role.raven.ravenTimeAlived", (2, 23), 4, () => InvokeRavenTime);
    internal static readonly FloatConfiguration RavenTimeDuration = NebulaAPI.Configurations.Configuration("options.role.raven.ravenTimeDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second, () => InvokeRavenTime);
    internal static readonly BoolConfiguration TaskPhaseRestartRavenTimeDisperse = NebulaAPI.Configurations.Configuration("options.role.raven.taskPhaseRestartEnterRavenTimeDisperse", true, () => InvokeRavenTime);

    public static readonly Raven MyRole = new();
    bool DefinedRole.IsKiller => true;

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonImage!, "role.raven.ability.leave");
        yield return new(SpriteLoader.FromResource("Nebula.Resources.Buttons.VanillaKillButton.png", 115f), "role.raven.ability.kill");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%RAVENTIME%", InvokeRavenTime ? Language.Translate("role.raven.ability.ravenTime") : "");
        yield return new("%NUM%", RavenTimeAliveNum.GetValue().ToString());
        yield return new("%DURATION%", RavenTimeDuration.GetValue().ToString());
    }

    private readonly static Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/RavenMeetingButton.png")?.AsImage(150f);
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/RavenIcon.png")?.AsImage(120f);
    (VColor mainColor, VColor? subColor)? DefinedAssignable.IconColor => (RavenTeam.Color, NebulaTeams.ImpostorTeam.Color);
    public static TranslatableTag missing = new("state.missing");

    [NebulaRPCHolder]
    public class Instance(Player player) : RuntimeAssignableTemplate(player), RuntimeRole, RuntimeAssignable, ILifespan, IBindPlayer, IGameOperator, IReleasable
    {
        public DeadbodyArrowAbility? ArrowAbility { get; private set; }
        DefinedRole RuntimeRole.Role => MyRole;

        public static GameEnd? RavenTeamWin = NebulaAPI.Preprocessor?.CreateEnd("raven", MyRole.RoleColor);

        private static ModAbilityButton? killButton, meetingKillButton;
        public static bool IsInRavenTime;
        private Coroutine? coroutine;

        private TextMeshPro? tmPro;
        private DefinedRole? targetRole;
        private bool killed;

        void ClaimRavenTeamRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Raven.RavenTeam) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        [OnlyHost]
        void CheckWin(GameUpdateEvent ev)
        {
            var totalAlive = AddonHelper.AlivePlayers.Length;
            if (NebulaAPI.RunEvent(new KillerTeamCallback(RavenTeam)).RemainingOtherTeam) return;

            if (MyPlayer.IsAlive && totalAlive <= 1 && RavenTeamWin is not null)
                NebulaAPI.CurrentGame?.TriggerGameEnd(RavenTeamWin, GameEndReason.Situation, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
        }

        [OnlyMyPlayer]
        void CheckKill(PlayerCheckKilledEvent ev)
        {
            if (!IsInRavenTime) return;
            if (ev.Killer.PlayerId == MyPlayer.PlayerId) return;
            if (ev.EventDetail == EventDetail.Bubbled || ev.EventDetail == EventDetail.Curse)
                NebulaManager.Instance.StartDelayAction(2f, () => MyPlayer.VanillaPlayer.moveable = true);

            ev.Result = KillResult.Rejected;
        }

        void OnKill(PlayerTryVanillaKillLocalEventAbstractPlayerEvent ev)
        {
            if (!IsInRavenTime || ev.Target.RealPlayer.PlayerId != MyPlayer.PlayerId) return;
            ev.Cancel();
        }

        void OnCheckKill(PlayerCheckCanKillLocalEvent ev)
        {
            if (IsInRavenTime && ev.Player == MyPlayer)
                ev.SetAsCanKillForcedly();

            if (!IsInRavenTime || ev.Target.RealPlayer.PlayerId != MyPlayer.PlayerId) return;
            ev.SetAsCannotKillForcedly();
        }

        void OnHudUpdate(GameHudUpdateEvent ev)
        {
            var time = TimeMoment.AllTimes.FirstOrDefault(t => t.Id == "raven");
            IsInRavenTime = (time?.IsRunning ?? false) && time.CanRunning;
        }

        int killCount = 0;

        void RuntimeAssignable.OnActivated()
        {
            IsInRavenTime = false;

            if (AmOwner)
            {
                GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev => RpcCamouflage.Invoke((MyPlayer, false)), this);

                GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
                {
                    if (ev.Player == MyPlayer && AddonHelper.IsOutMeeting())
                        NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                }, this);

                if (HasDeadBodyArrow)
                {
                    ArrowAbility = new DeadbodyArrowAbility();
                    ArrowAbility.RegisterSelf().Bind(this);
                    GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => ArrowAbility.ShowArrow = !IsInRavenTime && MyPlayer.IsAlive, this);
                }

                var meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction).SetImage(buttonImage!);
                meetingButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting;
                meetingButton.Availability = _ => !killed && AddonHelper.ModAbilityMeetingButton() && coroutine == null && AddonHelper.AlivePlayers.Length > 1;
                meetingButton.OnClick = _ =>
                {
                    if (coroutine != null) return;
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(!AddonHelper.IsOutMeeting()).WrapToIl2Cpp());
                };
                meetingButton.OnUpdate = _ =>
                {
                    if (AddonHelper.IsOutMeeting() && MeetingHudExtension.VotingTimer <= 5f && coroutine == null)
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());

                    meetingButton.SetLabel(AddonHelper.IsOutMeeting() ? "raven.returnmeeting" : "raven.leavemeeting");
                };

                var mkillTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.LocalKillablePredicate(p) && AddonHelper.IsOutMeeting(), null, false, false);

                meetingKillButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.Kill).SetLabel("kill").SetLabelType(ModAbilityButton.LabelType.Impostor);
                meetingKillButton.Visibility = _ => AddonHelper.ModAbilityMeetingButton() && AddonHelper.IsOutMeeting();
                meetingKillButton.Availability = _ => !killed && mkillTracker.CurrentTarget != null && AddonHelper.IsOutMeeting() && MyPlayer.IsAlive && killCount <= MultipleKillsCount && coroutine == null;
                meetingKillButton.OnClick = _ =>
                {
                    var p = mkillTracker.CurrentTarget;
                    if (p == null || targetRole == null) return;

                    bool isMatch = p.Role.Role == targetRole;
                    if (isMatch)
                    {
                        MyPlayer.MurderPlayer(p, missing, missing, KillParameter.MeetingKill, KillCondition.TargetAlive);
                        killCount++;
                    }

                    if (!CanMultipleKills || !isMatch)
                    {
                        killed = true;
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                    }
                };

                var killTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeLocalKillablePredicate(p), null, false, false);
                killButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false)
                    .BindKey(VirtualKeyInput.Kill, null).SetLabel("kill").SetLabelType(ModAbilityButton.LabelType.Impostor);
                killButton.Visibility = _ => MyPlayer.IsAlive && (IsInRavenTime || !GamePlayer.AllPlayers.Any(p => !p.IsDead && p.IsImpostor)) && !AddonHelper.IsOutMeeting();
                killButton.Availability = _ => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown).SetAsKillCoolTimer().Start();
                killButton.OnClick = _ =>
                {
                    var target = killTracker.CurrentTarget;
                    if (target != null)
                    {
                        var cancelable = NebulaAPI.RunEvent(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target));
                        if (!(cancelable?.IsCanceled ?? false))
                            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);

                        if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    }
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }
        }

        private IEnumerator CoLeaveOrJoinMeeting(bool isleaving)
        {
            yield return AmongUsLLImpl.TryGetHudManager(out var hud);

            if (Minigame.Instance.AsBoolFast(out var minigame)) minigame.ForceClose();
            if (AmongUsUtil.MapIsOpen) MapBehaviour.Instance.Close();
            if (hud.GameMenu.IsOpen) hud.GameMenu.Close();

            yield return hud.CoFadeFullScreen(VColor.Clear.ToUnityColor(), VColor.Black.ToUnityColor(), 1f, false);
            MeetingHud.Instance.ModGameObject(false).LocalPosition = new VVector3(isleaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isleaving;

            if (isleaving && tmPro == null)
            {
                var textHolder = UnityHelper.CreateObject("RavenTarget", hud.transform, VVector3.Zero, LayerExpansion.GetUILayer());
                this.BindGameObject(textHolder);

                if (NebulaGUIWidgetEngine.API != null)
                {
                    var textAttribute = new TextAttribute(NebulaGUIWidgetEngine.API.GetAttribute(AttributeParams.StandardBaredBoldLeftNonFlexible))
                    {
                        Alignment = Virial.Text.TextAlignment.Top,
                        FontSize = new FontSize(1.6f, true),
                        Size = new Size(3f, 1f)
                    };
                    var noSGUIText = new NoSGUIText(GUIAlignment.Bottom, textAttribute, new RawTextComponent("")) { PostBuilder = t => tmPro = t };
                    var instantiatedObj = noSGUIText.Instantiate(new Anchor(new VVector2(0f, 0f), new VVector3(-0.5f, -0.5f, 0f)), new Size(20f, 20f), out _)?.ModGameObject();
                    instantiatedObj?.GetUnityTransform().SetParent(textHolder.transform, false);
                }

                if (GameOperatorManager.Instance is null) yield break;
                GameOperatorManager.Instance.Subscribe<GameUpdateEvent>(ev =>
                {
                    if (!tmPro) return;
                    var textObj = tmPro?.ModGameObject();

                    if (AddonHelper.IsOutMeeting() && !killed)
                    {
                        if (NebulaGameManager.Instance is null) return;
                        if (targetRole == null || !GamePlayer.AllPlayers.Any(p => p.IsAlive && targetRole.Id == p.Role.Role.Id))
                        {
                            var allAlive = GamePlayer.AllPlayers.Where(p => !p.IsDead && p.Role is not Raven.Instance).ToList();
                            targetRole = allAlive.Count > 0 ? allAlive[Random.Range(0, allAlive.Count)].Role.Role : null;
                        }
                        tmPro?.gameObject.SetActive(true);

                        var iconTag = targetRole != null ? targetRole.GetRoleIconTag() : "";
                        tmPro?.UseRoleIcon();
                        tmPro?.text = Language.Translate("role.raven.killtarget").Replace("%ROLE%", iconTag + (targetRole?.DisplayColoredName ?? ""));
                        textObj?.LocalPosition = new VVector3(-0.07f, -2.45f, 0f);
                    }
                    else if ((MyPlayer.IsDead && AddonHelper.IsOutMeeting()) || !AddonHelper.IsOutMeeting() || killed)
                        textObj?.SetActive(false);
                }, this);
            }

            yield return hud.CoFadeFullScreen(VColor.Black.ToUnityColor(), VColor.Clear.ToUnityColor(), 1f, false);
            coroutine = null;
        }

        void OnRavenTimeStart(TimeMomentStartEvent ev)
        {
            if (ev.TimeMoment.Id == "raven")
            {
                GamePlayer.LocalPlayer?.GainAttribute(PlayerAttributes.Roughening, 0.5f, 20f, false, 0, "DHMO::Raven");
                killButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown / 10f).SetAsKillCoolTimer().Start();
            }
        }

        void OnRavenTimeEnd(TimeMomentEndEvent ev)
        {
            if (ev.IsTimeOver && MyPlayer.IsAlive)
                MyPlayer.Suicide(PlayerStates.Suicide, EventDetails.Kill, KillParameter.NormalKill | ~KillParameter.WithDeadBody);
        }

        [OnlyMyPlayer]
        void CheckPlayerStepSound(PlayerCheckPlayFootSoundEvent ev) => ev.PlayFootSound &= !AddonHelper.IsOutMeeting();

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if ((IsInRavenTime || AddonHelper.IsOutMeeting()) && MyPlayer.IsAlive)
                ev.UpdateSaturation(0f, true);
        }

        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            RpcCamouflage.Invoke((MyPlayer, true));
            if (MyPlayer.AmOwner)
            {
                killCount = 0;
                killed = false;
                targetRole = null;
            }
        }

        void OnUpdateVisibility(PlayerUpdateVisibilityEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && ev.Visibility == PlayerUpdateVisibilityEvent.VisibilityLevel.Invisible)
                ev.SetSemitransparent();
        }

        bool RuntimeRole.EyesightIgnoreWalls => true;

        public static RemoteProcess<(GamePlayer player, bool add)> RpcCamouflage = new("RavenCamouflage", (message, b) =>
        {
            if (NebulaGameManager.Instance is null || GamePlayer.LocalPlayer is null) return;
            if (message.player.AmOwner || !GamePlayer.LocalPlayer.TryGetAbility<LucidDreamer.Ability>(out _)) return;

            var tag = $"Raven{GamePlayer.LocalPlayer.PlayerId}";
            if (message.add)
                message.player?.AddOutfit(new OutfitCandidate(NebulaGameManager.Instance.UnknownOutfit, tag, 50, false));
            else
                message.player?.RemoveOutfitByTag(tag);
        });
    }
}