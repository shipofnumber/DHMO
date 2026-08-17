namespace DHMO.Roles.Impostor;

/*public class Illusionist : DefinedSingleAbilityRoleTemplate<Illusionist.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Illusionist() : base("illusionist", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [IllusionCooldownOption, NumOfIllusionOption, NumOfDummyOption, NumOfNeedKillDummyOption])
    {
    }

    static private readonly IntegerConfiguration IllusionCooldownOption = NebulaAPI.Configurations.Configuration("options.role.illusionist.illusionCooldown", (10, 60), 25);
    static private readonly IntegerConfiguration NumOfIllusionOption = NebulaAPI.Configurations.Configuration("options.role.illusionist.numOfillusion", (1, 5), 1);
    static internal readonly IntegerConfiguration NumOfDummyOption = NebulaAPI.Configurations.Configuration("options.role.illusionist.numOfdummy", (1, 5), 3);
    static internal readonly IntegerConfiguration NumOfNeedKillDummyOption = NebulaAPI.Configurations.Configuration("options.role.illusionist.numOfkilledDummy", (1, 5), 3);
    internal static readonly FloatConfiguration IllusionTimeDuration = NebulaAPI.Configurations.Configuration("options.role.illusionist.IllusionTimeDuration", (0f, 60f, 2.5f), 40f, FloatConfigurationDecorator.Second);

    Citation? HasCitation.Citation => DHMOCitations.DHMO;

    static public Illusionist MyRole = new();

    bool IAssignableDocument.HasTips => true;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfIllusionOption));

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        int leftUses = NumOfIllusionOption; 
        EditableBitMask<GamePlayer>? deadBodysMask = BitMasks.AsPlayer();

        public Ability(GamePlayer player, bool isUsurped, int uses) : base(player, isUsurped)
        {
            this.leftUses = uses;

            if (AmOwner)
            {
                ObjectTracker<Virial.Game.DeadBody> tracker = ObjectTrackers.ForDeadBody(this, null, MyPlayer, (d) => true);

                var illusionButton = NebulaAPI.Modules.AbilityButton(this).BindKey(VirtualKeyInput.Ability).SetLabel("illusionist.illusion");
                illusionButton.Visibility = button => MyPlayer.IsAlive && leftUses > 0;
                illusionButton.Availability = button => MyPlayer.CanMove && tracker.CurrentTarget != null;
                illusionButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, IllusionCooldownOption).SetAsAbilityTimer().Start();
                illusionButton.ShowUsesIcon(0, leftUses.ToString());
                illusionButton.OnClick = button =>
                {
                    if (tracker.CurrentTarget != null)
                    {
                        deadBodysMask.Add(tracker.CurrentTarget.Player);
                        leftUses--;
                        illusionButton.UpdateUsesIcon(leftUses.ToString());
                    }
                };
            }
        }

        [Local]
        void OnReportDeadBody(ReportDeadBodyEvent ev)
        {
            if (ev.Reporter.PlayerId == MyPlayer.PlayerId || ev.Reported == null) return;
            if (deadBodysMask?.Test(ev.Reported) ?? false)
            {
                var degenerate = new Degenerate(MyPlayer, ev.Player);
                degenerate.RegisterPermanently();
                deadBodysMask.Remove(ev.Reported);
            }
        }
    }
}

public class Degenerate : FlexibleLifespan, IGameOperator, IBindPlayer, IPlayerAbility
{
    private GamePlayer myPlayer;
    private GamePlayer illusionist;

    private List<IFakePlayer> dummys = [];
    GamePlayer IBindPlayer.MyPlayer => myPlayer;

    float timer = Illusionist.IllusionTimeDuration;
    int killCount = 0;

    public static readonly TranslatableTag madness = new("state.madness");

    bool IPlayerAbility.CanReport => false;

    public Degenerate(GamePlayer illusionist, GamePlayer player)
    {
        this.myPlayer = player;
        this.illusionist = illusionist;
        
        if (player.AmOwner)
        {
            string prefix = Language.Translate("roles.illusionist.leftTime");
            Helpers.TextHudContent("DegenerateText", this, (tmPro) => tmPro.text = $"{prefix}: {timer}s");
        }
    }

    public void SummonDummy(int count)
    {
        var locations = NebulaPreSpawnLocation.PreLocations.Select(c => c.Position!.Value).ToArray();

        for (int i = 0; i < count; i++)
        {
            var postion = locations[System.Random.Shared.Next(NebulaPreSpawnLocation.PreLocations.Length)];
            var petPostion = new VVector2(postion.x - 0.1f, postion.y);
            GamePlayer player = GamePlayer.AllOrderedPlayers[System.Random.Shared.Next(GamePlayer.AllOrderedPlayers.Count)];

            var fakePlayer = FakePlayerController.SpawnSyncFakePlayer(player, new(postion, KillCharacteristics.Disappear, true, true, player.VanillaCosmetics.FlipX, petPostion)).BindLifespan(this);
            dummys.Add(fakePlayer);
            NebulaManager.Instance.StartCoroutine(CoDummyWalk(fakePlayer).WrapToIl2Cpp());
        }
    }

    internal void DespawnDummy(IFakePlayer fakePlayer)
    {
        ManagedEffects.CoDisappearEffect(LayerExpansion.GetPlayersLayer(), null, fakePlayer.Position.ToUnityVector().AsVector3(-1f), 1f).StartOnScene();
        dummys.Remove(fakePlayer);
        fakePlayer.Release();
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        if (AmongUsUtil.InMeeting) return;
        timer -= ev.DeltaTime;

        foreach (var fp in dummys)
        {
            if (fp.IsDead) continue;
            float distance = myPlayer.Position.Distance(fp.Position);
            if (NebulaAPI.CurrentGame?.CurrentMap?.AnyShadowsBetween(myPlayer.Position, fp.Position) ?? false) continue;

            if (distance < 0.8f) DespawnDummy(fp);
        }

        if (killCount >= Illusionist.NumOfNeedKillDummyOption) this.Release();
        else if (timer <= 0f)
        {
            illusionist.MurderPlayer(myPlayer, madness, null, Virial.Game.KillParameter.RemoteKill);
        }
    }

    void OnMeetingPreStart(MeetingPreStartEvent ev)
    {
        dummys.Do(p => DespawnDummy(p));
    }

    void OnTaskPhaseReStart(TaskPhaseRestartEvent ev)
    {
        SummonDummy(Illusionist.NumOfNeedKillDummyOption - killCount);
    }

    [OnlyMyPlayer]
    void OnPlayerDeadOrDisconnect(PlayerDieOrDisconnectEvent ev)
    {
        this.Release();
    }

    void IGameOperator.OnReleased() => dummys.Clear();

    private static IEnumerator CoDummyWalk(IFakePlayer fakePlayer)
    {
        var locations = NebulaPreSpawnLocation.PreLocations.Select(c => c.Position!.Value).ToArray();

        while (!fakePlayer.IsDead && fakePlayer.IsActive)
        {
            var postion = locations[System.Random.Shared.Next(NebulaPreSpawnLocation.PreLocations.Length)];

            var path = NavVerticesHelpers.CalcPath(fakePlayer.TruePosition, postion);
            if (path == null) yield break;

            yield return NavVerticesHelpers.WalkPath(path.Path, path.StopCond, fakePlayer.Logic);
            yield return ManagedEffects.Wait(1f);
        }
    }
}*/