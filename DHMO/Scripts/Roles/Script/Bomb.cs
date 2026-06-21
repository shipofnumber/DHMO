namespace DHMO.Roles.Script;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public class BombEvidence(Vector2 pos) : NebulaSyncStandardObject(pos, NebulaSyncStandardObject.ZOption.Back, true, evidenceSprite.GetSprite(), false), IGameOperator
{
    static BombEvidence() => RegisterInstantiater(MyTag, args => new BombEvidence(new Vector2(args[0], args[1])));
    public static string MyTag = "BomberEvidence";
    static Image evidenceSprite = NebulaAPI.AddonAsset.GetResource("BombEvidence.png")?.AsImage()!;
}

[NebulaRPCHolder]
public class Bomb : FlexibleLifespan, IGameOperator, IBindPlayer
{
    private GamePlayer Owner { get; set; }
    public GamePlayer Bomber { get; private set; }

    public TimerImpl? Timer { get; private set; } = null;

    GamePlayer IBindPlayer.MyPlayer => Owner;

    public static IEnumerable<byte> HasBomb { get => hasBomb; }
    internal static List<byte> hasBomb { get; private set; } = [];

    public GameActionType PassBombAction = new("bomber.passbomb", Roles.Impostor.Bomber.MyRole);
    public static Image passBombImage = NebulaAPI.AddonAsset.GetResource("Button/PassBombButton.png")?.AsImage(115f)!;
    public static IDividedSpriteLoader ExplosionSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ExplosionAnim.png", 120f, 4, 2);
    public static TranslatableTag explosion = new("state.bomber.explosion");

    public Bomb(GamePlayer owner, GamePlayer bomber, float duration)
    {
        this.Owner = owner;
        this.Bomber = bomber;

        if (Owner.AmOwner)
        {
            Timer = new TimerImpl(duration).Register(this);
            Timer.Start();

            var passTracker = ObjectTrackers.ForPlayer(this, null, Owner, (p) => ObjectTrackers.StandardPredicate(p) && !hasBomb.Contains(p.PlayerId), null);

            var passButton = NebulaAPI.Modules.AbilityButton(this, true, false).BindKey((VirtualKeyInput)120);
            passButton.SetLabel("game.passBomb");
            passButton.SetImage(passBombImage);
            passButton.Visibility = _ => Owner.IsAlive && (Owner.PlayerId == Bomber.PlayerId || Timer.CurrentTime <= global::DHMO.Roles.Impostor.Bomber.BombExplodeTime);
            passButton.Availability = _ => Owner.CanMove && passTracker.CurrentTarget != null;
            passButton.PlayFlashWhile = _ => true;
            passButton.OnClick = _ =>
            {
                if (passTracker.CurrentTarget != null)
                {
                    RPCSetBomb.Invoke((passTracker.CurrentTarget, Bomber, Timer.CurrentTime));
                    NebulaGameManager.Instance?.RpcDoGameAction(Owner, Owner.Position, PassBombAction);
                    this.Release();
                }
            };
            passButton.ShowUsesIcon(0, GetBombTime().ToString());
            passButton.OnUpdate = _ =>
            {
                if (passButton.IsVisible)
                    passButton.UpdateUsesIcon(GetBombTime().ToString());
            };
        }
    }

    int GetBombTime() => Mathn.CeilToInt(Timer?.CurrentTime ?? 0f);

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

    void OnUpdata(GameUpdateEvent ev)
    {
        if (Timer != null) 
        {
            if (Timer.isActive && GetBombTime() <= 0f)
            {
                Timer.Pause().SetTime(0f);
                var killParam = KillParameter.RemoteKill | KillParameter.WithoutSelfSE;
                killParam &= ~KillParameter.WithOverlay;

                if (!Impostor.Bomber.BombKillLeftDeadBody)
                    killParam &= ~KillParameter.WithDeadBody;

                Bomber.MurderPlayer(Owner, explosion, EventDetails.Kill, killParam, KillCondition.TargetAlive | KillCondition.InTaskPhase);
                NebulaSyncObject.RpcInstantiate(BombEvidence.MyTag, [Owner.Position.x, Owner.Position.y]);
                NebulaManager.Instance.StartCoroutine(CoPlayExplosion(Owner.Position).WrapToIl2Cpp());
            }
        }
    }

    [OnlyMyPlayer]
    void OnDeadOrDisconnect(PlayerDieOrDisconnectEvent ev) => this.Release();

    void OnMeetingStart(MeetingPreStartEvent ev) => Timer?.Pause();

    void ResetVentCoolDownOnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        if (Impostor.Bomber.AfterMeetingResetBombTime)
            Timer?.Start();
        else
            Timer?.Resume();
    }

    void IGameOperator.OnReleased() => RPCRemovePlayer(Owner);

    public readonly static RemoteProcess<(GamePlayer player, GamePlayer bomber, float duration)> RPCSetBomb = new("BomberSetBomb", (message, _) =>
    {
        if (message.player.AmOwner)
        {
            Bomb bomb = new(message.player, message.bomber, message.duration);
            if (NebulaAPI.CurrentGame != null)
                bomb.Bind(NebulaAPI.CurrentGame);
            bomb.RegisterSelf();
            Bomb.hasBomb.Add(message.player.PlayerId);
        }
    }, false);

    [NebulaRPC]
    public static void RPCRemovePlayer(GamePlayer player)
    {
        Bomb.hasBomb.Remove(player.PlayerId);
    }
}