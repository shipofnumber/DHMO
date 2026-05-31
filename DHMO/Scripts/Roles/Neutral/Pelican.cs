namespace DHMO.Roles;

public class Pelican : DefinedRoleTemplate, HasCitation, DefinedRole, DefinedSingleAssignable, DefinedCategorizedAssignable, DefinedAssignable, IRoleID, ISpawnable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder, IAssignableDocument
{
    public static readonly Team PelicanTeam = new("teams.pelican", new(0, 153, 76), TeamRevealType.OnlyMe);
    private Pelican() : base("pelican", PelicanTeam.Color, RoleCategory.NeutralRole, PelicanTeam, [DevourCooldown, CanReduceCDOption, ReduceTime, VentConfiguration]) { }
    Citation? HasCitation.Citation => DHMOCitations.GGD;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public static readonly IRelativeCooldownConfiguration DevourCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.pelican.devourCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly BoolConfiguration CanReduceCDOption = NebulaAPI.Configurations.Configuration("options.role.pelican.canReduceCD", true);
    public static readonly IntegerConfiguration ReduceTime = NebulaAPI.Configurations.Configuration("options.role.pelican.reduceTime", (1, 10), 3, () => CanReduceCDOption);
    static private readonly IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.pelican.vent", true);

    private static Image? buttonImage = NebulaAPI.AddonAsset?.GetResource("DevourButton.png")?.AsImage(115f);
    public static TranslatableTag Digestion = new("state.digestion");
    public static readonly Pelican MyRole = new();

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonImage!, "role.pelican.ability.devour");
    }

    [NebulaRPCHolder]
    public class Instance(GamePlayer player) : RuntimeVentRoleTemplate(player, VentConfiguration), RuntimeRole
    {
        public static GameEnd? PelicanTeamWin = NebulaAPI.Preprocessor?.CreateEnd("pelican", MyRole.RoleColor);
        public override DefinedRole Role => MyRole;

        int[]? RuntimeAssignable.RoleArguments
        {
            get
            {
                int mask = 0;
                foreach (var p in devouredPlayers)
                {
                    mask |= 1 << p.PlayerId;
                }
                return [mask];
            }
        }

        static List<GamePlayer> devouredPlayers = [];

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
            GameOperatorManager.Instance?.Subscribe<PlayerDieEvent>(ev =>
            {
                if (!AmongUsUtil.InMeeting)
                    if (devouredPlayers.Contains(ev.Player))
                    {
                        RPCSetCam.Invoke((ev.Player, null, (MyPlayer.TruePosition.x, MyPlayer.TruePosition.y)));
                        devouredPlayers.Remove(ev.Player);
                    }
            }, this);
            GameOperatorManager.Instance?.Subscribe<PlayerUpdateVisibilityEvent>(ev =>
            {
                if (devouredPlayers.Contains(ev.Player))
                    ev.SetInvisible();
            }, this);

            if (AmOwner)
            {
                var devourTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p), MyRole.UnityColor, false, false);

                var devourButton = NebulaAPI.Modules.AbilityButton(this, false, true, 0, false).BindKey(VirtualKeyInput.Kill).SetImage(buttonImage!).SetLabel("pelican.devour").SetColorLabel(MyRole.RoleColor);
                devourButton.Visibility = _ => !MyPlayer.IsDead;
                devourButton.Availability = _ => devourTracker.CurrentTarget != null && MyPlayer.CanMove && !MyPlayer.WillDie;
                devourButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, DevourCooldown.Cooldown).SetAsKillCoolTimer().Start();
                devourButton.OnClick = _ =>
                {
                    var target = devourTracker.CurrentTarget;
                    if (target != null)
                    {
                        if (target is IFakePlayer fake)
                            MyPlayer.MurderPlayer(fake, Digestion, EventDetails.Kill, KillParameter.RemoteKill);
                        else if (target is GamePlayer player)
                        {
                            devouredPlayers.Add(player);
                            RPCSetCam.Invoke((player, MyPlayer, (10f, 10f)));
                            devourButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, GetCurrentCooldown()).SetAsKillCoolTimer();
                        }
                        devourButton.StartCoolDown();
                    }
                };

                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(devourButton.GetKillButtonLike());
            }
        }

        protected override void OnReleased()
        {
            base.OnReleased();
            for (int i = 0; i < devouredPlayers.Count; i++)
            {
                var player = devouredPlayers[i];
                MyPlayer.MurderPlayer(player, Digestion, EventDetails.Kill, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE, KillCondition.BothAlive);
                RPCSetCam.Invoke((player, null, (MyPlayer.TruePosition.x, MyPlayer.TruePosition.y)));
            }
            devouredPlayers = [];
        }

        static float GetCurrentCooldown()
        {
            var time = Mathf.Max(DevourCooldown.Cooldown - (devouredPlayers.Count * ReduceTime.GetValue()));
            if (time < 0f)
                return 0f;
            else return time;
        }

        void OnMeetingPreStart(MeetingPreStartEvent _)
        {
            for (int i = 0; i < devouredPlayers.Count; i++)
            {
                var player = devouredPlayers[i];
                MyPlayer.MurderPlayer(player, Digestion, EventDetails.Kill, KillParameter.WithAssigningGhostRole | KillParameter.WithoutSelfSE, KillCondition.BothAlive);
                RPCSetCam.Invoke((player, null, (MyPlayer.TruePosition.x, MyPlayer.TruePosition.y)));
            }
            devouredPlayers = [];
            
        }

        [OnlyMyPlayer]
        void OnDeadOrDisconnect(PlayerDieOrDisconnectEvent _)
        {
            for (int i = 0; i < devouredPlayers.Count; i++)
            {
                var player = devouredPlayers[i];
                RPCSetCam.Invoke((player, null, (MyPlayer.TruePosition.x, MyPlayer.TruePosition.y)));
            }
            devouredPlayers = [];
        }

        public readonly static RemoteProcess<(GamePlayer player, GamePlayer? pelican, (float x, float y) position)> RPCSetCam = new("PelicanSetCamTarget", (message, b) =>
        {
            if (message.player.AmOwner)
                AmongUsUtil.SetCamTarget(message.pelican?.VanillaPlayer);

            message.player.Logic.SnapTo(new Vector2(message.position.x, message.position.y));
        }, false);
    }
}