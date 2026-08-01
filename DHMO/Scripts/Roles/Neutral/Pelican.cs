namespace DHMO.Roles.Neutral;

public class Pelican : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    public static readonly RoleTeam PelicanTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.pelican", new(0, 153, 76), TeamRevealType.OnlyMe);
    private Pelican() : base("pelican", PelicanTeam.Color, RoleCategory.NeutralRole, PelicanTeam, [DevourCooldown, IncreaseTime, new GroupConfiguration("options.role.pelican.group.pelicanTime", [InvokePelicanTime, PelicanTimeAliveNum, PelicanTimeDuration, TaskPhaseRestartPelicanTimeDisperse], PelicanTeam.Color.RGBMultiplied(0.65f)), VentConfiguration])
    {
    }

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration DevourCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.pelican.devourCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly FloatConfiguration IncreaseTime = NebulaAPI.Configurations.Configuration("options.role.pelican.increaseTime", (0f, 20f, 1f), 5f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration InvokePelicanTime = NebulaAPI.Configurations.Configuration("options.role.pelican.pelicanTime", true);
    internal static readonly IntegerConfiguration PelicanTimeAliveNum = NebulaAPI.Configurations.Configuration("options.role.pelican.pelicanTimeAlived", (2, 23), 4, () => InvokePelicanTime);
    internal static readonly FloatConfiguration PelicanTimeDuration = NebulaAPI.Configurations.Configuration("options.role.pelican.pelicanTimeDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second, () => InvokePelicanTime);
    internal static readonly BoolConfiguration TaskPhaseRestartPelicanTimeDisperse = NebulaAPI.Configurations.Configuration("options.role.pelican.taskPhaseRestartEnterPelicanTimeDisperse", true, () => InvokePelicanTime);
    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.pelican.vent", true);

    private static readonly Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/DevourButton.png")?.AsImage(115f);
    public static readonly TranslatableTag Digestion = new("state.digestion");

    public static Pelican MyRole = new();

    bool DefinedRole.IsKiller => true;

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonImage!, "role.pelican.ability.devour");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%WIN%", InvokePelicanTime ? Language.Translate("role.pelican.winCond.pelicanTimeOver") : "");
        yield return new("%PELICANTIME%", InvokePelicanTime ? Language.Translate("role.pelican.ability.pelicanTime") : "");
        yield return new("%NUM%", PelicanTimeAliveNum.GetValue().ToString());
        yield return new("%DURATION%", PelicanTimeDuration.GetValue().ToString());
    }

    [NebulaRPCHolder]
    public class Instance(GamePlayer player) : RuntimeVentRoleTemplate(player, VentConfiguration), RuntimeRole
    {
        public static GameEnd? PelicanTeamWin = NebulaAPI.Preprocessor?.CreateEnd("pelican", MyRole.RoleColor);
        public int DevouringTotal { get; set; } = 0;
        public override DefinedRole Role => MyRole;

        void ClaimPelicanTeamRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Pelican.PelicanTeam) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        [OnlyHost]
        void CheckWin(GameUpdateEvent ev)
        {
            var alivePlayers = AddonHelper.AlivePlayers;
            int totalAlive = alivePlayers.Length;
            int totalDevoured = alivePlayers.Select(p =>
            {
                p.TryGetRole<Pelican.Instance>(out var pelican);
                return pelican;
            }).NotNull().Sum(p => p.DevouringTotal);

            if (NebulaAPI.RunEvent(new KillerTeamCallback(PelicanTeam)).RemainingOtherTeam || (totalAlive - totalDevoured) > 1) return;

            if (MyPlayer.IsAlive && PelicanTeamWin != null)
                NebulaAPI.CurrentGame?.TriggerGameEnd(PelicanTeamWin, GameEndReason.Situation, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
        }

        [OnlyHost]
        void OnPelicanTimeEnd(TimeMomentEndEvent ev)
        {
            if (ev.IsTimeOver && ev.TimeMoment.Id == "pelican" && PelicanTeamWin != null && MyPlayer.IsAlive)
                NebulaGameManager.Instance?.RpcInvokeSpecialWin(PelicanTeamWin, 1 << MyPlayer.PlayerId);
        }

        public override void OnActivated() 
        {
            if (AmOwner)
            {
                var devourTracker = ObjectTrackers.ForPlayerlike(this, NebulaAPI.AmongUs.VanillaKillDistance + 0.25f, MyPlayer, p => ObjectTrackers.PlayerlikeLocalKillablePredicate(p), MyRole.UnityColor, false, false);
                var devourButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false)
                    .BindKey(VirtualKeyInput.Kill).SetImage(buttonImage!).SetLabel("pelican.devour").SetColorLabel(MyRole.RoleColor);

                devourButton.Visibility = _ => MyPlayer.IsAlive;
                devourButton.Availability = _ => devourTracker.CurrentTarget is not null && MyPlayer.CanMove;
                devourButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, DevourCooldown.Cooldown).SetAsKillCoolTimer().Start();

                devourButton.OnClick = button =>
                {
                    var target = devourTracker.CurrentTarget;
                    if (target == null) return;

                    if (target is IFakePlayer || target.RealPlayer.TryGetAbility<Bait.Ability>(out var a) || target.RealPlayer.Modifiers.Any(m => m.Modifier.InternalName.Contains("bait")))
                    {
                        var cancelable = NebulaAPI.RunEvent(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target));
                        if (!(cancelable?.IsCanceled ?? false))
                            MyPlayer.MurderPlayer(target, Digestion, null, KillParameter.NormalKill);

                        if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                        return;
                    }

                    RpcDevoured.Invoke((target.RealPlayer, MyPlayer));
                    devourButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, GetCurrentCooldown()).SetAsKillCoolTimer();
                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                };

                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(devourButton.GetKillButtonLike());
            }

            GameOperatorManager.Instance?.Subscribe<DevouredStatusUpdate>(ev =>
            {
                if (ev.Pelican != myPlayer) return;
                if (ev.Devoured)
                    DevouringTotal++;
                else
                    DevouringTotal--;
            }, this);
        }

        float GetCurrentCooldown() => Mathn.Min(DevourCooldown.Cooldown + DevouringTotal * IncreaseTime, 60f);

        public readonly static RemoteProcess<(GamePlayer player, GamePlayer pelican)> RpcDevoured = new("PelicanDevour", (message, _) =>
        {
            if (!message.player.AmOwner || message.pelican.Role is not Pelican.Instance pelican) return;
            DevouredAbility devoured = new(message.player, message.pelican);
            devoured.Register(pelican);
        });
    }

    [NebulaRPCHolder]
    public class DevouredAbility : FlexibleLifespan, IGameOperator, IBindPlayer
    {
        private GamePlayer myPlayer;
        private GamePlayer pelican;
        string tag = "PelicanDevouredInvisble";

        public GamePlayer Pelican => pelican;
        public GamePlayer MyPlayer => myPlayer;

        public DevouredAbility(GamePlayer player, GamePlayer pelican)
        {
            this.myPlayer = player;
            this.pelican = pelican;

            RpcUpdateStatus.Invoke((player, pelican, true));
            player.GainAttribute(PlayerAttributes.Invisible, 100000f, false, 0, tag);
            if (player.AmOwner)
            {
                AmongUsUtil.SetCamTarget(pelican.VanillaPlayer);
            }
            player.VanillaPlayer.NetTransform.RpcSnapTo(new VVector2(-100f, 10f));
        }

        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            pelican.MurderPlayer(myPlayer, global::DHMO.Roles.Neutral.Pelican.Digestion, null, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE, KillCondition.BothAlive);
        }

        void OnPlayerDieOrDisconnect(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player == myPlayer || ev.Player == pelican)
                this.Release();
        }

        [OnlyMyPlayer]
        void OnBombExplode(BombExplodeEvent ev)
        {
            ev.Recycle(pelican);
        }

        void IGameOperator.OnReleased()
        {
            RpcUpdateStatus.Invoke((myPlayer, pelican, false));
            myPlayer.RemoveAttributeByTag(tag);
            myPlayer.VanillaPlayer.NetTransform.RpcSnapTo(pelican.TruePosition);
            AmongUsUtil.SetCamTarget(null);
        }

        public readonly static RemoteProcess<(GamePlayer player, GamePlayer pelican, bool devoured)> RpcUpdateStatus = new("PelicanUpdateStatus", (message, _) => GameOperatorManager.Instance?.Run(new DevouredStatusUpdate(message.player, message.pelican, message.devoured)));
    }

    public class DevouredStatusUpdate : Virial.Events.Player.AbstractPlayerEvent
    {
        public Virial.Game.Player Pelican { get; init; }
        public bool Devoured { get; init; }

        public DevouredStatusUpdate(Virial.Game.Player player, Virial.Game.Player pelican, bool devour) : base(player)
        {
            this.Pelican = pelican;
            this.Devoured = devour;
        }
    }
}