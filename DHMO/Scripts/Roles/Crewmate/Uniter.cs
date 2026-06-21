namespace DHMO.Roles.Crewmate;

public class Uniter : DefinedSingleAbilityRoleTemplate<Uniter.Ability>, DefinedSingleAssignable, DefinedAssignable, DefinedRole, IAssignableDocument, HasCitation
{
    private Uniter() : base("uniter", new(64, 226, 193), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [NumOfUnitingOption, NumOfCanUnitOption, MaxLeftVotingTimeForUniting])
    {
    }

    static private readonly IntegerConfiguration NumOfUnitingOption = NebulaAPI.Configurations.Configuration("options.role.uniter.numOfuniting", (1, 15), 3);
    static private readonly IntegerConfiguration NumOfCanUnitOption = NebulaAPI.Configurations.Configuration("options.role.uniter.numOfcanUnit", (1, 5), 2);
    static private readonly FloatConfiguration MaxLeftVotingTimeForUniting = NebulaAPI.Configurations.Configuration("options.role.uniter.maxLeftVotingTimeForUniting", (0f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfUnitingOption));
    Citation? HasCitation.Citation => DHMOCitations.DHMO;
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/UniterIcon.png")?.AsImage(120f);

    public static readonly Uniter MyRole = new();

    bool IAssignableDocument.HasTips => true;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IBindPlayer, IGameOperator, IGameComponent, ILifespan
    {
        Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/UniterMeetingButton.png")?.AsImage(120f);
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        int leftUniting = NumOfUnitingOption;
        public Ability(GamePlayer player, bool isUsurped, int leftUses) : base(player, isUsurped)
        {
            leftUniting = leftUses;

            if (AmOwner)
            {
                string prefix = Language.Translate("roles.uniter.leftUniting");
                Helpers.TextHudContent("UniterText", this, (tmPro) => tmPro.text = prefix + ": " + leftUniting, true);
            }
        }

        List<byte> selected = [];

        void OnMeetingStart(MeetingStartEvent _)
        {
            if (leftUniting <= 0) return;
            selected.Clear();
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(buttonImage!, state =>
            {
                var p = state.MyPlayer;
                if (!state.IsSelected)
                {
                    if (selected.Count < NumOfCanUnitOption)
                    {
                        selected.Add(p.PlayerId);
                        state.SetSelect(true);
                    }
                }
                else
                {
                    if (selected.Contains(p.PlayerId))
                        selected.Remove(p.PlayerId);
                    state.SetSelect(false);
                }
            }, (p) => (!MeetingHud.Instance.GetPlayer(MyPlayer.PlayerId).DidVote || p.IsSelected) && MeetingHudExtension.VotingTimer > MaxLeftVotingTimeForUniting && leftUniting > 0 && p.MyPlayer.IsAlive && !p.MyPlayer.AmOwner && MyPlayer.IsAlive));
        }

        [OnlyMyPlayer]
        void OnVote(PlayerVoteCastLocalEvent _) { if (selected.Count > 0) --leftUniting; }

        void FixVote(PlayerFixVoteHostEvent ev)
        {
            if (MyPlayer.IsDead) return;
            var uniterArea = MeetingHud.Instance.GetPlayer(MyPlayer.PlayerId);

            if (selected.Contains(ev.Player.PlayerId))
            {
                if (uniterArea?.DidVote ?? false && ev.DidVote)
                    ev.VoteTo = NebulaGameManager.Instance?.GetPlayer(uniterArea.VotedFor);
            }
        }
    }
}