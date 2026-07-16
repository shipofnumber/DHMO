namespace DHMO.Roles.Neutral;

public class Pelican : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder, IAssignableDocument
{
    public static readonly RoleTeam PelicanTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.pelican", new(0, 153, 76), TeamRevealType.OnlyMe);
    private Pelican() : base("pelican", PelicanTeam.Color, RoleCategory.NeutralRole, PelicanTeam, [DevourCooldown, CanReduceCDOption, ReduceTime, VentConfiguration]) { }

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration DevourCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.pelican.devourCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly BoolConfiguration CanReduceCDOption = NebulaAPI.Configurations.Configuration("options.role.pelican.canReduceCD", true);
    public static readonly FloatConfiguration ReduceTime = NebulaAPI.Configurations.Configuration("options.role.pelican.reduceTime", (0.5f, 10f, 0.5f), 3f, FloatConfigurationDecorator.Second, () => CanReduceCDOption);
    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.pelican.vent", true);

    private static Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("Button/DevourButton.png")?.AsImage(115f);
    public static TranslatableTag Digestion = new("state.digestion");

    public static readonly Pelican MyRole = new();
    bool DefinedRole.IsKiller => true;

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield return new(buttonImage!, "role.pelican.ability.devour"); }

    [NebulaRPCHolder]
    public class Instance(GamePlayer player) : RuntimeVentRoleTemplate(player, VentConfiguration), RuntimeRole
    {
        public static GameEnd? PelicanTeamWin = NebulaAPI.Preprocessor?.CreateEnd("pelican", MyRole.RoleColor);
        public override DefinedRole Role => MyRole;
        HashSet<byte> devouredPlayers = [];

        int[]? RuntimeAssignable.RoleArguments
        {
            get
            {
                int mask = 0;
                foreach (var id in devouredPlayers) mask |= 1 << id;
                return [mask];
            }
        }

        void ClaimPelicanTeamRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == Pelican.PelicanTeam) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        [OnlyHost]
        void WinCheck(GameUpdateEvent ev)
        {
            var totalAlive = AddonHelper.GetAlivePlayers();
            if (NebulaAPI.RunEvent(new KillerTeamCallback(PelicanTeam)).RemainingOtherTeam && devouredPlayers.Count < totalAlive - 1) return;

            if (MyPlayer.IsAlive && PelicanTeamWin != null && (totalAlive <= 1 || devouredPlayers.Count >= totalAlive - 1))
                NebulaAPI.CurrentGame?.TriggerGameEnd(PelicanTeamWin, GameEndReason.Situation, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
        }

        public override void OnActivated() 
        {
            if (AmOwner)
            {
                var devourTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeLocalKillablePredicate(p), MyRole.UnityColor, false, false);

                var devourButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false)
                    .BindKey(VirtualKeyInput.Kill).SetImage(buttonImage!).SetLabel("pelican.devour").SetColorLabel(MyRole.RoleColor);

                devourButton.Visibility = _ => MyPlayer.IsAlive;
                devourButton.Availability = _ => devourTracker.CurrentTarget is not null && MyPlayer.CanMove;
                devourButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, DevourCooldown.Cooldown).SetAsKillCoolTimer().Start();

                devourButton.OnClick = button =>
                {
                    var target = devourTracker.CurrentTarget;
                    if (target == null) return;

                    if (target is IFakePlayer fake)
                        MyPlayer.MurderPlayer(fake, Digestion, EventDetails.Kill, KillParameter.RemoteKill);
                    else if (target is GamePlayer p)
                    {
                        if (p.TryGetAbility<Bait.Ability>(out var a) || p.Modifiers.Any(m => m.Modifier.InternalName.Contains("bait")))
                        {
                            var cancelable = NebulaAPI.RunEvent(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target));
                            if (!(cancelable?.IsCanceled ?? false))
                                MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);

                            if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                            return;
                        }

                        p.VanillaPlayer.NetTransform.RpcSnapTo(new VVector2(-1000f, 10f));
                        RpcUpdateStatus.Invoke((MyPlayer, p, 1));
                        RPCSetCam.Invoke((p, MyPlayer));
                        button.CoolDownTimer = NebulaAPI.Modules.Timer(this, GetCurrentCooldown()).SetAsKillCoolTimer().Start();
                    }
                    button.StartCoolDown();
                };

                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(devourButton.GetKillButtonLike());

            }

            GameOperatorManager.Instance?.Subscribe<BombExplodeEvent>(ev =>
            {
                if (devouredPlayers.Contains(ev.Player.PlayerId))
                    ev.Recycle(MyPlayer);
            }, this);
            GameOperatorManager.Instance?.Subscribe<PlayerUpdateVisibilityEvent>(ev =>
            {
                if (devouredPlayers.Contains(ev.Player.PlayerId))
                    ev.SetInvisible();
            }, this);
        }

        protected override void OnReleased() => KillDevouredPlayer();
        float GetCurrentCooldown() => Mathn.Max(DevourCooldown.Cooldown - devouredPlayers.Count * ReduceTime, 0f);

        public void KillDevouredPlayer()
        {
            foreach (var id in devouredPlayers)
            {
                var player = GamePlayer.GetPlayer(id);
                if (player == null) continue;

                MyPlayer.MurderPlayer(player, Digestion, Digestion, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE, KillCondition.BothAlive);
                player.VanillaPlayer.NetTransform.RpcSnapTo(MyPlayer.Position);
                RPCSetCam.Invoke((player, null));
            }
            RpcUpdateStatus.Invoke((MyPlayer, MyPlayer, 0));
        }

        void OnMeetingPreStart(MeetingPreStartEvent ev) => KillDevouredPlayer();

        void OnPlayerDieOrDisconnect(PlayerDieOrDisconnectEvent ev)
        {
            if (devouredPlayers.Contains(ev.Player.PlayerId))
            {
                RpcUpdateStatus.Invoke((MyPlayer, ev.Player, 2));
                ev.Player.VanillaPlayer.NetTransform.RpcSnapTo(MyPlayer.Position);
                RPCSetCam.Invoke((ev.Player, null));
            }
            if (ev.Player == MyPlayer)
            {
                foreach (var id in devouredPlayers)
                    if (GamePlayer.GetPlayer(id) is GamePlayer p)
                    {
                        p.VanillaPlayer.NetTransform.RpcSnapTo(MyPlayer.Position);
                        RPCSetCam.Invoke((p, null));
                    }
                RpcUpdateStatus.Invoke((MyPlayer, MyPlayer, 0));
            }
        }

        public readonly static RemoteProcess<(GamePlayer player, GamePlayer? pelican)> RPCSetCam = new("PelicanSetCamTarget", (message, _) =>
        {
            if (message.player.AmOwner) AmongUsUtil.SetCamTarget(message.pelican?.VanillaPlayer);
        });

        static private readonly RemoteProcess<(GamePlayer pelican, GamePlayer target, int parameter)> RpcUpdateStatus = new("UpdatePelican", (message, _) => {
            if (message.pelican.Role is not Instance pelican) return;
            switch (message.parameter)
            {
                case 0: pelican.devouredPlayers.Clear(); break;
                case 1: pelican.devouredPlayers.Add(message.target.PlayerId); break;
                case 2: pelican.devouredPlayers.Remove(message.target.PlayerId); break;
            }
        });
    }
}