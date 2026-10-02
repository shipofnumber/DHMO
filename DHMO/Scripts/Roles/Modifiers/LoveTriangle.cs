using Virial.Runtime;

namespace DHMO.Roles.Modifiers;

public class LoveTriangle : DefinedModifierTemplate, DefinedModifier, HasCitation, ICodeName, IAssignableDocument
{
    public LoveTriangle() : base("loveTriangle", Lover.MyRole.Color, [InvolveCooldownOption, InvolveInactiveTurnsOption, SealUntilSecondTurnOption, TriangleMeetingChatOption], true, () => DGameManager.TriangleModeOption && !Lover.AvengerModeOption)
    {
        Lover.MyRole.ConfigurationHolder?.AppendRelatedHolders(ConfigurationHolder!);
    }
    
    public static FloatConfiguration InvolveCooldownOption = NebulaAPI.Configurations.Configuration("options.loveTriangle.involveCooldown", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    public static FloatConfiguration InvolveInactiveTurnsOption = NebulaAPI.Configurations.Configuration("options.loveTriangle.involveInactiveTurns", (2f, 10f, 1f), 5f, FloatConfigurationDecorator.TaskPhase);
    public static BoolConfiguration SealUntilSecondTurnOption = NebulaAPI.Configurations.Configuration("options.loveTriangle.sealUntilSecondTurn", true);
    public static BoolConfiguration TriangleMeetingChatOption = NebulaAPI.Configurations.Configuration("options.loveTriangle.meetingChat", false, () => NebulaAPI.GetAddon("Plan17ResourcesPlana") != null);

    bool IAssignableDocument.HasAbility => false;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;
    
    public bool ShowOnFreeplayScreen => false;
    public string CodeName => "LVT";
    public Citation Citation => Citations.NebulaOnTheShip;
    public Image? IconImage => Trilemma.MyRole.GetRoleIcon();

    static public LoveTriangle MyRole = new();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));
    
    public class Instance: RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        
        public Cache<Tuple<GamePlayer?, GamePlayer?>> MyLovers { get; }
        public int LoveTriangleId { get; }
        public VColor LoveTriangleColor => Lover.Colors[(NebulaGameManager.Instance?.CanSeeAllInfo ?? false) ? LoveTriangleId : 0];

        public Instance(GamePlayer player, int loversId) : base(player)
        {
            this.LoveTriangleId = loversId;

            MyLovers = new(() =>
            {
                var lover1 = GamePlayer.AllPlayers.FirstOrDefault(p =>
                    p.PlayerId != MyPlayer.PlayerId && p.Modifiers.Any(m =>
                        m is LoveTriangle.Instance lover && lover.LoveTriangleId == loversId));
                var lover2 = GamePlayer.AllPlayers.FirstOrDefault(p =>
                    p.PlayerId != MyPlayer.PlayerId && p.PlayerId != lover1?.PlayerId && p.Modifiers.Any(m =>
                        m is LoveTriangle.Instance lover && lover.LoveTriangleId == loversId));
                
                return Tuple.Create(lover1, lover2);
            });
        }
        
        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
        {
            if (ev.SetWinIf(ev.GameEnd == AddonGameTags.LoveTriangleEnd && !MyPlayer.IsDead)) ev.Recorder.AddReason(ev.Player.PlayerId, AddonGameTags.LoveTriangleWinDetail);
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
        {
            if (ev.SetBlockedIf(ev.GameEnd != AddonGameTags.LoveTriangleEnd)) ev.Recorder.AddReason(ev.Player.PlayerId, AddonGameTags.LoveTriangleBlockedDetail);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (MyPlayer.AmOwner && TriangleMeetingChatOption)
            {
                PrivateChat.RegisterChannel(LoveTriangleColor, LoveTriangleColor, $"LoveTriangle{LoveTriangleId}", 
                    () => Language.Translate("chat.loveTriangleText").Color(LoveTriangleColor), 
                    this, () => MeetingHud.Instance.AsBoolFast(), (p1, p2) => IsMyLover(p2));
            }
        }
        
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            bool canSee = AmOwner || canSeeAllInfo || CheckLovers(p => p.AmOwner, true);

            if (canSee) name += "♣".Color(LoveTriangleColor);
        }
        
        [OnlyMyPlayer, OnlyHost]
        void OnMurdered(PlayerDieOrDisconnectEvent ev)
        {
            if (CheckLovers(p => p.IsDead)) return;

            switch (ev)
            {
                case PlayerMurderedEvent:
                    Do(p => p.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill));
                    break;
                case PlayerExiledEvent:
                {
                    Do(p => p.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide));
                    if (Helpers.CurrentMonth == 12) NebulaAchievementManager.RpcClearAchievement.Invoke(("christmas", MyPlayer));
                    break;
                }
                default:
                    Do(p => p.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.WithAssigningGhostRole));
                    break;
            }
        }

        public bool CheckLovers(Predicate<GamePlayer> predicate, bool isOr = false)
        {
            var (p1, p2) = MyLovers.Get();

            if (isOr)
            {
                bool r1 = p1 != null && predicate(p1);
                bool r2 = p2 != null && predicate(p2);
                return r1 || r2;
            }
            
            return p1 != null && p2 != null && predicate(p1) && predicate(p2);
        }

        public void Do(Action<GamePlayer> action)
        {
            var myLovers = MyLovers.Get();
            if (myLovers.Item1 != null) action(myLovers.Item1);
            if (myLovers.Item2 != null) action(myLovers.Item2);
        }
        
        public bool IsMyLover(GamePlayer player)
        {
            var lovers = MyLovers.Get();

            return lovers.Item1 == player || lovers.Item2 == player;
        }
        
        [OnlyMyPlayer]
        void OnCheckCanKill(PlayerCheckCanKillLocalEvent ev)
        {
            if (IsMyLover(ev.Target)) ev.SetAsCannotKillBasically();
        }
        
        void CheckSheriffKill(SheriffCheckKillEvent ev)
        {
            if (Sheriff.CanKillLoversOption && ev.Target == MyPlayer)
            {
                ev.CanKill = true;
            }
        }
        
        [OnlyMyPlayer]
        void EditGuessable(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (IsMyLover(ev.Guesser)) ev.CanGuess = false;
        }
        
        bool RuntimeAssignable.InvalidateCrewmateTask => true;
        bool RuntimeAssignable.MyCrewmateTaskIsIgnored => true;
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class TriangleCriteria : AbstractModule<IGameModeStandard>, IGameOperator
{
    static TriangleCriteria() => DIManager.Instance.RegisterModule(() => new TriangleCriteria().RegisterPermanently());

    [OnlyHost]
    void OnUpdate(GameUpdateEvent ev)
    {
        var alive = APICompat.AlivePlayers;
        
        int totalAlive = alive.Length;
        if (totalAlive > 5) return;

        foreach (var p in alive)
        {
            foreach (var lover in p.GetModifiers<LoveTriangle.Instance>())
            {
                if (lover.CheckLovers(player => player.IsDead)) continue;

                NebulaAPI.CurrentGame?.TriggerGameEnd(AddonGameTags.LoveTriangleEnd, GameEndReason.Situation);
                return;
            }
        }
    }
}