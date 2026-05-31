using Image = Virial.Media.Image;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DHMO.Roles;

[NebulaRPCHolder]
public class Raven : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder, IAssignableDocument
{
    public static readonly Team RavenTeam = new("teams.raven", new(49, 36, 82), TeamRevealType.OnlyMe);
    private Raven() : base("raven", RavenTeam.Color, RoleCategory.NeutralRole, RavenTeam,
        [KillCooldown, HasDeadBodyArrow, new GroupConfiguration("options.role.raven.group.raventime", [RavenTimeOption, RavenTimeAliveNum, RavenTimeDuration, MeetingEndEnterRavenTimeDisperse], RavenTeam.Color.ToUnityColor().RGBMultiplied(0.65f))]){}

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration KillCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.raven.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly BoolConfiguration HasDeadBodyArrow = NebulaAPI.Configurations.Configuration("options.role.raven.hasDeadBodyArrow", true);
    public static readonly BoolConfiguration RavenTimeOption = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTime", true);
    public static readonly IntegerConfiguration RavenTimeAliveNum = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTimeAlived", (2, 24), 4, () => RavenTimeOption);
    public static readonly FloatConfiguration RavenTimeDuration = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTimeDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second, () => RavenTimeOption);
    public static readonly BoolConfiguration MeetingEndEnterRavenTimeDisperse = NebulaAPI.Configurations.Configuration("options.role.raven.meetingEndEnterRavenTimeDisperse", true, () => RavenTimeOption);

    public static readonly Raven MyRole = new();

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
        yield return new("%RAVENTIME%", Language.Translate(RavenTimeOption ? "role.raven.ability.ravenTime" : ""));
        yield return new("%NUM%", RavenTimeAliveNum.GetValue().ToString());
        yield return new("%DURATION%", RavenTimeDuration.GetValue().ToString());
    }

    private static Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("RavenMeetingButton.png")?.AsImage(150f);
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RavenIcon.png")?.AsImage(120f);
    (Virial.Color mainColor, Virial.Color? subColor)? DefinedAssignable.IconColor => (RavenTeam.Color, Impostor.MyTeam.Color);
    public static TranslatableTag missing = new("state.missing");

    public static RemoteProcess RavenStartDisperseRpc = new("RavenStartDisperseRpc", (_) =>
    {
        var player = GamePlayer.LocalPlayer;
        var vanillaplayer = PlayerControl.LocalPlayer;
        if (player is null || vanillaplayer is null || player.IsDead || player.IsDisconnected) return;

        AmongUsUtil.PlayFlash(MyRole.RoleColor.ToUnityColor());

        if (Minigame.Instance)
        {
            try { Minigame.Instance.Close(); } catch { }
        }

        if (vanillaplayer.inVent)
        {
            vanillaplayer.MyPhysics.RpcExitVent(Vent.currentVent.Id);
            vanillaplayer.MyPhysics.ExitAllVents();
        }

        var mapId = (int)AmongUsUtil.CurrentMapId;
        var cand = NebulaPreSpawnLocation.Locations[mapId];
        if (cand.Length == 0) cand = NebulaPreSpawnLocation.Locations[mapId].Where(l => l.VanillaIndex != null).ToArray();

        var list = cand.Select(p => p.Position!.Value).ToList();
        vanillaplayer.NetTransform.RpcSnapTo(list[Random.Range(0, list.Count)]);

        if (vanillaplayer.walkingToVent)
        {
            vanillaplayer.inVent = false;
            Vent.currentVent = null;
            vanillaplayer.moveable = true;
            vanillaplayer.MyPhysics.StopAllCoroutines();
        }
    });

    bool DefinedRole.IsKiller => true;
    private static Coroutine? FlashCoroutine;

    public static IEnumerator CoRavenTimeFlash()
    {
        var hud = Object.FindObjectOfType<HudManager>();
        if (hud is null) yield break;

        var wait = new WaitForSeconds(1f);
        var light = false;
        hud.FullScreen.color = new Color(1f, 0f, 0f, 0.37254903f);

        while (true)
        {
            hud.FullScreen.gameObject.SetActive(!hud.FullScreen.gameObject.activeSelf);

            hud.lightFlashHandle ??= DestroyableSingleton<DualshockLightManager>.Instance.AllocateLight();
            hud.lightFlashHandle.color = new Color(1f, 0f, 0f, 1f);
            hud.lightFlashHandle.intensity = 1f;

            light = !light;
            var currentColor = hud.lightFlashHandle.color;
            currentColor.a = light ? 1f : 0f;
            hud.lightFlashHandle.color = currentColor;

            yield return wait;
        }
    }

    public static void StopRavenTimeFlash()
    {
        if (FlashCoroutine is null) return;

        DestroyableSingleton<HudManager>.Instance.StopCoroutine(FlashCoroutine);
        DestroyableSingleton<HudManager>.Instance.FullScreen.gameObject.SetActive(false);
        FlashCoroutine = null;
        DestroyableSingleton<HudManager>.Instance.lightFlashHandle?.Dispose();
        DestroyableSingleton<HudManager>.Instance.lightFlashHandle = null;
    }

    [NebulaRPCHolder]
    public class Instance(Player player) : RuntimeAssignableTemplate(player), RuntimeRole, RuntimeAssignable, ILifespan, IBindPlayer, IGameOperator, IReleasable
    {
        public static bool IsInRavenTime;
        public DeadbodyArrowAbility? ArrowAbility { get; private set; }
        DefinedRole RuntimeRole.Role => MyRole;
        bool RuntimeRole.CanUseVent => true;
        public static GameEnd? RavenTeamWin = NebulaAPI.Preprocessor?.CreateEnd("raven", MyRole.RoleColor);

        void BlockTriggerEnd(EndCriteriaPreMetEvent ev)
        {
            if (ev.GameEnd != NebulaGameEnd.LoversWin && ev.GameEnd != RavenTeamWin && !MyPlayer.IsDead && ev.EndReason == GameEndReason.Situation)
                ev.Reject();
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            var totalAlive = AddonHelper.GetAlivePlayers().totalAlive;
            ev.SetWinIf(ev.GameEnd == RavenTeamWin && !MyPlayer.IsDead && totalAlive <= 1);
        }

        [OnlyHost]
        void WinCheck(GameUpdateEvent ev)
        {
            try
            {
                var totalAlive = AddonHelper.GetAlivePlayers().totalAlive;
                if (!MyPlayer.IsDead && totalAlive <= 1)
                    NebulaAPI.CurrentGame?.TriggerGameEnd(RavenTeamWin!, GameEndReason.Situation, BitMasks.AsPlayer().Add(MyPlayer));
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
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
            ev.Cancel(false);
        }

        void OnCheckKill(PlayerCheckCanKillLocalEvent ev)
        {
            if (!IsInRavenTime || ev.Target.RealPlayer.PlayerId != MyPlayer.PlayerId) return;
            ev.SetAsCannotKillForcedly();
        }

        private static ModAbilityButton? killButton, meetingButton, meetingKillButton;
        private Coroutine? coroutine;
        private static float RavenTimeLeft;
        private bool even;

        void AppendTaskPanel(PlayerTaskTextLocalEvent ev)
        {
            if (IsInRavenTime)
            {
                FlashCoroutine ??= DestroyableSingleton<HudManager>.Instance.StartCoroutine(CoRavenTimeFlash());
                even = !even;
                var color = even ? Color.yellow : Color.red;
                ev.AppendText(Language.Translate("role.raven.raventime").Replace("%TIME%", Mathf.Ceil(RavenTimeLeft).ToString()).Color(color));
                if (!MyPlayer.IsDead) MyPlayer.VanillaPlayer.Visible = true;
            }
            else
                StopRavenTimeFlash();
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            if (NebulaGameManager.Instance is null || !RavenTimeOption) return;

            var aliveCount = NebulaGameManager.Instance.AllPlayerInfo.Count(p => !p.IsDead);
            if (aliveCount <= RavenTimeAliveNum && !IsInRavenTime && !MyPlayer.IsDead)
            {
                if (!MeetingHud.Instance && !ExileController.Instance)
                    SetRavenTime.Invoke(true);
            }

            if ((FlashCoroutine != null && !IsInRavenTime) || (IsInRavenTime && MeetingHud.Instance) || (IsInRavenTime && aliveCount > RavenTimeAliveNum) || MyPlayer.IsDead)
            {
                SetRavenTime.Invoke(false);
            }

            if (IsInRavenTime)
            {
                DestroyableSingleton<HudManager>.Instance.StopOxyFlash();
                DestroyableSingleton<HudManager>.Instance.StopReactorFlash();
                RavenTimeLeft -= Time.deltaTime;
                if (RavenTimeLeft <= 0f && MyPlayer.AmOwner)
                {
                    MyPlayer.Suicide(PlayerStates.Suicide, EventDetails.Kill, KillParameter.NormalKill);
                    SetRavenTime.Invoke(false);
                }
            }
        }

        private TextMeshPro? tmPro;
        private DefinedRole? targetRole = null;

        private IEnumerator CoLeaveOrJoinMeeting(bool isleaving)
        {
            if (Minigame.Instance)
                Minigame.Instance.ForceClose();
            if (AmongUsUtil.MapIsOpen)
                MapBehaviour.Instance.Close();
            if (DestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen)
                DestroyableSingleton<HudManager>.Instance.GameMenu.Close();

            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new Vector3(isleaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isleaving;

            if (isleaving)
                RpcCamouflage.Invoke((MyPlayer, true));
            else
                RpcCamouflage.Invoke((MyPlayer, false));

            if (isleaving && tmPro == null)
            {
                var textHolder = UnityHelper.CreateObject("RavenTarget", DestroyableSingleton<HudManager>.Instance.transform, Vector3.zero, LayerExpansion.GetUILayer());
                this.BindGameObject(textHolder);

                if (NebulaGUIWidgetEngine.API != null)
                {
                    var textAttribute = new TextAttribute(NebulaGUIWidgetEngine.API.GetAttribute(AttributeParams.StandardBaredBoldLeftNonFlexible))
                    {
                        Alignment = Virial.Text.TextAlignment.Top,
                        FontSize = new FontSize(1.6f, true),
                        Size = new Size(3f, 1f)
                    };

                    var noSGUIText = new NoSGUIText(GUIAlignment.Bottom, textAttribute, new RawTextComponent(""))
                    {
                        PostBuilder = t => tmPro = t
                    };

                    var instantiatedObj = noSGUIText.Instantiate(new Anchor(new Virial.Compat.Vector2(0f, 0f), new Virial.Compat.Vector3(-0.5f, -0.5f, 0f)),new Size(20f, 20f),out _);
                    instantiatedObj?.transform.SetParent(textHolder.transform, false);
                }

                if (GameOperatorManager.Instance is null) yield break;
                GameOperatorManager.Instance.Subscribe<GameUpdateEvent>(ev =>
                {
                    if (!tmPro) return;
                    if (AddonHelper.IsOutMeeting() && !killed)
                    {
                        if (NebulaGameManager.Instance is null) return;

                        if (targetRole == null || !NebulaGameManager.Instance.AllPlayerInfo.Any(p => !p.IsDead && targetRole.Id == p.Role.Role.Id))
                        {
                            var allAlive = NebulaGameManager.Instance.AllPlayerInfo.Where(p => !p.IsDead && p.Role.Role is not Raven).ToList();
                            targetRole = allAlive.Count > 0 ? allAlive[Random.Range(0, allAlive.Count)].Role.Role : null;
                        }

                        tmPro?.gameObject.SetActive(true);
                        var iconTag = targetRole != null ? targetRole.GetRoleIconTag() : "";
                        tmPro?.UseRoleIcon();
                        tmPro?.text = Language.Translate("role.raven.killtarget").Replace("%ROLE%", iconTag + (targetRole?.DisplayColoredName ?? ""));
                        tmPro?.transform.localPosition = new Vector3(-0.07f, -2.45f, 0f);
                    }
                    else if ((MyPlayer.IsDead && AddonHelper.IsOutMeeting()) || !AddonHelper.IsOutMeeting() || killed)
                        tmPro?.gameObject.SetActive(false);
                }, this);
            }

            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.black, Color.clear, 1f, false);
            coroutine = null;
        }

        private bool killed;

        void RuntimeAssignable.OnActivated()
        {
            IsInRavenTime = false;
            RavenTimeLeft = RavenTimeDuration;

            GameOperatorManager.Instance?.Subscribe<PlayerDieOrDisconnectEvent>(ev =>
            {
                if (ev.Player == MyPlayer && AddonHelper.IsOutMeeting())
                    NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
            }, this);
            GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
            {
                IsInRavenTime = false;
                RavenTimeLeft = RavenTimeDuration;
            }, this);
            GameOperatorManager.Instance?.Subscribe<MeetingPreStartEvent>(ev =>
            {
                RpcCamouflage.Invoke((MyPlayer, true));
            }, this);
            GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev =>
            {
                RpcCamouflage.Invoke((MyPlayer, false));
            }, this);
            GameOperatorManager.Instance?.Subscribe<PlayerUpdateVisibilityEvent>(ev =>
            {
                if (IsInRavenTime && ev.Player == MyPlayer)
                    ev.SetInvisible();
            }, this);
            GameOperatorManager.Instance?.RegisterOnReleased(() =>
            {
                IsInRavenTime = false;
                RavenTimeLeft = RavenTimeDuration;
            }, this);

            if (AmOwner)
            {
                IgnoreBubble.IsIgnore = p => p.PlayerId == MyPlayer.PlayerId && IsInRavenTime;

                if (HasDeadBodyArrow)
                {
                    ArrowAbility = new DeadbodyArrowAbility();
                    ArrowAbility.Bind(this);
                    ArrowAbility.RegisterSelf();
                    GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => ArrowAbility.ShowArrow = !IsInRavenTime && !MyPlayer.IsDead, this);
                }

                var modUseButton = new ModAbilityButtonImpl(alwaysShow: true).Register(this).KeyBind(NebulaInput.GetInput(VirtualKeyInput.Use));
                modUseButton.Visibility = _ => !MyPlayer.IsDead && AddonHelper.IsOutMeeting();
                modUseButton.Availability = _ => MyPlayer.CanMove && MyPlayer.VanillaPlayer.closest != null && coroutine == null;
                modUseButton.OnClick = _ =>
                {
                    if (MyPlayer.VanillaPlayer.closest != null)
                        MyPlayer.VanillaPlayer.UseClosest();
                };
                modUseButton.OnUpdate = _ =>
                {
                    if (NebulaAPI.CurrentGame is null || DestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings is null) return;
                    ImageNames imageNames = DestroyableSingleton<HudManager>.Instance.UseButton.currentTarget.UseIcon;
                    if (!DestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings.ContainsKey(imageNames) || MyPlayer.VanillaPlayer.closest == null)
                    {
                        imageNames = ImageNames.UseButton;
                    }
                    var settings = DestroyableSingleton<HudManager>.Instance.UseButton.fastUseSettings[imageNames];
                    if (settings != null)
                    {
                        modUseButton.SetSprite(settings.Image);
                        modUseButton.VanillaButton.graphic.SetCooldownNormalizedUvs();
                        modUseButton.VanillaButton.buttonLabelText.fontSharedMaterial = settings.FontMaterial;
                        modUseButton.VanillaButton.buttonLabelText.text = DestroyableSingleton<TranslationController>.Instance.GetString(settings.Text, []);
                    }
                };

                var mkillTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.LocalKillablePredicate(p) && AddonHelper.IsOutMeeting(), null, false, false);

                meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction)
                    .SetImage(buttonImage!)
                    .SetColorLabel(MyRole.RoleColor);
                meetingButton.Visibility = _ => !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                meetingButton.Availability = _ => !killed && AddonHelper.ModAbilityMeetingButton() && coroutine == null;
                meetingButton.OnClick = _ =>
                {
                    if (killed)
                    {
                        if (AddonHelper.IsOutMeeting())
                            NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                        return;
                    }
                    if (coroutine != null) return;
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(!AddonHelper.IsOutMeeting()).WrapToIl2Cpp());
                };
                meetingButton.OnUpdate = _ =>
                {
                    if (AddonHelper.IsOutMeeting() && MeetingHudExtension.VotingTimer <= 5f && coroutine == null)
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                    meetingButton.SetLabel(AddonHelper.IsOutMeeting() ? "raven.returnmeeting" : "raven.leavemeeting");
                };

                meetingKillButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.Kill)
                    .SetLabel("kill")
                    .SetLabelType(ModAbilityButton.LabelType.Impostor);
                meetingKillButton.Visibility = _ => AddonHelper.ModAbilityMeetingButton() && AddonHelper.IsOutMeeting();
                meetingKillButton.Availability = _ => !killed && mkillTracker.CurrentTarget != null && AddonHelper.IsOutMeeting() && !MyPlayer.IsDead && coroutine == null;
                meetingKillButton.OnClick = _ =>
                {
                    killed = true;
                    var p = mkillTracker.CurrentTarget;
                    if (p != null && targetRole != null && p.Role.Role == targetRole)
                        MyPlayer.MurderPlayer(p, missing, EventDetails.Kill, KillParameter.MeetingKill, KillCondition.TargetAlive);
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                };

                var killTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p) && !AddonHelper.IsOutMeeting(), null, false, false);
                killButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false)
                    .BindKey(VirtualKeyInput.Kill, null)
                    .SetLabel("kill")
                    .SetLabelType(ModAbilityButton.LabelType.Impostor);
                killButton.Visibility = _ => !MyPlayer.IsDead && (IsInRavenTime || !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.IsImpostor));
                killButton.Availability = _ => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown).SetAsKillCoolTimer().Start();
                killButton.OnClick = _ =>
                {
                    var target = killTracker.CurrentTarget;
                    if (target != null)
                    {
                        MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                        killButton.StartCoolDown();
                    }
                };
                killButton.StartCoolDown();
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }
        }

        void RuntimeAssignable.OnInactivated()
        {
            IsInRavenTime = false;
            if (AddonHelper.IsOutMeeting())
                NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
        }

        [OnlyMyPlayer]
        void OnRavenTimeStart(RavenTimeStartEvent _) => killButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown / 10f).SetAsKillCoolTimer().Start(null);

        [OnlyMyPlayer]
        void OnPlayerStepSound(PlayerCheckPlayFootSoundEvent ev) => ev.PlayFootSound &= !AddonHelper.IsOutMeeting();

        void CheckCanPushEmergencyButton(CheckCanPushEmergencyButtonEvent ev)
        {
            if (IsInRavenTime && !MeetingHud.Instance && !MyPlayer.IsDead)
                ev.DenyButton("role.raven.meetingButtonText");
        }

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if ((IsInRavenTime || AddonHelper.IsOutMeeting()) && !MyPlayer.IsDead)
                ev.UpdateSaturation(0f, true);
        }

        [Local]
        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            killed = false;
            targetRole = null;
        }

        void OnTaskStart(TaskPhaseRestartEvent ev)
        {
            if (IsInRavenTime && MeetingEndEnterRavenTimeDisperse)
                RavenStartDisperseRpc.Invoke();
        }

        [Local]
        void OnUpdateVisibility(PlayerUpdateVisibilityEvent ev)
        {
            if (AddonHelper.IsOutMeeting() && ev.Visibility == PlayerUpdateVisibilityEvent.VisibilityLevel.Invisible)
                ev.SetSemitransparent();
        }

        bool RuntimeRole.EyesightIgnoreWalls => true;

        public RemoteProcess<bool> SetRavenTime = new("SetRavenTimeRPC", (msg, _) =>
        {
            if (msg)
            {
                StopRavenTimeFlash();
                FlashCoroutine = DestroyableSingleton<HudManager>.Instance.StartCoroutine(CoRavenTimeFlash().WrapToIl2Cpp());
                GamePlayer.LocalPlayer?.GainAttribute(PlayerAttributes.Roughening, 0.5f, 20f, false, 0, "DHMO::Raven");
                NebulaAPI.RunEvent(new RavenTimeStartEvent());
                IsInRavenTime = msg;
                RavenTimeLeft = RavenTimeDuration;
            }
            else
            {
                StopRavenTimeFlash();
                IsInRavenTime = msg;
            }
        });

        public static RemoteProcess<(GamePlayer player, bool on)> RpcCamouflage = new("RavenCamouflage", (message, _) =>
        {
            if (NebulaGameManager.Instance is null || GamePlayer.LocalPlayer is null) return;
            if (GamePlayer.LocalPlayer == message.player || !GamePlayer.LocalPlayer.TryGetAbility<LucidDreamer.Ability>(out var _)) return;

            var tag = $"Raven{GamePlayer.LocalPlayer?.PlayerId}";
            if (message.on)
                message.player?.Unbox().AddOutfit(new OutfitCandidate(NebulaGameManager.Instance.UnknownOutfit, tag, 50, true));
            else
                message.player?.Unbox().RemoveOutfit(tag);
        });
    }

    public class RavenTimeStartEvent : Virial.Events.Event
    {
    }
}