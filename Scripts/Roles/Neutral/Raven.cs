using Image = Virial.Media.Image;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DHMO.Roles;

[NebulaRPCHolder]
public class Raven : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder
{
    public static readonly Team RavenTeam = new("teams.raven", new(49, 36, 82), TeamRevealType.OnlyMe);
    private Raven() : base("raven", RavenTeam.Color, RoleCategory.NeutralRole, RavenTeam,
        [KillCooldown, HasDeadBodyArrow, new GroupConfiguration("options.role.raven.group.raventime", [RavenTimeOption, RavenTimeAliveNum, RavenTimeDuration, MeetingEndEnterRavenTimeDisperse], RavenTeam.Color.ToUnityColor().RGBMultiplied(0.65f))])
    { }

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration KillCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.raven.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly BoolConfiguration HasDeadBodyArrow = NebulaAPI.Configurations.Configuration("options.role.raven.hasDeadBodyArrow", true);
    public static readonly BoolConfiguration RavenTimeOption = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTime", true);
    public static readonly IntegerConfiguration RavenTimeAliveNum = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTimeAlived", (2, 24), 4, () => RavenTimeOption);
    public static readonly FloatConfiguration RavenTimeDuration = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTimeDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second, () => RavenTimeOption);
    public static readonly BoolConfiguration MeetingEndEnterRavenTimeDisperse = NebulaAPI.Configurations.Configuration("options.role.raven.meetingEndEnterRavenTimeDisperse", true, () => RavenTimeOption);

    public static readonly Raven MyRole = new();
    private static Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("Raven.png")?.AsImage(115f);
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

        HudManager.Instance.StopCoroutine(FlashCoroutine);
        HudManager.Instance.FullScreen.gameObject.SetActive(false);
        FlashCoroutine = null;
        HudManager.Instance.lightFlashHandle?.Dispose();
        HudManager.Instance.lightFlashHandle = null;
    }

    [HarmonyPatch]
    public static class RavenPatchs
    {
        [HarmonyPatch(typeof(PlayerControl), "get_CanMove"), HarmonyPostfix]
        public static void CanMovePatch(ref bool __result)
        {
            if (GamePlayer.LocalPlayer is null) return;
            if (!__result && Instance.IsOutMeeting() && NebulaAPI.CurrentGame != null && GamePlayer.LocalPlayer.Role is Raven.Instance)
                __result = true;
        }

        [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation"), HarmonyPrefix]
        public static void PlayerHandleAnimationPatch(PlayerPhysics __instance, ref bool amDead)
        {
            var gamePlayer = __instance.myPlayer.ToGamePlayer();
            if (!amDead && (Instance.IsOutMeeting() || Instance.IsInRavenTime) && NebulaAPI.CurrentGame != null && gamePlayer.Role is Raven.Instance)
            {
                amDead = true;
                __instance.myPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
                __instance.myPlayer.cosmetics.SetGhost();
            }
        }

        [HarmonyPatch(typeof(HudManagerExtension), "UpdateHudContent"), HarmonyPostfix]
        public static void UpdateHudContent(HudManager manager)
        {
            if (!PlayerControl.LocalPlayer) return;
            var instance = NebulaGameManager.Instance;
            if (instance != null && instance.GameState == NebulaGameStates.NotStarted) return;
            if (Instance.IsInRavenTime)
            {
                manager.ReportButton.ToggleVisible(false);
                manager.ImpostorVentButton.ToggleVisible(false);
                manager.SabotageButton.ToggleVisible(false);
            }
        }

        [HarmonyPatch(typeof(PlayerControl), "CmdReportDeadBody"), HarmonyPrefix]
        public static bool ReportDeadBodyPatch() => !Instance.IsInRavenTime;

        [HarmonyPatch(typeof(MeetingHudExtension), "ModCoStartMeeting"), HarmonyPrefix]
        public static bool ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo deadBody, int reportType, ref IEnumerator __result)
        {
            if (NebulaGameManager.Instance != null && (MyRole as ISpawnable).IsSpawnable)
            {
                __result = ModCoStartMeeting(reporter, deadBody, reportType);
                return false;
            }
            return true;
        }

        private static IEnumerator ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, int reportType)
        {
            while (!MeetingHud.Instance) yield return null;

            MeetingRoomManager.Instance.RemoveSelf();
            HudManager.Instance.InitMap();
            MapBehaviour.Instance.SetPreMeetingPosition(PlayerControl.LocalPlayer.transform.position, false);

            foreach (var player in GamePlayer.AllPlayers)
            {
                if (player.VanillaPlayer is not { } vp) continue;
                if (!vp.GetComponent<DummyBehaviour>().enabled) vp.MyPhysics.ExitAllVents();
                vp.RemoveProtection();
                vp.NetTransform.enabled = true;
                vp.MyPhysics.ResetMoveState(true);

                for (int i = 0; i < vp.currentRoleAnimations.Count; i++)
                {
                    if (vp.currentRoleAnimations[i]?.gameObject != null)
                        Object.Destroy(vp.currentRoleAnimations[i].gameObject);
                }
                vp.logger.Error("Encountered a null Role Animation while destroying.", null);

                vp.inMovingPlat = false;
                vp.isKilling = false;
                vp.currentRoleAnimations.Clear();

                if (vp.cosmetics.CurrentPet is not { } pet) continue;
                if (vp.cosmetics.petHiddenByViper)
                {
                    vp.cosmetics.TogglePet(true);
                    var vector = vp.transform.position;
                    if (ShipStatus.Instance.TryCast<AirshipStatus>())
                    {
                        var list = new List<Vector2>
                        {
                            new(8.2f, 15.2f), new(8.25f, 15.9f), new(8.2f, 14.3f),
                            new(11f, 14.3f), new(9.8f, 14.3f), new(13f, 14.3f)
                        };
                        vector = list[Random.Range(0, list.Count)];
                    }
                    pet.SetGettingPet(false, vector);
                    continue;
                }
                pet.SetGettingPet(false, pet.transform.position);
            }

            if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
            if (Minigame.Instance) Minigame.Instance.ForceClose();
            ShipStatus.Instance.OnMeetingCalled();
            KillAnimation.SetMovement(reporter, true);
            GameData.TimeLastMeetingStarted = Time.realtimeSinceStartup;

            MeetingHud instance = MeetingHud.Instance;
            instance.StartCoroutine(MeetingHudExtension.ModCoMeetingHudIntro(instance, reporter, deadBody, (MeetingHudExtension.ReportType)reportType).WrapToIl2Cpp());
        }
    }

    public class Instance(Player player) : RuntimeAssignableTemplate(player), RuntimeRole, RuntimeAssignable, ILifespan, IBindPlayer, IGameOperator, IReleasable
    {
        public static bool IsInRavenTime;
        public DeadbodyArrowAbility? ArrowAbility { get; private set; }
        DefinedRole RuntimeRole.Role => MyRole;
        public static GameEnd RavenTeamWin = NebulaAPI.Preprocessor!.CreateEnd("raven", MyRole.RoleColor);

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
        void WinCheck(GameUpdateEvent _)
        {
            try
            {
                var totalAlive = AddonHelper.GetAlivePlayers().totalAlive;
                if (!MyPlayer.IsDead && totalAlive <= 1)
                    NebulaAPI.CurrentGame?.TriggerGameEnd(RavenTeamWin, GameEndReason.Situation, BitMasks.AsPlayer().Add(MyPlayer));
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
                FlashCoroutine ??= HudManager.Instance.StartCoroutine(CoRavenTimeFlash());
                even = !even;
                var color = even ? Color.yellow : Color.red;
                ev.AppendText(Language.Translate("role.raven.raventime").Replace("%TIME%", Mathf.Ceil(RavenTimeLeft).ToString()).Color(color));
                if (!MyPlayer.IsDead) MyPlayer.VanillaPlayer.Visible = true;
            }
            else
            {
                StopRavenTimeFlash();
            }
        }

        void OnUpdate(GameUpdateEvent _)
        {
            if (NebulaGameManager.Instance is null || !RavenTimeOption || MyPlayer.IsDead) return;

            var aliveCount = NebulaGameManager.Instance.AllPlayerInfo.Count(p => !p.IsDead);
            if (aliveCount <= RavenTimeAliveNum && !IsInRavenTime)
            {
                if (!MeetingHud.Instance && !ExileController.Instance)
                    SetRavenTime.Invoke(true);
                MyPlayer.VanillaCosmetics.TogglePet(false);
            }

            if ((FlashCoroutine != null && !IsInRavenTime) || (IsInRavenTime && MeetingHud.Instance) || (IsInRavenTime && aliveCount > RavenTimeAliveNum))
            {
                SetRavenTime.Invoke(false);
                MyPlayer.VanillaCosmetics.TogglePet(true);
            }

            if (IsInRavenTime)
            {
                HudManager.Instance.StopOxyFlash();
                HudManager.Instance.StopReactorFlash();
                RavenTimeLeft -= Time.deltaTime;
                if (RavenTimeLeft <= 0f && MyPlayer.AmOwner)
                {
                    MyPlayer.Suicide(PlayerStates.Suicide, EventDetails.Kill, KillParameter.NormalKill);
                    SetRavenTime.Invoke(false);
                }
            }
        }

        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player == MyPlayer && IsInRavenTime)
                SetRavenTime.Invoke(false);
        }

        public static bool IsOutMeeting() => MeetingHud.Instance && MeetingHud.Instance.gameObject.transform.localPosition.x > 15;

        private TextMeshPro? tmPro;
        private DefinedRole? targetRole;

        private IEnumerator CoLeaveOrJoinMeeting(bool isleaving)
        {
            yield return HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new Vector3(isleaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isleaving;

            if (isleaving && tmPro == null)
            {
                var textHolder = UnityHelper.CreateObject("RavenTarget", HudManager.Instance.transform, Vector3.zero, LayerExpansion.GetUILayer());
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

                    var instantiatedObj = noSGUIText.Instantiate(
                        new Anchor(new Virial.Compat.Vector2(0f, 0f), new Virial.Compat.Vector3(-0.5f, -0.5f, 0f)),
                        new Size(20f, 20f),
                        out _
                    );
                    instantiatedObj?.transform.SetParent(textHolder.transform, false);
                }

                if (GameOperatorManager.Instance is null) yield break;
                GameOperatorManager.Instance.Subscribe<GameUpdateEvent>(ev =>
                {
                    if (!tmPro) return;
                    if (IsOutMeeting() && !killed)
                    {
                        MyPlayer.VanillaCosmetics.TogglePet(false);
                        NebulaAPI.RunEvent(new LeaveMeetingEvent());

                        if (targetRole == null || !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && targetRole.Id == p.Role.Role.Id))
                        {
                            var allAlive = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.IsDead && p.Role.Role is not Raven).ToList();
                            targetRole = allAlive.Count > 0 ? allAlive[Random.Range(0, allAlive.Count)].Role.Role : null;
                        }

                        tmPro?.gameObject.SetActive(true);
                        var iconTag = targetRole != null ? targetRole.GetRoleIconTag() : "";
                        tmPro?.text = Language.Translate("role.raven.killtarget").Replace("%ROLE%", iconTag + (targetRole?.DisplayColoredName ?? ""));
                        tmPro?.transform.localPosition = new Vector3(-0.07f, -2.45f, 0f);
                        PlayerControl.LocalPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
                    }
                    else if ((MyPlayer.IsDead && IsOutMeeting()) || !IsOutMeeting() || killed)
                    {
                        MyPlayer.VanillaCosmetics.TogglePet(true);
                        NebulaAPI.RunEvent(new ReturnMeetingEvent());
                        PlayerControl.LocalPlayer.gameObject.layer = PlayerControl.LocalPlayer.Data.IsDead ? LayerExpansion.GetGhostLayer() : LayerExpansion.GetPlayersLayer();
                        tmPro?.gameObject.SetActive(false);
                    }
                }, this);
            }

            yield return HudManager.Instance.CoFadeFullScreen(Color.black, Color.clear, 1f, false);
            coroutine = null;
        }

        private bool killed;

        void RuntimeAssignable.OnActivated()
        {
            IsInRavenTime = false;
            RavenTimeLeft = RavenTimeDuration;

            if (NebulaAPI.CurrentGame is { } currentGame)
            {
                GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
                {
                    IsInRavenTime = false;
                    RavenTimeLeft = RavenTimeDuration;
                }, currentGame);
                GameOperatorManager.Instance?.RegisterOnReleased(() =>
                {
                    IsInRavenTime = false;
                    RavenTimeLeft = RavenTimeDuration;
                }, currentGame);
            }

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

                var mkillTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.LocalKillablePredicate(p) && IsOutMeeting(), null, false, false);

                meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction)
                    .SetImage(buttonImage!)
                    .SetColorLabel(MyRole.RoleColor);
                meetingButton.Availability = _ => !killed && MeetingHud.Instance.state is not MeetingHud.VoteStates.Animating and not MeetingHud.VoteStates.Discussion and not MeetingHud.VoteStates.Results;
                meetingButton.Visibility = _ => !MyPlayer.IsDead && AmongUsUtil.InMeeting;
                meetingButton.OnClick = (action) =>
                {
                    if (killed)
                    {
                        if (IsOutMeeting())
                            NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                        return;
                    }
                    if (coroutine != null) return;
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(!IsOutMeeting()).WrapToIl2Cpp());
                };
                meetingButton.OnUpdate = _ =>
                {
                    if (IsOutMeeting() && MeetingHudExtension.VotingTimer <= 5f && coroutine == null)
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                    meetingButton.SetLabel(IsOutMeeting() ? "raven.returnmeeting" : "raven.leavemeeting");
                };

                meetingKillButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.Kill)
                    .SetLabelType(ModAbilityButton.LabelType.Impostor);
                meetingKillButton.Availability = _ => !killed && mkillTracker.CurrentTarget != null && IsOutMeeting() && !MyPlayer.IsDead;
                meetingKillButton.Visibility = _ => AmongUsUtil.InMeeting && MeetingHud.Instance.state is not MeetingHud.VoteStates.Animating and not MeetingHud.VoteStates.Discussion and not MeetingHud.VoteStates.Results && IsOutMeeting();
                meetingKillButton.OnClick = _ =>
                {
                    killed = true;
                    var p = mkillTracker.CurrentTarget;
                    if (p != null && targetRole != null && p.Role.Role == targetRole)
                        MyPlayer.MurderPlayer(p, missing, EventDetails.Kill, KillParameter.MeetingKill, KillCondition.TargetAlive);
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                };
                meetingKillButton.SetLabel("kill");

                var killTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p) && !IsOutMeeting(), null, false, false);
                killButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false)
                    .BindKey(VirtualKeyInput.Kill, null)
                    .SetLabelType(ModAbilityButton.LabelType.Impostor);
                killButton.Availability = _ => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown).SetAsKillCoolTimer().Start(null);
                killButton.Visibility = _ => !MyPlayer.IsDead && (IsInRavenTime || !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.IsImpostor));
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
                killButton.SetLabel("kill");
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }
        }

        void RuntimeAssignable.OnInactivated()
        {
            IsInRavenTime = false;
            if (IsOutMeeting())
                NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
        }

        [OnlyMyPlayer]
        void OnRavenTimeStart(RavenTimeStartEvent _) => killButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown / 10f).SetAsKillCoolTimer().Start(null);

        void OnCameraUpdate(CameraUpdateEvent ev)
        {
            if ((IsInRavenTime || IsOutMeeting()) && !MyPlayer.IsDead)
            {
                ev.UpdateHue(180f);
                ev.UpdateSaturation(0f, true);
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent _) => killed = false;

        void OnMeetingEnd(MeetingPreEndEvent _)
        {
            foreach (var player in GamePlayer.AllPlayers)
                if (player.VanillaPlayer) player.VanillaPlayer.ResetForMeeting();
        }

        void OnTaskStart(TaskPhaseRestartEvent _)
        {
            if (IsInRavenTime && MeetingEndEnterRavenTimeDisperse)
                RavenStartDisperseRpc.Invoke();
        }

        bool RuntimeRole.EyesightIgnoreWalls => true;

        public RemoteProcess<bool> SetRavenTime = new("SetRavenTimeRPC", (msg, _) =>
        {
            if (msg)
            {
                StopRavenTimeFlash();
                FlashCoroutine = HudManager.Instance.StartCoroutine(CoRavenTimeFlash().WrapToIl2Cpp());
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
    }
}