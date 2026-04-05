using Color = UnityEngine.Color;
using Image = Virial.Media.Image;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace DHMO.Roles;

// Made by Plana at the first time.
// Optimized by Water in subsequent versions.
[NebulaRPCHolder]
public class Raven : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder
{
    public static Team RavenTeam = new("teams.raven", new(87, 66, 102), TeamRevealType.OnlyMe);
    private Raven() : base("raven", RavenTeam.Color, RoleCategory.NeutralRole, RavenTeam,
        [KillCooldown,HasDeadBodyArrow, new GroupConfiguration("options.role.raven.group.raventime", [RavenTimeOption, RavenTimeAliveNum, RavenTimeDuration, MeetingEndEnterRavenTimeDisperse], RavenTeam.Color.ToUnityColor().RGBMultiplied(0.65f))]){ }
    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    static public readonly IRelativeCooldownConfiguration KillCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.raven.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    static public readonly BoolConfiguration HasDeadBodyArrow = NebulaAPI.Configurations.Configuration("options.role.raven.hasDeadBodyArrow", true);
    static public readonly BoolConfiguration RavenTimeOption = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTime", true);
    static public readonly IntegerConfiguration RavenTimeAliveNum = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTimeAlived", (1,24),3,() => RavenTimeOption);
    static public readonly FloatConfiguration RavenTimeDuration = NebulaAPI.Configurations.Configuration("options.role.raven.RavenTimeDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second,() => RavenTimeOption);
    static public readonly BoolConfiguration MeetingEndEnterRavenTimeDisperse = NebulaAPI.Configurations.Configuration("options.role.raven.meetingEndEnterRavenTimeDisperse", true,() => RavenTimeOption);

    static public readonly Raven MyRole = new();
    static private Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("Raven.png")?.AsImage(115f);
    public static TranslatableTag missing = new("state.missing");
    public static RemoteProcess RavenStartDisperseRpc = new("RavenStartDisperseRpc", (_) =>
    {
        var player = GamePlayer.LocalPlayer;
        var vanillaplayer = PlayerControl.LocalPlayer;
        if (player == null || vanillaplayer == null)
        {
            return;
        }
        if (player.IsDead || player.IsDisconnected)
        {
            return;
        }
        AmongUsUtil.PlayFlash(MyRole.RoleColor.ToUnityColor());
        if (Minigame.Instance)
        {
            try
            {
                Minigame.Instance.Close();
            }
            catch
            {
            }
        }
        if (vanillaplayer.inVent)
        {
            vanillaplayer.MyPhysics.RpcExitVent(Vent.currentVent.Id);
            vanillaplayer.MyPhysics.ExitAllVents();
        }
        byte mapId = AmongUsUtil.CurrentMapId;
        NebulaPreSpawnLocation[] cand = NebulaPreSpawnLocation.Locations[(int)mapId];
        if (cand.Length == 0)
        {
            cand = [.. NebulaPreSpawnLocation.Locations[(int)mapId].Where(l => l.VanillaIndex != null)];
        }
        var list = new List<Vector2>();
        cand.Do(p => list.Add(p.Position!.Value));
        vanillaplayer.NetTransform.RpcSnapTo(list[UnityEngine.Random.Range(0, list.Count)]);
        if (vanillaplayer.walkingToVent)
        {
            vanillaplayer.inVent = false;
            Vent.currentVent = null;
            vanillaplayer.moveable = true;
            vanillaplayer.MyPhysics.StopAllCoroutines();
        }
    });
    bool DefinedRole.IsKiller
    {
        get
        {
            return true;
        }
    }
    static Coroutine? FlashCoroutine;

    public static IEnumerator CoRavenTimeFlash()
    {
        HudManager hud = UnityEngine.Object.FindObjectOfType<HudManager>();
        if (hud == null)
        {
            yield break;
        }

        WaitForSeconds wait = new(1f);
        bool light = false;
        hud.FullScreen.color = new Color(1f, 0f, 0f, 0.37254903f);

        for (; ; )
        {
            hud.FullScreen.gameObject.SetActive(!hud.FullScreen.gameObject.activeSelf);

            if (hud.lightFlashHandle == null)
            {
                hud.lightFlashHandle = DestroyableSingleton<DualshockLightManager>.Instance.AllocateLight();
                hud.lightFlashHandle.color = new Color(1f, 0f, 0f, 1f);
                hud.lightFlashHandle.intensity = 1f;
            }

            light = !light;
            Color currentColor = hud.lightFlashHandle.color;
            currentColor.a = light ? 1f : 0f;
            hud.lightFlashHandle.color = currentColor;

            yield return wait;
        }
    }

    public static void StopRavenTimeFlash()
    {
        if (FlashCoroutine != null)
        {
            HudManager.Instance.StopCoroutine(FlashCoroutine);
            HudManager.Instance.FullScreen.gameObject.SetActive(false);
            FlashCoroutine = null;
            HudManager.Instance.lightFlashHandle?.Dispose();
            HudManager.Instance.lightFlashHandle = null;
        }
    }

    [HarmonyPatch]
    public class RavenPatchs
    {
        [HarmonyPatch(typeof(PlayerControl),"get_CanMove"),HarmonyPostfix]
        public static void CanMovePatch(ref bool __result)
        {
            if (!__result && Instance.IsOutMeeting() && NebulaAPI.CurrentGame!=null && GamePlayer.LocalPlayer!.Role is Raven.Instance)
            {
                __result = true;
            }
        }
        [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation"),HarmonyPrefix]
        public static void PlayerHandleAnimationPatch(PlayerPhysics __instance,ref bool amDead)
        {
            if (!amDead && (Instance.IsOutMeeting()|| Instance.IsInRavenTime) && NebulaAPI.CurrentGame != null && __instance.myPlayer.ToGamePlayer()!.Role is Raven.Instance)
            {
                amDead = true;
                __instance.myPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
            }
        }
        [HarmonyPatch(typeof(HudManagerExtension), "UpdateHudContent"), HarmonyPostfix]
        public static void UpdateHudContent(HudManager manager)
        {
            if (!PlayerControl.LocalPlayer)
            {
                return;
            }
            NebulaGameManager? instance = NebulaGameManager.Instance;
            if (instance != null && instance.GameState == NebulaGameStates.NotStarted)
            {
                return;
            }
            if (Instance.IsInRavenTime)
            {
                manager.ReportButton.ToggleVisible(false);
                manager.ImpostorVentButton.ToggleVisible(false);
                manager.SabotageButton.ToggleVisible(false);
            }
        }
        [HarmonyPatch(typeof(PlayerControl), "CmdReportDeadBody")]
        [HarmonyPrefix]
        public static bool ReportDeadBodyPatch()
        {
            if (Instance.IsInRavenTime)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(MeetingHudExtension), "ModCoStartMeeting"),HarmonyPrefix]
        public static bool ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo deadBody, int reportType,ref System.Collections.IEnumerator __result)
        {
            if (NebulaGameManager.Instance != null && (MyRole as ISpawnable).IsSpawnable)
            {
                __result = ModCoStartMeeting(reporter, deadBody, reportType);
                return false;
            }
            return true;
        }
        [HarmonyPatch(typeof(NebulaGameManager),"OnGameStart"),HarmonyPostfix]
        public static void OnGameStarted(Game __instance)
        {
            if (__instance!= null && (Raven.MyRole as ISpawnable).IsSpawnable)
            {
                GameOperatorManager.Instance?.Subscribe<MeetingPreEndEvent>(ev =>
                {
                    foreach (var player in GamePlayer.AllPlayers)
                    {
                        if (player.VanillaPlayer) player.VanillaPlayer.ResetForMeeting();
                    }
                }, __instance);
            }
        }
        private static IEnumerator ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, int reportType)
        {
            while (!MeetingHud.Instance) yield return null;

            MeetingRoomManager.Instance.RemoveSelf();
            DestroyableSingleton<HudManager>.Instance.InitMap();
            MapBehaviour.Instance.SetPreMeetingPosition(PlayerControl.LocalPlayer.transform.position, false);
            foreach (var player in GamePlayer.AllPlayers)
            {
                if (player.VanillaPlayer)
                {
                    var vp = player.VanillaPlayer;
                    if (!vp.GetComponent<DummyBehaviour>().enabled)
                    {
                        vp.MyPhysics.ExitAllVents();
                    }
                    vp.RemoveProtection();
                    vp.NetTransform.enabled = true;
                    vp.MyPhysics.ResetMoveState(true);
                    for (int i = 0; i < vp.currentRoleAnimations.Count; i++)
                    {
                        if (vp.currentRoleAnimations[i] != null && vp.currentRoleAnimations[i].gameObject != null)
                        {
                            UnityEngine.Object.Destroy(vp.currentRoleAnimations[i].gameObject);
                            vp.logger.Error("Encountered a null Role Animation while destroying.", null);
                        }
                    }
                    vp.inMovingPlat = false;
                    vp.isKilling = false;
                    vp.currentRoleAnimations.Clear();
                    if (vp.cosmetics.CurrentPet != null)
                    {
                        if (vp.cosmetics.petHiddenByViper)
                        {
                            vp.cosmetics.TogglePet(true);
                            Vector2 vector = vp.transform.position;
                            if (ShipStatus.Instance.TryCast<AirshipStatus>())
                            {
                                List<Vector2> list =
                                [
                                    new Vector2(8.2f, 15.2f),
                                    new Vector2(8.25f, 15.9f),
                                    new Vector2(8.2f, 14.3f),
                                    new Vector2(11f, 14.3f),
                                    new Vector2(9.8f, 14.3f),
                                    new Vector2(13f, 14.3f)
                                ];
                                vector = list[UnityEngine.Random.Range(0, list.Count)];
                            }
                            vp.cosmetics.CurrentPet.SetGettingPet(false, vector);
                            continue;
                        }
                        vp.cosmetics.CurrentPet.SetGettingPet(false, vp.cosmetics.CurrentPet.transform.position);
                    }
                }
            }

            if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
            if (Minigame.Instance) Minigame.Instance.ForceClose();
            ShipStatus.Instance.OnMeetingCalled();
            KillAnimation.SetMovement(reporter, true);
            GameData.TimeLastMeetingStarted = Time.realtimeSinceStartup;

            var meetingHud = MeetingHud.Instance;
            meetingHud.StartCoroutine(MeetingHudExtension.ModCoMeetingHudIntro(meetingHud, reporter, deadBody, (MeetingHudExtension.ReportType)reportType).WrapToIl2Cpp());
            yield break;
        }
    }
    public class Instance(Player player) : RuntimeAssignableTemplate(player), RuntimeRole, RuntimeAssignable, ILifespan, IBindPlayer, IGameOperator, IReleasable
    {
        public static bool IsInRavenTime;
        public DeadbodyArrowAbility? ArrowAbility { get; private set; } = null;
        DefinedRole RuntimeRole.Role => MyRole;
        public static GameEnd RavenTeamWin = NebulaAPI.Preprocessor!.CreateEnd("raven", MyRole.RoleColor);

        void BlockTriggerEnd(EndCriteriaPreMetEvent ev)
        {
            if (ev.GameEnd != NebulaGameEnd.LoversWin && ev.GameEnd != RavenTeamWin && !MyPlayer.IsDead && ev.EndReason == GameEndReason.Situation)
            {
                ev.Reject();
            }
        }
        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            var totalAlive = DHMO.Core.DHMOUtilities.GetAlivePlayers().totalAlive;
            ev.SetWinIf(ev.GameEnd == RavenTeamWin && !MyPlayer.IsDead && totalAlive <= 1);
        }
        [OnlyHost]
        void WinCheck(GameUpdateEvent ev)
        {
            try
            {
                var totalAlive = DHMO.Core.DHMOUtilities.GetAlivePlayers().totalAlive;
                if (!MyPlayer.IsDead && totalAlive <= 1)
                {
                    NebulaAPI.CurrentGame?.TriggerGameEnd(RavenTeamWin, GameEndReason.Situation, BitMasks.AsPlayer().Add(MyPlayer));
                }
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }
        [OnlyMyPlayer]
        void CheckKill(PlayerCheckKilledEvent ev)
        {
            if (!IsInRavenTime)
            {
                return;
            }
            if (ev.Killer.PlayerId == base.MyPlayer.PlayerId)
            {
                return;
            }
            if (ev.EventDetail == EventDetail.Bubbled || ev.EventDetail == EventDetail.Curse)
            {
                NebulaManager.Instance.StartDelayAction(2f, () =>
                {
                    MyPlayer.VanillaPlayer.moveable = true;
                });
            }
            ev.Result = KillResult.Rejected;
        }
        void OnKill(PlayerTryVanillaKillLocalEventAbstractPlayerEvent ev)
        {
            if (!IsInRavenTime)
            {
                return;
            }
            if (ev.Target.RealPlayer.PlayerId == MyPlayer.PlayerId)
            {
                ev.Cancel(false);
            }
        }
        void OnCheckKill(PlayerCheckCanKillLocalEvent ev)
        {
            if (!IsInRavenTime)
            {
                return;
            }
            if (ev.Target.RealPlayer.PlayerId == MyPlayer.PlayerId)
            {
                ev.SetAsCannotKillForcedly();
            }
        }

        private static ModAbilityButton? killButton, meetingButton, meetingKillButton;
        Coroutine? coroutine;
        public static float RavenTimeLeft;
        bool even;
        void AppendTaskPanel(PlayerTaskTextLocalEvent ev)
        {
            if (IsInRavenTime)
            {
                FlashCoroutine ??= HudManager.Instance.StartCoroutine(CoRavenTimeFlash());
                even = !even;
                Color color = (even ? Color.yellow : Color.red);
                ev.AppendText(Language.Translate("role.raven.raventime").Replace("%TIME%", Mathf.Ceil(RavenTimeLeft).ToString()).Color(color));
                if (!MyPlayer.IsDead)
                {
                    MyPlayer.VanillaPlayer.Visible = true;
                }
            }
            else
            {
                StopRavenTimeFlash();
            }
        }
        void OnUpdate(GameUpdateEvent ev)
        {
            if (NebulaGameManager.Instance is null) return;
            if (RavenTimeOption && !MyPlayer.IsDead)
            {
                if (NebulaGameManager.Instance.AllPlayerInfo.Count(p => !p.IsDead) <= RavenTimeAliveNum && !IsInRavenTime)
                {
                    if (!MeetingHud.Instance && !ExileController.Instance)
                        SetRavenTime.Invoke(true);
                }
                if (IsInRavenTime && NebulaGameManager.Instance.AllPlayerInfo.Count(p => !p.IsDead) > RavenTimeAliveNum) SetRavenTime.Invoke(false);
                if (IsInRavenTime && MeetingHud.Instance) SetRavenTime.Invoke(false);
                if (FlashCoroutine != null && !IsInRavenTime) SetRavenTime.Invoke(false);
                
                if (IsInRavenTime)
                {
                    HudManager.Instance.StopOxyFlash();
                    HudManager.Instance.StopReactorFlash();
                    RavenTimeLeft -= Time.deltaTime;
                    if (RavenTimeLeft <= 0f)
                    {
                        if (MyPlayer.AmOwner)
                        {
                            MyPlayer.Suicide(PlayerStates.Suicide, EventDetails.Kill, KillParameter.NormalKill);
                            SetRavenTime.Invoke(false);
                        }
                    }
                }
            }
        }
        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player == MyPlayer && IsInRavenTime)
            {
                SetRavenTime.Invoke(false);
            }
        }
        public static bool IsOutMeeting()
        {
            return MeetingHud.Instance && MeetingHud.Instance.gameObject.transform.localPosition.x > 15;
        }
        TextMeshPro? tmPro;
        DefinedRole? targetRole;
        IEnumerator CoLeaveOrJoinMeeting(bool isleaving)
        {
            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.clear, Color.black, 1f, false);
            MeetingHud.Instance.gameObject.transform.localPosition = new(isleaving ? 17f : 0f, 0f);
            Camera.main.GetComponent<FollowerCamera>().Locked = !isleaving;
            if (isleaving)
            {
                if (tmPro == null)
                {
                    GameObject TextHolder = UnityHelper.CreateObject("RavenTarget", DestroyableSingleton<HudManager>.Instance.transform, UnityEngine.Vector3.zero, LayerExpansion.GetUILayer());
                    this.BindGameObject(TextHolder.gameObject);
                    if (NebulaGUIWidgetEngine.API != null)
                    {
                        var textAttribute = new TextAttribute(NebulaGUIWidgetEngine.API.GetAttribute(AttributeParams.StandardBaredBoldLeftNonFlexible))
                        {
                            Alignment = Virial.Text.TextAlignment.Top,
                            FontSize = new FontSize(1.6f, true),
                            Size = new Size(3f, 1f)
                        };

                        var noSGUIText = new NoSGUIText(
                            GUIAlignment.Bottom,
                            textAttribute,
                            new RawTextComponent("")
                        )
                        {
                            PostBuilder = delegate (TextMeshPro t)
                            {
                                if (t != null)
                                {
                                    tmPro = t;
                                    tmPro.sortingOrder = 0;
                                }
                            }
                        };

                        if (TextHolder != null)
                        {
                            var instantiatedObj = noSGUIText.Instantiate(
                                new Anchor(new Virial.Compat.Vector2(0f, 0f), new Virial.Compat.Vector3(-0.5f, -0.5f, 0f)),
                                new Size(20f, 20f),
                                out Size size
                            );
                            instantiatedObj?.transform.SetParent(TextHolder.transform, false);
                        }
                    }
                    if (GameOperatorManager.Instance is null) yield break;
                    GameOperatorManager instance = GameOperatorManager.Instance;
                    instance?.Subscribe(delegate (GameUpdateEvent ev)
                        {
                            if (tmPro)
                            {
                                if (IsOutMeeting() && !killed)
                                {
                                    if (GameOperatorManager.Instance is null) return;
                                    NebulaAPI.RunEvent(new LeaveMeetingEvent());
                                    if (targetRole == null || !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && targetRole.Id == p.Role.Role.Id))
                                    {
                                        var allalive = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.IsDead && p.Role.Role is not Raven).ToList();
                                        targetRole = allalive[UnityEngine.Random.Range(0, allalive.Count)].Role.Role;
                                    }
                                    tmPro!.gameObject.SetActive(true);
                                    string iconTag = targetRole != null ? targetRole.GetRoleIconTag() : "";
                                    tmPro.text = Language.Translate("role.raven.killtarget").Replace("%ROLE%", iconTag + targetRole!.DisplayColoredName);

                                    tmPro.transform.localPosition = new Vector3(-0.07f, -2.45f, 0f);
                                    PlayerControl.LocalPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
                                }
                                else
                                {
                                    if (GameOperatorManager.Instance is null) return;
                                    NebulaAPI.RunEvent(new ReturnMeetingEvent());
                                    PlayerControl.LocalPlayer.gameObject.layer = PlayerControl.LocalPlayer.Data.IsDead ? LayerExpansion.GetGhostLayer() : LayerExpansion.GetPlayersLayer();
                                    tmPro!.gameObject.SetActive(false);
                                }
                            }
                        }, this);
                }
            }
            yield return DestroyableSingleton<HudManager>.Instance.CoFadeFullScreen(Color.black, Color.clear, 1f, false);
            coroutine = null;
        }
        bool killed;
        void RuntimeAssignable.OnActivated()
        {
            IsInRavenTime = false;
            RavenTimeLeft = RavenTimeDuration;
            var currentGame = NebulaAPI.CurrentGame;
            if (currentGame != null)
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
                if (HasDeadBodyArrow)
                {
                    ArrowAbility = new DeadbodyArrowAbility();
                    ArrowAbility.Bind(this);
                    ArrowAbility.RegisterSelf();
                    GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => ArrowAbility.ShowArrow = !IsInRavenTime && !MyPlayer.IsDead, this);
                }
                ObjectTracker<GamePlayer> mkillTracker = ObjectTrackers.ForPlayer(this, null, base.MyPlayer, p => ObjectTrackers.LocalKillablePredicate(p) && IsOutMeeting(), null, false, false);

                meetingButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.SidekickAction).SetImage(buttonImage!).SetLabelType(ModAbilityButton.LabelType.Impostor).SetColorLabel(MyRole.RoleColor);
                meetingButton.Availability = _ => !killed;
                meetingButton.Visibility = _ => !MyPlayer.IsDead && MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Discussion && MeetingHud.Instance.state != MeetingHud.VoteStates.Results;
                meetingButton.OnClick = _ =>
                {
                    if (killed)
                    {
                        if (IsOutMeeting())
                        {
                            NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                        }
                        return;
                    }
                    if (coroutine != null)
                    {
                        return;
                    }
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(!IsOutMeeting()).WrapToIl2Cpp());
                };

                meetingButton.OnUpdate = _ =>
                {
                    if (IsOutMeeting() && MeetingHudExtension.VotingTimer <= 5f && coroutine == null)
                    {
                        coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                    }

                    if (IsOutMeeting())
                    {
                        meetingButton.SetLabel("raven.returnmeeting");
                    }
                    else
                    {
                        meetingButton.SetLabel("raven.leavemeeting");
                    }
                };

                meetingKillButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true).BindKey(VirtualKeyInput.Kill).SetLabelType(ModAbilityButton.LabelType.Impostor);
                meetingKillButton.Availability = _ => !killed && mkillTracker.CurrentTarget != null && IsOutMeeting();
                meetingKillButton.Visibility = _ => MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Discussion && MeetingHud.Instance.state != MeetingHud.VoteStates.Results && IsOutMeeting();
                meetingKillButton.OnClick = _ =>
                {
                    killed = true;
                    var p = mkillTracker.CurrentTarget;
                    if (p != null && targetRole != null && p.Role.Role == targetRole)
                    {
                        MyPlayer.MurderPlayer(p, missing, EventDetails.Kill, KillParameter.MeetingKill, KillCondition.TargetAlive);
                    }
                    coroutine = NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
                };
                meetingKillButton.SetLabel("kill");

                ObjectTracker<IPlayerlike> killTracker = ObjectTrackers.ForPlayerlike(this, null, base.MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p) && !IsOutMeeting(), null, false, false);
                killButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false).BindKey(VirtualKeyInput.Kill, null).SetLabelType(ModAbilityButton.LabelType.Impostor);
                killButton.Availability = _ => killTracker.CurrentTarget != null && MyPlayer.CanMove;
                killButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown).SetAsKillCoolTimer().Start(null);
                killButton.OnUpdate = _ =>
                {
                    if (IsInRavenTime)
                    {
                        killButton.Visibility = _ => !MyPlayer.IsDead;

                    }
                    else
                    {
                        killButton.Visibility = _ => !MyPlayer.IsDead && !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.IsImpostor);
                    }
                };
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
            {
                NebulaManager.Instance.StartCoroutine(CoLeaveOrJoinMeeting(false).WrapToIl2Cpp());
            }
        }
        void OnRavenTimeStart(RavenTimeStartEvent ev)
        {
            killButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, KillCooldown.Cooldown / 10f).SetAsKillCoolTimer().Start(null);
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            killed = false;
        }
        [Local]
        void OnTaskStart(TaskPhaseRestartEvent ev)
        {
            if (IsInRavenTime && MeetingEndEnterRavenTimeDisperse)
            {
                RavenStartDisperseRpc.Invoke();
            }
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
            }
            else
            {
                StopRavenTimeFlash();
                IsInRavenTime = msg;
            }
        });
    }
}