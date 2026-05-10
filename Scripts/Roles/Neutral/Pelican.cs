using System.Net.Security;

namespace DHMO.Roles;

public class Pelican : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder
{
    public static readonly Team PelicanTeam = new("teams.pelican", new(0, 153, 76), TeamRevealType.OnlyMe);
    private Pelican() : base("pelican", PelicanTeam.Color, RoleCategory.NeutralRole, PelicanTeam, [DevourCooldown, CanReduceCDOption, ReduceTime,VentConfiguration]) { }
    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration DevourCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.pelican.devourCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly BoolConfiguration CanReduceCDOption = NebulaAPI.Configurations.Configuration("options.role.pelican.canReduceCD", true);
    public static readonly IntegerConfiguration ReduceTime = NebulaAPI.Configurations.Configuration("options.role.pelican.reduceTime", (1, 10), 3, () => CanReduceCDOption);
    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.pelican.vent", true);

    private static Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("DevourButton.png")?.AsImage(115f);
    public static TranslatableTag Digestion = new("state.digestion");
    public static readonly Pelican MyRole = new();

    public class Instance(GamePlayer player) : RuntimeVentRoleTemplate(player, VentConfiguration), RuntimeRole
    {
        public static GameEnd? PelicanTeamWin = NebulaAPI.Preprocessor?.CreateEnd("pelican", MyRole.RoleColor);
        public override DefinedRole Role => MyRole;

        int[]? RuntimeAssignable.RoleArguments
        {
            get
            {
                int mask = 0;
                foreach (var p in devouredPlayer)
                {
                    mask |= 1 << p.PlayerId;
                }
                return [mask];
            }
        }

        static List<GamePlayer> devouredPlayer = [];
        ModAbilityButton? DevourButton;

        void BlockTriggerEnd(EndCriteriaPreMetEvent ev)
        {
            if (ev.GameEnd != NebulaGameEnd.LoversWin && ev.GameEnd != PelicanTeamWin && !MyPlayer.IsDead && ev.EndReason == GameEndReason.Situation)
                ev.Reject();
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            var totalAlive = AddonHelper.GetAlivePlayers().alivePlayers.Where(p => !p.WillDie).Count();
            ev.SetWinIf(ev.GameEnd == PelicanTeamWin && !MyPlayer.IsDead && totalAlive <= 1);
        }

        [OnlyHost]
        void WinCheck(GameUpdateEvent _)
        {
            try
            {
                var totalAlive = AddonHelper.GetAlivePlayers().alivePlayers.Where(p => !p.WillDie).Count();
                if (!MyPlayer.IsDead && totalAlive <= 1)
                    NebulaAPI.CurrentGame?.TriggerGameEnd(PelicanTeamWin!, GameEndReason.Situation, BitMasks.AsPlayer().Add(MyPlayer));
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var devourTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p), MyRole.UnityColor, false, false);

                DevourButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false)
                    .BindKey(VirtualKeyInput.Kill)
                    .SetImage(buttonImage!)
                    .SetLabel("pelican.devour")
                    .SetColorLabel(MyRole.RoleColor);
                DevourButton.Visibility = _ => !MyPlayer.IsDead;
                DevourButton.Availability = _ => devourTracker.CurrentTarget != null && MyPlayer.CanMove && !MyPlayer.WillDie;
                DevourButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, DevourCooldown.Cooldown).SetAsKillCoolTimer().Start();
                DevourButton.OnClick = _ =>
                {
                    var target = devourTracker.CurrentTarget;
                    if (target != null)
                    {
                        if (target is IFakePlayer fake)
                            MyPlayer.MurderPlayer(fake, Digestion, EventDetails.Kill, KillParameter.RemoteKill);
                        else
                        {
                            devouredPlayer.Add(target.RealPlayer);
                            NebulaAPI.RunEvent(new PelicanDevourEvent(MyPlayer, target.RealPlayer));
                        }
                        DevourButton.StartCoolDown();
                    }
                };

                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(DevourButton.GetKillButtonLike());
            }
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            var list = devouredPlayer;
            devouredPlayer = [];
            if (list.Count > 0)
            {
                foreach (var player in list)
                {
                    if (player != null)
                    {
                        MyPlayer.MurderPlayer(player, Digestion, EventDetails.Kill, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE, KillCondition.BothAlive);
                    }
                }
            }
        }

        float GetCurrentCooldown()
        {
            var time = Mathf.Max(DevourCooldown.Cooldown - (devouredPlayer.Count * ReduceTime.GetValue()));
            if (time < 0f)
                return 0f;
            else return time;
        }

        void OnDevour(PelicanDevourEvent ev)
        {
            ev.Devoured.Logic.SnapTo(new UnityEngine.Vector2(-100f, 10f));
            if (GamePlayer.LocalPlayer is not null)
            if (ev.Devoured == GamePlayer.LocalPlayer)
                AmongUsUtil.SetCamTarget(MyPlayer.VanillaPlayer);
            ev.Devoured.Unbox().WillDie = true;
            DevourButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, GetCurrentCooldown()).SetAsKillCoolTimer();
        }

        void OnMeetingPreStart(MeetingPreStartEvent ev)
        {
            var list = devouredPlayer;
            DevourButton?.CoolDownTimer = NebulaAPI.Modules.Timer(this, GetCurrentCooldown()).SetAsKillCoolTimer();
            if (list.Count >0)
            {
                foreach (var player in list)
                {
                    if (player != null)
                    {
                        MyPlayer.MurderPlayer(player, Digestion, EventDetails.Kill, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE, KillCondition.BothAlive);
                    }
                }
            }
            devouredPlayer = [];
        }

        void OnUpdataVisible(PlayerUpdateVisibilityEvent ev)
        {
            if (devouredPlayer.Contains(ev.Player))
                ev.SetInvisible();
        }

        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            var deadPlayer = ev.Player;
            if (deadPlayer.PlayerId == MyPlayer.PlayerId)
            {
                if (deadPlayer == GamePlayer.LocalPlayer)
                    AmongUsUtil.SetCamTarget();

                foreach (var player in devouredPlayer)
                {
                    player?.Logic.SnapTo(ev.Player.TruePosition);
                    player?.Unbox().WillDie = false;
                }

                devouredPlayer = [];
            }
            else if (devouredPlayer.Contains(deadPlayer))
            {
                if (deadPlayer.IsDisconnected) devouredPlayer.Remove(deadPlayer);
                else
                {
                    if (deadPlayer == GamePlayer.LocalPlayer)
                        AmongUsUtil.SetCamTarget();

                    if (deadPlayer is not null)
                    {
                        deadPlayer.Unbox().WillDie = false;
                        deadPlayer.Logic.SnapTo(MyPlayer.TruePosition);
                        devouredPlayer.Remove(deadPlayer);
                    }
                }
            }
        }
    }
}