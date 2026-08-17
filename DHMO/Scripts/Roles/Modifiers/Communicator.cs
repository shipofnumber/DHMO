namespace DHMO.Roles.Modifiers;

public class Communicator : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, HasCitation
{
    private Communicator() : base("communicator", "COMR", new(151, 189, 216))
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    static public Communicator MyRole = new();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance(GamePlayer player) : RuntimeAssignableTemplate(player), RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += "⊙".Color(MyRole.Color);
        }
    }
}