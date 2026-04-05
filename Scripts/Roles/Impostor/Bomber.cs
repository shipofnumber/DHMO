using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace DHMO.Roles;


// Made by Plana at the first time.
// Optimized by Water in subsequent versions.
/*[NebulaRPCHolder]
public class Bomber : DefinedSingleAbilityRoleTemplate<Bomber.Ability>, HasCitation, DefinedRole
{
    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class BombSystem : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        TextMeshPro? tmPro;
        public float leftBombTime;
        public static GamePlayer? bombPlayer;
        GamePlayer? myplayer;
        public ModAbilityButtonImpl? passBombButton;
        static public BombSystem? Instance { get; }
        public static Image? passBomb = NebulaAPI.AddonAsset.GetResource("passBomb.png")!.AsImage(115f)!;
        public GameActionType PassBombAction = new("bomber.passbomb", Bomber.MyRole);

        static BombSystem() => DIManager.Instance.RegisterModule(() => new BombSystem());
        private BombSystem()
        {
            ModSingleton<BombSystem>.Instance = this;
            this.RegisterPermanently();
        }
        void OnGameStart(GameStartEvent _)
        {
            try
            {
                SetTmPro();
                leftBombTime = float.MaxValue;
                if (GamePlayer.LocalPlayer != null)
                {
                    ObjectTracker<GamePlayer>? passTracker = ObjectTrackers.ForPlayer(
                        NebulaAPI.CurrentGame,
                        null,
                        GamePlayer.LocalPlayer,
                        (p) => ObjectTrackers.StandardPredicate(p) && GamePlayer.LocalPlayer == Bomber.Ability.bombPlayer && p != Bomber.Ability.bombPlayer,
                        null
                    );
                    passBombButton = new ModAbilityButtonImpl(true).RegisterPermanently().KeyBind((VirtualKeyInput)120).SetLabel("bomber.passbomb").SetSprite(passBomb?.GetSprite());

                    passBombButton.Availability = _ => GamePlayer.LocalPlayer.CanMove && passTracker.CurrentTarget != null && passTracker.CurrentTarget != Bomber.Ability.bombPlayer;
                    if (!GamePlayer.LocalPlayer.TryGetAbility<Bomber.Ability>(out var ability))
                    {
                        passBombButton.Visibility = _ => !GamePlayer.LocalPlayer.IsDead && GamePlayer.LocalPlayer == Bomber.Ability.bombPlayer && Bomber.Ability.leftbombtime <= Bomber.BombExplodeTime;
                    }
                    else
                        passBombButton.Visibility = _ => !GamePlayer.LocalPlayer.IsDead && GamePlayer.LocalPlayer == Bomber.Ability.bombPlayer;

                    passBombButton.CoolDownTimer = NebulaAPI.Modules.Timer(NebulaAPI.CurrentGame!, 2.5f).SetAsAbilityTimer().Start(null);

                    passBombButton.OnClick = _ =>
                    {
                        var target = passTracker.CurrentTarget;
                        if (target != null)
                        {
                            NebulaGameManager.Instance?.RpcDoGameAction(GamePlayer.LocalPlayer, GamePlayer.LocalPlayer.Position, PassBombAction);
                            passBombButton.StartCoolDown();
                            Bomber.SetBombPlayer.Invoke((target, true, Bomber.Ability.leftbombtime));
                            Bomber.SetBombPlayer.Invoke((GamePlayer.LocalPlayer, false, float.MaxValue));
                            NebulaAPI.RunEvent(new BombPassEvent(GamePlayer.LocalPlayer, target, Bomber.Ability.leftbombtime));
                        }
                    };
                }
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }

        void OnUpdata(GameUpdateEvent _)
        {
            if (GamePlayer.LocalPlayer == null)
                return;
            if (GamePlayer.LocalPlayer == bombPlayer)
            {
                if (leftBombTime != float.MaxValue && leftBombTime >= 0f)
                    leftBombTime -= Time.deltaTime;
                if (GamePlayer.LocalPlayer.TryGetAbility<Bomber.Ability>(out var ability))
                {
                    tmPro?.gameObject.SetActive(true);
                    tmPro?.text = Language.Translate("role.bomber.leftexplodetime").Replace("%TIME%", Mathf.CeilToInt(leftBombTime).ToString());
                    tmPro?.transform.localPosition = new Vector3(-0.07f, -2.5f, 0f);
                }
                else if (leftBombTime <= BombExplodeTime)
                {
                    tmPro?.gameObject.SetActive(true);
                    tmPro?.text = Language.Translate("role.bomber.leftexplodetime").Replace("%TIME%", Mathf.CeilToInt(leftBombTime).ToString());
                    tmPro?.transform.localPosition = new Vector3(-0.07f, -2.5f, 0f);
                }
                if (leftBombTime <= 0f)
                {
                    tmPro?.gameObject.SetActive(false);
                    var killParam = KillParameter.RemoteKill | KillParameter.WithoutSelfSE;
                    killParam &= ~KillParameter.WithOverlay;
                    if (!BombKillLeftDeadBody)
                    {
                        killParam &= ~KillParameter.WithDeadBody;
                    }
                    myplayer?.MurderPlayer(GamePlayer.LocalPlayer!, explosion, EventDetails.Kill, killParam, KillCondition.TargetAlive | KillCondition.InTaskPhase);
                }
            }
        }

        void SetTmPro()
        {
            if (this.tmPro == null)
            {
                GameObject? TextHolder = UnityHelper.CreateObject("BoomTime", DestroyableSingleton<HudManager>.Instance.KillButton.transform.parent, UnityEngine.Vector3.zero, null);
                NebulaAPI.CurrentGame?.BindGameObject(TextHolder.gameObject);
                TextMeshPro? tmPro = null;
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
                    this.tmPro = tmPro;
                    tmPro?.gameObject.SetActive(false);
                }
            }
        }

        void IGameOperator.OnReleased()
        {
            GameObject.Destroy(tmPro?.gameObject);
            leftBombTime = float.MaxValue;
            bombPlayer = null;
        }
    }
    private Bomber() : base("bomber", NebulaTeams.ImpostorTeam.Color, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, [GetBombCooldown, ActivateBombTime, BombExplodeTime, AfterMeetingResetBombTime, BombKillLeftDeadBody])
    {
    }
    public static readonly IRelativeCooldownConfiguration GetBombCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.bomber.getbombCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 5f, (0.125f, 2f, 0.125f), 1.125f);
    public static readonly FloatConfiguration ActivateBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.activatebombTime", (0f, 60f, 1f), 5f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration BombExplodeTime = NebulaAPI.Configurations.Configuration("options.role.bomber.bombExplodeTime", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration AfterMeetingResetBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.afterMeetingResetBomb", true);
    public static readonly BoolConfiguration BombKillLeftDeadBody = NebulaAPI.Configurations.Configuration("options.role.bomber.bombkillleftDeadbody", false);

    public static IDividedSpriteLoader ExplosionSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ExplosionAnim.png", 100f, 4, 2);

    public static Image? getBomb = NebulaAPI.AddonAsset.GetResource("getBomb.png")!.AsImage(115f)!;
    public static TranslatableTag explosion = new("state.explosion");
    Citation? HasCitation.Citation => DHMOCitations.GGD;

    static public Bomber MyRole = new();

    public static float GetBombTotalTime() => ActivateBombTime + BombExplodeTime;

    public static bool OnReportDeadBody(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        try
        {
            if (target == null)
            {
                return true;
            }
            var reporter = __instance.ToGamePlayer();
            var reported = GamePlayer.GetPlayer(target.PlayerId);
            if (reporter != null && reporter.Role.GetAbility<Bomber.Ability>() != null && reported != null)
            {
                if (reported.TryGetAbility<Bait.Ability>(out var ability) || reported.Modifiers.Any(r => r.Modifier.InternalName is "baitM"))
                {
                    if (NebulaGameManager.Instance!.HavePassed(reported.DeathTime ?? NebulaGameManager.Instance.CurrentTime, Mathf.Min(0.5f, (NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelay")?.Value ?? 0f) + NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelayDispersion")?.Value ?? 0f) + 1f))
                    {
                        return true;
                    }
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            DLog.Log(e);
        }
        return true;
    }

    private static IEnumerator CoPlayExplosion(Vector2 pos)
    {
        NebulaAsset.PlaySE(NebulaAudioClip.ExplosionNear, pos, 20f, 20f);

        var explosion = UnityHelper.CreateObject<SpriteRenderer>("Explosion", null, pos.AsVector3(-10f));

        for (int i = 0; i < 8; i++)
        {
            explosion.sprite = ExplosionSprite.GetSprite(i);
            yield return Effects.Wait(0.12f);
        }

        GameObject.Destroy(explosion.gameObject);
    }

    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;
    public static RemoteProcess<(GamePlayer? target, bool bomb, float leftbombTime)> SetBombPlayer = new("SetBombPlayer", (message, _) =>
    {
        Ability.isfirstappear = true;
        if (message.bomb == false && message.target != null)
        {
            Ability.leftbombtime = float.MaxValue;
            Ability.bombPlayer = null;
            return;
        }
        if (message.bomb)
        {
            if (message.target != null)
            {
                Ability.bombPlayer = message.target;
                if (message.target.AmOwner && message.leftbombTime <= BombExplodeTime)
                {
                    BombSystem.Instance?.passBombButton?.StartCoolDown();
                    BombSystem.Instance?.passBombButton?.PlayFlashOnce();
                }
                Ability.leftbombtime = message.leftbombTime;
            }
        }
    });

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static TextMeshPro? tmp;
        public static ModAbilityButton? bombButton;
        public static GamePlayer? bombPlayer;
        public static float leftbombtime;
        public static bool isfirstappear;
        bool IPlayerAbility.HideKillButton => bombButton != null && !bombButton.IsBroken;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        static void OnGameStarted(GameStartEvent ev)
        {
            leftbombtime = float.MaxValue;
        }

        [OnlyHost]
        static void OnBombPlayerDead(PlayerDieEvent ev)
        {
            if (ev.Player == bombPlayer)
            {
                SetBombPlayer.Invoke((ev.Player, false, float.MaxValue));
            }
        }

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                bombButton = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: true)
                 .BindKey(VirtualKeyInput.Kill)
                 .SetLabel("bomber.getbomb")
                 .SetLabelType(ModAbilityButton.LabelType.Impostor);

                bombButton.Availability = _ => MyPlayer.CanMove && MyPlayer != bombPlayer;
                bombButton.Visibility = _ => !MyPlayer.IsDead;
                bombButton.SetImage(getBomb!);
                bombButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, GetBombCooldown.Cooldown).SetAsAbilityTimer().Start(null);

                bombButton.OnClick = _ =>
                {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, BombSystem.Instance?.PassBombAction!);
                    bombButton.StartCoolDown();
                    SetBombPlayer.Invoke((MyPlayer, true, GetBombTotalTime()));
                };

                bombButton.OnBroken = _ =>
                {
                    Snatcher.RewindKillCooldown();
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(bombButton.GetKillButtonLike());
            }

            //BombTimeAction
            if (tmp == null)
            {
                try
                {
                    GameObject? TextHolder = UnityHelper.CreateObject("BoomTime", DestroyableSingleton<HudManager>.Instance.KillButton.transform.parent, UnityEngine.Vector3.zero, null);
                    NebulaAPI.CurrentGame?.BindGameObject(TextHolder.gameObject);
                    TextMeshPro? tmPro = null;
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
                    GameOperatorManager? instance = GameOperatorManager.Instance;
                    if (instance != null)
                    {
                        instance.Subscribe(delegate (GameUpdateEvent ev)
                        {
                            if (tmPro)
                            {
                                if (GamePlayer.LocalPlayer! == bombPlayer && GamePlayer.LocalPlayer!.IsDead)
                                {
                                    tmPro!.gameObject.SetActive(false);
                                }
                                if (!MeetingHud.Instance && !ExileController.Instance && GamePlayer.LocalPlayer! == bombPlayer)
                                {
                                    leftbombtime -= Time.deltaTime;
                                    if (MyPlayer == bombPlayer)
                                    {
                                        if (isfirstappear)
                                        {
                                            BombSystem.Instance?.passBombButton?.PlayFlashOnce();
                                            BombSystem.Instance?.passBombButton?.StartCoolDown();
                                        }
                                        isfirstappear = false;
                                        tmPro!.gameObject.SetActive(true);
                                        tmPro.text = Language.Translate("role.bomber.leftexplodetime").Replace("%TIME%", Mathf.CeilToInt(leftbombtime).ToString());
                                        tmPro.transform.localPosition = new Vector3(-0.07f, -2.5f, 0f);
                                    }
                                    else if (leftbombtime <= BombExplodeTime && !GamePlayer.LocalPlayer!.TryGetAbility<Bomber.Ability>(out _))
                                    {
                                        if (isfirstappear)
                                        {
                                            BombSystem.Instance?.passBombButton?.PlayFlashOnce();
                                            BombSystem.Instance?.passBombButton?.StartCoolDown();
                                        }
                                        isfirstappear = false;
                                        tmPro!.gameObject.SetActive(true);
                                        tmPro.text = Language.Translate("role.bomber.leftexplodetime").Replace("%TIME%", Mathf.CeilToInt(leftbombtime).ToString());
                                        tmPro.transform.localPosition = new Vector3(-0.07f, -2.5f, 0f);
                                    }
                                    if (leftbombtime <= 0f)
                                    {
                                        var killParam = KillParameter.RemoteKill | KillParameter.WithoutSelfSE;
                                        killParam &= ~KillParameter.WithOverlay;
                                        if (!BombKillLeftDeadBody)
                                        {
                                            killParam &= ~KillParameter.WithDeadBody;
                                        }
                                        MyPlayer!.MurderPlayer(GamePlayer.LocalPlayer!, explosion, EventDetails.Kill, killParam, KillCondition.TargetAlive | KillCondition.InTaskPhase);
                                        var pos = GamePlayer.LocalPlayer!.Position;
                                        NebulaSyncObject.RpcInstantiate(BombEvidence.MyTag, [pos.x, pos.y]);
                                        SetBombPlayer.Invoke((GamePlayer.LocalPlayer, false, 0f));
                                        NebulaManager.Instance.StartCoroutine(CoPlayExplosion(pos).WrapToIl2Cpp());
                                        tmPro?.gameObject.SetActive(false);
                                    }
                                }
                                else
                                {
                                    tmPro?.gameObject.SetActive(false);
                                }
                            }
                        }, NebulaAPI.CurrentGame!, 100);
                        instance.Subscribe<TaskPhaseRestartEvent>(ev =>
                        {
                            if (AfterMeetingResetBombTime)
                            {
                                leftbombtime = BombExplodeTime;
                            }
                        }, NebulaAPI.CurrentGame!);
                        if (tmPro != null)
                        {
                            tmp = tmPro;
                        }
                    }
                }
                catch (Exception e)
                {
                    DLog.Log(e);
                }
            }
        }
        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player == bombPlayer && ev.Player.PlayerState != explosion)
            {
                SetBombPlayer.Invoke((ev.Player, false, 0f));
            }
        }
    }

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class BombEvidence(Vector2 pos) : NebulaSyncStandardObject(pos, NebulaSyncStandardObject.ZOption.Back, true, evidenceSprite.GetSprite(), false), IGameOperator
    {
        static BombEvidence() => RegisterInstantiater(MyTag, args => new BombEvidence(new Vector2(args[0], args[1])));
        public static string MyTag = "BomberEvidence";
        static Image evidenceSprite = NebulaAPI.AddonAsset.GetResource("bombEvidence.png")!.AsImage()!;
    }
}*/