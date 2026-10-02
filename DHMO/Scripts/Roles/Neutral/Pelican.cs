namespace DHMO.Roles.Neutral;

public class Pelican : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    private Pelican() : base("pelican", AddonGameTags.PelicanTeam.Color, RoleCategory.NeutralRole, AddonGameTags.PelicanTeam, [DevourCooldown, IncreaseTime, CanCallEmergencyMeeting, new GroupConfiguration("options.role.pelican.group.pelicanHunt", [InvokePelicanHunt, PelicanHuntAliveNum, PelicanHuntDuration, TaskPhaseRestartPelicanHuntDisperse], AddonGameTags.PelicanTeam.Color.RGBMultiplied(0.65f)), VentConfiguration])
    {
    }

    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration DevourCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.pelican.devourCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly FloatConfiguration IncreaseTime = NebulaAPI.Configurations.Configuration("options.role.pelican.additionalCooldown", (0f, 20f, 1f), 5f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration InvokePelicanHunt = NebulaAPI.Configurations.Configuration("options.role.pelican.pelicanHunt", true);
    internal static readonly BoolConfiguration CanCallEmergencyMeeting = NebulaAPI.Configurations.Configuration("options.role.pelican.canCallEmergencyMeeting", true);
    internal static readonly IntegerConfiguration PelicanHuntAliveNum = NebulaAPI.Configurations.Configuration("options.role.pelican.pelicanHuntAlive", (2, 23), 4, () => InvokePelicanHunt);
    internal static readonly FloatConfiguration PelicanHuntDuration = NebulaAPI.Configurations.Configuration("options.role.pelican.pelicanHuntDuration", (0f, 300f, 2.5f), 40f, FloatConfigurationDecorator.Second, () => InvokePelicanHunt);
    internal static readonly BoolConfiguration TaskPhaseRestartPelicanHuntDisperse = NebulaAPI.Configurations.Configuration("options.role.pelican.taskPhaseRestartEnterPelicanHuntDisperse", true, () => InvokePelicanHunt);
    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.pelican.vent", true);

    private static readonly Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/DevourButton.png")?.AsImage(115f);

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
        yield return new("%WIN%", InvokePelicanHunt ? Language.Translate("role.pelican.winCond.pelicanHuntOver") : "");
        yield return new("%PELICANHUNT%", InvokePelicanHunt ? Language.Translate("role.pelican.ability.pelicanHunt") : "");
        yield return new("%NUM%", PelicanHuntAliveNum.GetValue().ToString());
        yield return new("%DURATION%", PelicanHuntDuration.GetValue().ToString());
    }

    [NebulaRPCHolder]
    public class Instance(GamePlayer player) : RuntimeVentRoleTemplate(player, VentConfiguration), RuntimeRole
    {
        public EditableBitMask<GamePlayer> DevouredPlayerMask { get; } = BitMasks.AsPlayer();
        
        public override DefinedRole Role => MyRole;
        bool RuntimeAssignable.CanCallEmergencyMeeting => CanCallEmergencyMeeting;

        void ClaimPelicanTeamRemaining(KillerTeamCallback callback)
        {
            if (callback.ExcludedTeam == AddonGameTags.PelicanTeam) return;
            if (MyPlayer.IsAlive) callback.MarkRemaining();
        }

        [OnlyHost]
        void CheckWin(GameUpdateEvent ev)
        {
            var alivePlayers = APICompat.AlivePlayers;
            int totalAlive = alivePlayers.Length;
            int totalDevoured = alivePlayers.Select(p =>
            {
                p.TryGetRole<Pelican.Instance>(out var pelican);
                return pelican;
            }).NotNull().Sum(p => DevouredPlayerMask.Count);

            if (NebulaAPI.RunEvent(new KillerTeamCallback(AddonGameTags.PelicanTeam)).RemainingOtherTeam || (totalAlive - totalDevoured) > 1) return;

            if (MyPlayer.IsAlive)
                NebulaAPI.CurrentGame?.TriggerGameEnd(AddonGameTags.PelicanEnd, GameEndReason.Situation, BitMasks.AsPlayer(MyPlayer));
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

                    if (target is IFakePlayer || target.RealPlayer.TryGetAbility<Bait.Ability>(out var a) ||
                        target.RealPlayer.Modifiers.Any(m => m.Modifier.InternalName.Contains("bait")))
                    {
                        var cancelable =
                            NebulaAPI.RunEvent(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target));
                        if (!(cancelable?.IsCanceled ?? false))
                            MyPlayer.MurderPlayer(target, AddonGameTags.Digestion, null, KillParameter.NormalKill);

                        if (cancelable?.ResetCooldown ?? false) devourButton.CoolDownTimer.Start(GetCurrentCooldown());
                        return;
                    }

                    RpcDevoured.Invoke((MyPlayer, target.RealPlayer));

                    devourButton.CoolDownTimer.Start(GetCurrentCooldown());
                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                };
            }
            
            GameOperatorManager.Instance?.Subscribe<UpdateSpectatorEvent>(ev =>
            {
                ev.CanMonitorAlives &= !DevouredPlayerMask.Test(GamePlayer.LocalPlayer);
            }, this);
        }
        private float GetCurrentCooldown() => Mathn.Min(DevourCooldown.Cooldown + DevouredPlayerMask.Count * IncreaseTime, 60f);

        [OnlyHost]
        void OnDevoured(PelicanDevourEvent ev)
        {
            if (ev.Player.TryGetAbility<Nebula.Roles.Crewmate.Provocateur.Ability>(out var ability) && ability.EffectIsActive)
            {
                ev.Cancelled = true;
                ev.Player.MurderPlayer(MyPlayer, PlayerState.Embroiled, EventDetail.Embroil, KillParameter.RemoteKill, KillCondition.TargetAlive);
            }
        }
        
        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            GamePlayer? localPlayer = GamePlayer.LocalPlayer;
            if (localPlayer == null) return;
            
            if (DevouredPlayerMask.Test(localPlayer))
            {
                MyPlayer.MurderPlayer(localPlayer, AddonGameTags.Digestion, null, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE,
                    KillCondition.BothAlive);

                localPlayer.VanillaPlayer.NetTransform.RpcSnapTo(MyPlayer.Position);
                AmongUsUtil.SetCamTarget(null);
            }

            DevouredPlayerMask.Clear();
        }

        void OnPlayerDieOrDisconnected(PlayerDieOrDisconnectEvent ev)
        {
            if (MyPlayer.PlayerId == ev.Player.PlayerId)
            {
                GamePlayer? localPlayer = GamePlayer.LocalPlayer;
                if (localPlayer == null) return;

                if (DevouredPlayerMask.Test(localPlayer))
                {
                    localPlayer.VanillaPlayer.NetTransform.RpcSnapTo(MyPlayer.Position);
                    AmongUsUtil.SetCamTarget(null);
                }
                
                DLog.Log(DevouredPlayerMask.AsRawPattern);
                DevouredPlayerMask.Clear();
            }
            else if (DevouredPlayerMask.Test(ev.Player))
            {
                ev.Player.Logic.SnapTo(MyPlayer.Position);
                if (ev.Player.AmOwner) AmongUsUtil.SetCamTarget(null);
                
                DevouredPlayerMask.Remove(ev.Player);
            }
        }

        void OnUpdateVisibility(PlayerUpdateVisibilityEvent ev)
        {
            if (DevouredPlayerMask.Test(ev.Player) && ev.Player.IsAlive)
            {
                ev.SetInvisible();
            }
        }

        private static readonly RemoteProcess<(GamePlayer pelican, GamePlayer devoured)> RpcDevoured = new("PelicanDevour", (message, _) =>
        {
            if (message.pelican.Role is Pelican.Instance pelican)
            {
                var ev = NebulaAPI.RunEvent<PelicanDevourEvent>(new PelicanDevourEvent(message.devoured, message.pelican));
                if (ev.Cancelled) return;
                
                if (message.devoured.AmOwner) AmongUsUtil.SetCamTarget(pelican.MyPlayer.VanillaPlayer);
                
                message.devoured.Logic.SnapTo(new VVector2(-100f, 10f));
                pelican.DevouredPlayerMask.Add(message.devoured);
            }
        });
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class PelicanCriteria : AbstractModule<IGameModeStandard>, IGameOperator
{
    static PelicanCriteria() => DIManager.Instance.RegisterModule(() => new PelicanCriteria().RegisterPermanently());

    [OnlyHost]
    void OnPelicanHuntEnd(TimeHuntEndEvent ev)
    {
        if (ev is { IsTimeOver: true, TimeHunt.Id: "pelican" })
        {
            EditableBitMask<GamePlayer> playerMask = BitMasks.AsPlayer().AddAll(GamePlayer.AllPlayers.Where(p => p.IsAlive && p.Role is Pelican.Instance));
            NebulaAPI.CurrentGame?.RequestGameEnd(AddonGameTags.PelicanEnd, playerMask, AddonGameTags.PelicanHuntOverWinDetail);
        }
    }
}