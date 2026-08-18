namespace DHMO.Roles.Impostor;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public class BombEvidence(VVector2 pos) : NebulaSyncStandardObject(pos, NebulaSyncStandardObject.ZOption.Back, false, evidenceSprite.GetSprite(), false), IGameOperator
{
    static BombEvidence() => RegisterInstantiater(MyTag, args => new BombEvidence(new VVector2(args[0], args[1])));
    public static string MyTag = "BomberEvidence";
    internal static Image evidenceSprite = NebulaAPI.AddonAsset.GetResource("BombEvidence.png")?.AsImage()!;
}

[NebulaRPCHolder]
public class Bomb : FlexibleLifespan, IGameOperator, IBindPlayer
{
    private GamePlayer myPlayer { get; set; }
    public GamePlayer Bomber { get; private set; }
    public GameTimer? Timer { get; private set; }

    public GamePlayer MyPlayer => myPlayer;

    public static GameActionType PassBombAction = new("bomber.passbomb", Roles.Impostor.Bomber.MyRole);
    public static Image passBombImage = NebulaAPI.AddonAsset.GetResource("Button/PassBombButton.png")?.AsImage(115f)!;
    public static IDividedSpriteLoader ExplosionSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ExplosionAnim.png", 120f, 4, 2);
    public static TranslatableTag explosion = new("state.bomb.explosion");

    public Bomb(GamePlayer player, GamePlayer bomber, float duration)
    {
        this.myPlayer = player;
        Bomber = bomber;

        if (player.AmOwner)
        {
            Timer = NebulaAPI.Modules.Timer(this, duration);
            Timer.Start();

            var passTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), null);
            var passButton = NebulaAPI.Modules.AbilityButton(this, true, false)
                .BindKey((VirtualKeyInput)120).SetLabel("game.passBomb").SetImage(passBombImage);

            passButton.Visibility = _ => MyPlayer.IsAlive && (MyPlayer.PlayerId == Bomber.PlayerId || Timer.CurrentTime <= global::DHMO.Roles.Impostor.Bomber.BombExplosionDuration);
            passButton.Availability = _ => MyPlayer.CanMove && passTracker.CurrentTarget != null;
            passButton.PlayFlashWhile = _ => true;
            passButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, MyPlayer.PlayerId == Bomber.PlayerId ? 0f : 3f).SetAsAbilityTimer().Start(null);

            passButton.OnClick = _ =>
            {
                if (passTracker.CurrentTarget == null) return;
                RPCSetBomb.Invoke((passTracker.CurrentTarget, Bomber, Timer.CurrentTime));
                NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, PassBombAction);
                this.Release();
            };
            passButton.ShowUsesIcon(0, Timer.TimerText ?? "0");
            passButton.OnUpdate = _ => passButton.UpdateUsesIcon(Timer.TimerText ?? "0");
        }
    }

    private static IEnumerator CoPlayExplosion(VVector2 pos)
    {
        NebulaAsset.PlaySE(NebulaAudioClip.ExplosionNear, pos, 5f, 5f);
        var explosion = UnityHelper.CreateObject<SpriteRenderer>("Explosion", null, pos.AsVector3(-10f));
        for (int i = 0; i < 8; i++)
        {
            explosion.sprite = ExplosionSprite.GetSprite(i);
            yield return Effects.Wait(0.12f);
        }
        explosion.gameObject.Destroy();
    }

    void OnUpdate(GameUpdateEvent ev)
    {
        if (Timer == null || Timer.IsProgressing) return;
        RpcExplode.Invoke((myPlayer, Bomber, myPlayer.TruePosition));
        Release();
    }
    
    void OnMeetingPreStart(MeetingPreStartEvent ev) => Timer?.Pause();

    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        if (global::DHMO.Roles.Impostor.Bomber.AfterMeetingResetBombTime)
            Timer?.Start();
        else
            Timer?.Resume();
    }

    public readonly static RemoteProcess<(GamePlayer player, GamePlayer bomber, float duration)> RPCSetBomb = new("BomberSetBomb", (message, _) =>
    {
        if (!message.player.AmOwner) return;
        var bomb = new Bomb(message.player, message.bomber, message.duration);
        bomb.RegisterPermanently();
    });

    static private RemoteProcess<(GamePlayer, GamePlayer, VVector2)> RpcExplode = new("PlayBombExplode", (message, _) =>
    {
        var killParam = KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE;
        if (Impostor.Bomber.BombKillLeftDeadBody)
            killParam |= KillParameter.WithDeadBody;

        var ev = NebulaAPI.RunEvent(new BombExplodeEvent(message.Item1));
        NebulaManager.Instance.StartCoroutine(CoPlayExplosion(message.Item3).WrapToIl2Cpp());

        if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
        {
            message.Item2.MurderPlayer(ev.Player, explosion, explosion, killParam, KillCondition.TargetAlive | KillCondition.InTaskPhase);
            NebulaSyncObject.RpcInstantiate(BombEvidence.MyTag, [ev.Player.Position.x, ev.Player.Position.y]);
        }
    });
}

public class Bomber : DefinedSingleAbilityRoleTemplate<Bomber.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Bomber() : base("bomber", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, 
        [IgniteBombCooldown, IgniteBombOn, CanVanillaKill, SyncKillAndIgniteCooldown, ActivateBombDuration, BombExplosionDuration, AfterMeetingResetBombTime, BombKillLeftDeadBody])
    {
    }

    public static readonly IRelativeCooldownConfiguration IgniteBombCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.bomber.igniteBombCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 5f, (0.125f, 2f, 0.125f), 1.125f);
    private static readonly ValueConfiguration<int> IgniteBombOn = NebulaAPI.Configurations.Configuration("options.role.bomber.igniteBombOn", ["options.role.bomber.igniteBombOn.self", "options.role.bomber.igniteBombOn.other"], 0);
    public static readonly BoolConfiguration CanVanillaKill = NebulaAPI.Configurations.Configuration("options.role.bomber.canVanillaKill", false);
    public static readonly BoolConfiguration SyncKillAndIgniteCooldown = NebulaAPI.Configurations.Configuration("options.role.bomber.syncKillAndIgniteCooldown", true, () => CanVanillaKill);
    public static readonly FloatConfiguration ActivateBombDuration = NebulaAPI.Configurations.Configuration("options.role.bomber.activateBombDuration", (0f, 60f, 1f), 5f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration BombExplosionDuration = NebulaAPI.Configurations.Configuration("options.role.bomber.bombExplosionDuration", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration AfterMeetingResetBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.afterMeetingResetBomb", true);
    public static readonly BoolConfiguration BombKillLeftDeadBody = NebulaAPI.Configurations.Configuration("options.role.bomber.bombKillLeftDeadbody", false);

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/BomberIcon.png")?.AsImage();
    Citation? HasCitation.Citation => DHMOCitations.GGD;

    static public Bomber MyRole = new();

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(igniteBombImage!, "role.bomber.ability.ignite");
        yield return new(Bomb.passBombImage, "role.bomber.ability.pass");
        yield return new(BombEvidence.evidenceSprite, "role.bomber.ability.evidence");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%ACTIVE%", ActivateBombDuration.GetValue().ToString());
        yield return new("%EXPLODE%", BombExplosionDuration.GetValue().ToString());
        yield return new("%OF%", IgniteBombOn.GetValue() == 0 ? Language.Translate("role.bomber.ability.self") : Language.Translate("role.bomber.ability.other"));
    }

    public static Image? igniteBombImage = NebulaAPI.AddonAsset.GetResource("Button/IgniteBombButton.png")?.AsImage(115f)!;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public static ModAbilityButton? igniteBomb;
        bool IPlayerAbility.HideKillButton => (!igniteBomb?.IsBroken ?? false) || !CanVanillaKill;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        float time = ActivateBombDuration + BombExplosionDuration;
        private ObjectTracker<GamePlayer>? igniteTracker = null;

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                if (IgniteBombOn.GetValue() == 1)
                    igniteTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), VColor.ImpostorColor.ToUnityColor(), false, false);

                igniteBomb = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: !CanVanillaKill).BindKey(CanVanillaKill ? VirtualKeyInput.Ability : VirtualKeyInput.Kill)
                 .SetLabel("bomber.ignitebomb")
                 .SetLabelType(ModAbilityButton.LabelType.Impostor)
                 .SetAsUsurpableButton(this);

                igniteBomb.Visibility = _ => MyPlayer.IsAlive;
                igniteBomb.Availability = _ => MyPlayer.CanMove && (igniteTracker == null || igniteTracker.CurrentTarget != null);
                igniteBomb.SetImage(igniteBombImage!);

                var timer = NebulaAPI.Modules.Timer(this, IgniteBombCooldown.Cooldown);
                if (CanVanillaKill)
                    timer.SetAsAbilityTimer();
                else
                    timer.SetAsKillCoolTimer();

                igniteBomb.CoolDownTimer = timer.Start();

                igniteBomb.OnClick = button =>
                {
                    if (igniteTracker != null)
                    {
                        var target = igniteTracker.CurrentTarget;
                        if (target != null) Bomb.RPCSetBomb.Invoke((target, MyPlayer, time));
                    }
                    else
                        Bomb.RPCSetBomb.Invoke((MyPlayer, MyPlayer, time));

                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, Bomb.PassBombAction);

                    if (SyncKillAndIgniteCooldown && CanVanillaKill)
                        NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    else
                        button.StartCoolDown();
                };

                if (!CanVanillaKill)
                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(igniteBomb.GetKillButtonLike());
            }
        }
    }
}

public class BombExplodeEvent(GamePlayer player) : Virial.Events.Player.AbstractPlayerEvent(player)
{
}