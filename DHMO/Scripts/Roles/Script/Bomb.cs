namespace DHMO.Roles.Script;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public class BombEvidence(VVector2 pos) : NebulaSyncStandardObject(pos, NebulaSyncStandardObject.ZOption.Back, true, evidenceSprite.GetSprite(), false), IGameOperator
{
    static BombEvidence() => RegisterInstantiater(MyTag, args => new BombEvidence(new VVector2(args[0], args[1])));
    public static string MyTag = "BomberEvidence";
    static Image evidenceSprite = NebulaAPI.AddonAsset.GetResource("BombEvidence.png")?.AsImage()!;
}

[NebulaRPCHolder]
public class Bomb : FlexibleLifespan, IGameOperator, IBindPlayer
{
    private GamePlayer Owner { get; set; }
    public GamePlayer Bomber { get; private set; }
    public TimerImpl? Timer { get; private set; }

    GamePlayer IBindPlayer.MyPlayer => Owner;

    public GameActionType PassBombAction = new("bomber.passbomb", Roles.Impostor.Bomber.MyRole);
    public static Image passBombImage = NebulaAPI.AddonAsset.GetResource("Button/PassBombButton.png")?.AsImage(115f)!;
    public static IDividedSpriteLoader ExplosionSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ExplosionAnim.png", 120f, 4, 2);
    public static TranslatableTag explosion = new("state.bomber.explosion");

    public Bomb(GamePlayer owner, GamePlayer bomber, float duration)
    {
        Owner = owner;
        Bomber = bomber;

        if (Owner.AmOwner)
        {
            Timer = new TimerImpl(duration).Register(this).Start();

            var passTracker = ObjectTrackers.ForPlayer(this, null, Owner, p => ObjectTrackers.StandardPredicate(p), null);
            var passButton = NebulaAPI.Modules.AbilityButton(this, true, false)
                .BindKey((VirtualKeyInput)120).SetLabel("game.passBomb").SetImage(passBombImage);

            passButton.Visibility = _ => Owner.IsAlive && (Owner.PlayerId == Bomber.PlayerId || Timer.CurrentTime <= global::DHMO.Roles.Impostor.Bomber.BombExplodeTime);
            passButton.Availability = _ => Owner.CanMove && passTracker.CurrentTarget != null;
            passButton.PlayFlashWhile = _ => true;
            passButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, 3f).SetAsAbilityTimer().Start(null);

            passButton.OnClick = _ =>
            {
                if (passTracker.CurrentTarget == null) return;
                RPCSetBomb.Invoke((passTracker.CurrentTarget, Bomber, Timer.CurrentTime));
                NebulaGameManager.Instance?.RpcDoGameAction(Owner, Owner.Position, PassBombAction);
                Release();
            };
            passButton.ShowUsesIcon(0, GetBombTime().ToString());
            passButton.OnUpdate = _ => passButton.UpdateUsesIcon(GetBombTime().ToString());
        }

        GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev => Timer?.Pause(), this);
    }

    int GetBombTime() => Mathn.CeilToInt(Timer?.CurrentTime ?? 0f);

    private static IEnumerator CoPlayExplosion(VVector2 pos)
    {
        NebulaAsset.PlaySE(NebulaAudioClip.ExplosionNear, pos, 20f, 20f);
        var explosion = UnityHelper.CreateObject<SpriteRenderer>("Explosion", null, pos.AsVector3(-10f));
        for (int i = 0; i < 8; i++)
        {
            explosion.sprite = ExplosionSprite.GetSprite(i);
            yield return Effects.Wait(0.12f);
        }
        explosion.gameObject.Destroy();
    }

    void OnUpdata(GameUpdateEvent ev)
    {
        if (Timer == null || !Timer.isActive || GetBombTime() > 0f) return;
        Timer.Pause().SetTime(0f);
        BombExplode(Bomber, Owner);
        Release();
    }

    internal static void BombExplode(GamePlayer bomber, GamePlayer owner)
    {
        var killParam = KillParameter.RemoteKill | KillParameter.WithoutSelfSE & ~KillParameter.WithOverlay;
        if (!Impostor.Bomber.BombKillLeftDeadBody)
            killParam &= ~KillParameter.WithDeadBody;

        var ev = NebulaAPI.RunEvent(new BombExplodeEvent(owner));
        bomber.MurderPlayer(ev.Player, explosion, EventDetails.Kill, killParam, KillCondition.TargetAlive | KillCondition.InTaskPhase);
        NebulaSyncObject.RpcInstantiate(BombEvidence.MyTag, [ev.Player.Position.x, ev.Player.Position.y]);
        RpcExplode.Invoke(ev.Player.Position);
    }

    void ResetTimerOnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        if (Impostor.Bomber.AfterMeetingResetBombTime) Timer?.Start();
        else Timer?.Resume();
    }

    public readonly static RemoteProcess<(GamePlayer player, GamePlayer bomber, float duration)> RPCSetBomb = new("BomberSetBomb", (message, _) =>
    {
        if (!message.player.AmOwner) return;
        var bomb = new Bomb(message.player, message.bomber, message.duration);
        if (NebulaAPI.CurrentGame != null) bomb.Bind(NebulaAPI.CurrentGame);

        bomb.RegisterSelf();
    }, false);

    static private RemoteProcess<VVector2> RpcExplode = new("PlayBombExplode", (message, _) =>
        NebulaManager.Instance.StartCoroutine(CoPlayExplosion(message).WrapToIl2Cpp()));
}

public class BombExplodeEvent(Player player) : Virial.Events.Player.AbstractPlayerEvent(player) { }