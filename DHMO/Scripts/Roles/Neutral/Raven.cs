using Image = Virial.Media.Image;
using MeetingHudExtension = Nebula.Extensions.MeetingHudExtension;
using Random = UnityEngine.Random;

namespace DHMO.Roles.Neutral;

[NebulaRPCHolder]
public class Raven : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder, IAssignableDocument
{
    private Raven() : base("raven", AddonGameTags.RavenTeam.Color, RoleCategory.NeutralRole, AddonGameTags.RavenTeam,
        [KillCooldown, CanMultipleKills, MultipleKillsCount, HasDeadBodyArrow, new GroupConfiguration("options.role.raven.group.ravenHunt", [InvokeRavenHunt, RavenHuntAliveNum, RavenHuntDuration, TaskPhaseRestartHuntTimeDisperse], AddonGameTags.RavenTeam.Color.RGBMultiplied(0.65f))])
    { }

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    private static readonly IRelativeCooldownConfiguration KillCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.raven.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    private static readonly BoolConfiguration CanMultipleKills = NebulaAPI.Configurations.Configuration("options.role.raven.canMultipleKills", false);
    internal static readonly IntegerConfiguration MultipleKillsCount = NebulaAPI.Configurations.Configuration("options.role.raven.multipleKillsCount", (2, 5), 3, () => CanMultipleKills);
    private static readonly BoolConfiguration HasDeadBodyArrow = NebulaAPI.Configurations.Configuration("options.role.raven.hasDeadBodyArrow", true);
    internal static readonly BoolConfiguration InvokeRavenHunt = NebulaAPI.Configurations.Configuration("options.role.raven.ravenHunt", true);
    internal static readonly IntegerConfiguration RavenHuntAliveNum = NebulaAPI.Configurations.Configuration("options.role.raven.ravenHuntAlive", (2, 23), 4, () => InvokeRavenHunt);
    internal static readonly FloatConfiguration RavenHuntDuration = NebulaAPI.Configurations.Configuration("options.role.raven.ravenHuntDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second, () => InvokeRavenHunt);
    internal static readonly BoolConfiguration TaskPhaseRestartHuntTimeDisperse = NebulaAPI.Configurations.Configuration("options.role.raven.taskPhaseRestartEnterRavenHuntDisperse", true, () => InvokeRavenHunt);

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
        yield return new("%RAVENHUNT%", InvokeRavenHunt ? Language.Translate("role.raven.ability.ravenHunt") : "");
        yield return new("%NUM%", RavenHuntAliveNum.GetValue().ToString());
        yield return new("%DURATION%", RavenHuntDuration.GetValue().ToString());
    }

    private readonly static Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/RavenMeetingButton.png")?.AsImage(150f);
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/RavenIcon.png")?.AsImage(120f);
    (VColor mainColor, VColor? subColor)? DefinedAssignable.IconColor => (MyRole.Color, NebulaTeams.ImpostorTeam.Color);

    [NebulaRPCHolder]
    public class Instance(Player player) : RuntimeAssignableTemplate(player), RuntimeRole, RuntimeAssignable, ILifespan, IBindPlayer, IGameOperator, IReleasable
    {
        public DeadbodyArrowAbility? ArrowAbility { get; private set; }
        DefinedRole RuntimeRole.Role => MyRole;
        IEnumerable<IPlayerAbility?> RuntimeAssignable.MyAbilities => [blockUsingUtilityAbility];

        private static ModAbilityButton? killButton, meetingKillButton;
        public static bool IsInRavenTime;
        private IPlayerAbility? blockUsingUtilityAbility;
        private Coroutine? coroutine;

        private TextMeshPro? tmPro;
        private DefinedRole? targetRole;
        private bool killed;

        void ClaimRavenTeamRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == AddonGameTags.RavenTeam) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }
        
        [OnlyHost]
        void CheckWin(GameUpdateEvent ev)
        {
            var totalAlive = APICompat.AlivePlayers.Length;
            if (NebulaAPI.RunEvent(new KillerTeamCallback(AddonGameTags.RavenTeam)).RemainingOtherTeam) return;

            if (MyPlayer.IsAlive && totalAlive <= 1)
                NebulaAPI.CurrentGame?.TriggerGameEnd(AddonGameTags.RavenEnd, GameEndReason.Situation, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
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
            var time = global::DHMO.Modules.TimeHunt.AllTimes.FirstOrDefault(t => t.Id == "raven");
            IsInRavenTime = (time?.IsRunning ?? false) && time.CanRunning;
        }

        int killCount = 0;

        void RuntimeAssignable.OnActivated()
        {
            IsInRavenTime = false;

            if (AmOwner)
            {
                blockUsingUtilityAbility = new BlockUsingUtilityAbility(MyPlayer, () => APICompat.IsOutMeeting()).Register(this);
                
                GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev => RpcCamouflage.Invoke((MyPlayer, false)), this);

                GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
                {
                    if (ev.Player == MyPlayer && APICompat.IsOutMeeting())
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
                meetingButton.Availability = _ => !killed && APICompat.ModAbilityMeetingButton() && coroutine == null && APICompat.AlivePlayers.Length > 1;
                meetingButton.OnClick = _ =>
                {
                    if (coroutine != null) return;
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(!APICompat.IsOutMeeting()).WrapToIl2Cpp());
                };
                meetingButton.OnUpdate = _ =>
                {
                    if (APICompat.IsOutMeeting() && MeetingHudExtension.VotingTimer <= 5f && coroutine == null)
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());

                    meetingButton.SetLabel(APICompat.IsOutMeeting() ? "raven.returnMeeting" : "raven.leaveMeeting");
                };

                var mkillTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.LocalKillablePredicate(p) && APICompat.IsOutMeeting(), null, false, false);

                meetingKillButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.Kill).SetLabel("kill").SetLabelType(ModAbilityButton.LabelType.Impostor);
                meetingKillButton.Visibility = _ => APICompat.ModAbilityMeetingButton() && APICompat.IsOutMeeting();
                meetingKillButton.Availability = _ => !killed && mkillTracker.CurrentTarget != null && APICompat.IsOutMeeting() && MyPlayer.IsAlive && killCount <= MultipleKillsCount && coroutine == null;
                meetingKillButton.OnClick = _ =>
                {
                    var p = mkillTracker.CurrentTarget;
                    if (p == null || targetRole == null) return;

                    bool isMatch = p.Role.Role == targetRole;
                    if (isMatch)
                    {
                        MyPlayer.MurderPlayer(p, AddonGameTags.Missing, null, KillParameter.MeetingKill, KillCondition.TargetAlive);
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
                killButton.Visibility = _ => MyPlayer.IsAlive && (IsInRavenTime || !GamePlayer.AllPlayers.Any(p => !p.IsDead && p.IsImpostor)) && !APICompat.IsOutMeeting();
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

        private IEnumerator CoLeaveOrJoinMeeting(bool isLeaving)
        {
            yield return AmongUsLLImpl.TryGetHudManager(out var hud);

            if (Minigame.Instance.AsBoolFast(out var minigame)) minigame.ForceClose();
            if (AmongUsUtil.MapIsOpen) MapBehaviour.Instance.Close();
            if (hud.GameMenu.IsOpen) hud.GameMenu.Close();

            yield return hud.CoFadeFullScreen(VColor.Clear.ToUnityColor(), VColor.Black.ToUnityColor(), 1f, false);
            MeetingHud.Instance.ModGameObject(false).LocalPosition = new VVector3(isLeaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isLeaving;

            if (isLeaving && tmPro == null)
            {
                var textHolder = UnityHelper.CreateObject("RavenTarget", hud.transform, VVector3.Zero);
                this.BindGameObject(textHolder);

                {
                    var textAttribute = new TextAttribute(NebulaGUIWidgetEngine.API.GetAttribute(AttributeParams.StandardBaredBoldLeftNonFlexible))
                    {
                        Alignment = Virial.Text.TextAlignment.Top,
                        FontSize = new FontSize(1.6f, true),
                        Size = new Size(3f, 1f)
                    };
                    var noSGUIText = new NoSGUIText(GUIAlignment.Bottom, textAttribute, new RawTextComponent("")) { PostBuilder = t => tmPro = t };
                    var instantiatedObj = noSGUIText.Instantiate(new Anchor(new VVector2(0f, 0f), new VVector3(-0.5f, -0.5f)), new Size(20f, 20f), out _)?.ModGameObject();
                    instantiatedObj?.GetUnityTransform().SetParent(textHolder.transform, false);
                }

                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
                {
                    if (!tmPro.AsBoolFast()) return;
                    var textObj = tmPro?.ModGameObject();

                    if (APICompat.IsOutMeeting() && !killed)
                    {
                        if (targetRole == null || !GamePlayer.AllPlayers.Any(p => p.IsAlive && targetRole.Id == p.Role.Role.Id))
                        {
                            var allAlive = GamePlayer.AllPlayers.Where(p => !p.IsDead && p.Role is not Raven.Instance).ToList();
                            targetRole = allAlive.Count > 0 ? allAlive[Random.Range(0, allAlive.Count)].Role.Role : null;
                        }
                        tmPro?.gameObject.SetActive(targetRole != null);

                        var iconTag = targetRole != null ? targetRole.GetRoleIconTag() : "";
                        tmPro?.UseRoleIcon();
                        tmPro?.text = Language.Translate("role.raven.killTarget").Replace("%ROLE%", iconTag + (targetRole?.DisplayColoredName ?? ""));
                        textObj?.LocalPosition = new VVector3(-0.07f, -2.45f, 0f);
                    }
                    else if ((MyPlayer.IsDead && APICompat.IsOutMeeting()) || !APICompat.IsOutMeeting() || killed)
                        textObj?.SetActive(false);
                }, this);
            }

            if (isLeaving) MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
            yield return hud.CoFadeFullScreen(VColor.Black.ToUnityColor(), VColor.Clear.ToUnityColor(), 1f, false);
            if (!isLeaving && !MyPlayer.IsDead) MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetPlayersLayer();
            coroutine = null;
        }

        void OnRavenTimeStart(TimeHuntStartEvent ev)
        {
            if (ev.TimeHunt.Id is "raven")
            {
                AmongUsLLImpl.SoundManagerInstance.CrossFadeSound("RavenHuntTime", DHMOAsset.ChildSingingScaryMelodyMusic, 0.5f);
                MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();

                GamePlayer.LocalPlayer?.GainAttribute(PlayerAttributes.Roughening, 0.5f, 20f, false, 0, "DHMO::Raven");
                killButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown / 10f).SetAsKillCoolTimer().Start();
            }
        }

        void OnRavenTimeEnd(TimeHuntEndEvent ev)
        {
            if (ev.TimeHunt.Id is not "raven") return;

            AmongUsLLImpl.SoundManagerInstance.CrossFadeSound("RavenHuntTime", null, 0.5f);
            if (ev.IsTimeOver && MyPlayer.IsAlive)
            {
                MyPlayer.Suicide(PlayerStates.Suicide, EventDetails.Kill, KillParameter.MeetingKill);
                if (MyPlayer.IsAlive) MyPlayer.VanillaPlayer.gameObject.layer = LayerExpansion.GetPlayersLayer();
            }
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if ((IsInRavenTime || APICompat.IsOutMeeting()) && MyPlayer.IsAlive)
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
            if (APICompat.IsOutMeeting() && ev.Visibility == PlayerUpdateVisibilityEvent.VisibilityLevel.Invisible)
                ev.SetSemitransparent();
        }

        bool RuntimeRole.EyesightIgnoreWalls => true;
        

        public static RemoteProcess<(GamePlayer player, bool add)> RpcCamouflage = new("RavenCamouflage", (message, b) =>
        {
            if (NebulaGameManager.Instance is null || GamePlayer.LocalPlayer is null) return;
            if (message.player.AmOwner || !GamePlayer.LocalPlayer.TryGetAbility<LucidDreamer.Ability>(out _)) return;

            var tag = $"Raven{GamePlayer.LocalPlayer.PlayerId}";
            if (message.add)
                message.player.Unbox().AddOutfit(new OutfitCandidate(NebulaGameManager.Instance.UnknownOutfit, tag, 50, false));
            else
                message.player.Unbox().RemoveOutfit(tag);
        });
    }
}

public class BlockUsingUtilityAbility : DependentLifespan,IPlayerAbility, IBindPlayer
{
    public Func<bool> Predicate { get; }
    public Player MyPlayer { get; }
    public bool BlockUsingUtility => Predicate.Invoke();

    public BlockUsingUtilityAbility(Player myPlayer, Func<bool> predicate)
    {
        Predicate = predicate;
        MyPlayer = myPlayer;
    }
}