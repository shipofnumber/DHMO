namespace DHMO.Roles.Impostor;

/*public class Illusionist : DefinedSingleAbilityRoleTemplate<Illusionist.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Illusionist() : base("illusionist", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [])
    {
    }
    Citation? HasCitation.Citation => DHMOCitations.DHMO;

    static public Illusionist MyRole = new();

    bool IAssignableDocument.HasTips => true;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
            }
        }
    }
}*/