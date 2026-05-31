namespace DHMO.Roles;

/*public class Equalizer : DefinedSingleAbilityRoleTemplate<Equalizer.Ability>, DefinedRole
{
    private Equalizer() : base("equalizer", new(22, 97, 171), RoleCategory.CrewmateRole, Crewmate.MyTeam) { }
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;

    static public readonly Equalizer MyRole = new();
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                
            }
        }
    }
}*/