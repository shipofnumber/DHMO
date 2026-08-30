using Virial.Runtime;

namespace DHMO.Roles.Abilities;

public class RoleMarkAbility : FlexibleLifespan, IBindPlayer, IGameOperator
{
    public static RoleMarkAbility LocalMarkAbility { get; private set; } = null!;
    
    static readonly Image? MarkImage = NebulaAPI.AddonAsset?.GetResource("Button/MarkButton.png")?.AsImage();
    public static BoolConfiguration CanUseMark = NebulaAPI.Configurations.Configuration("options.meeting.canUseMark", true);

    public Dictionary<byte, HashSet<DefinedAssignable>> MarkRoleDic { get; private set; } = [];
    public MetaScreen? LastMarkWindow = null;
    private GamePlayer myPlayer;

    public GamePlayer MyPlayer => myPlayer;

    public RoleMarkAbility(ILifespan lifespan, GamePlayer player) : base(lifespan)
    {
        MarkRoleDic = [];
        myPlayer = player;

        foreach (var p in GamePlayer.AllPlayers) MarkRoleDic.Add(p.PlayerId, []);

        var assignables = Nebula.Roles.Roles.AllAssignables().Where(a => a is not DefinedGhostRole && a.ShowOnHelpScreen);

        var markButton = NebulaAPI.Modules.AbilityButton(this, isLeftSideButton: true, alwaysShow: true).SetImage(MarkImage!).SetLabel("mark");
        markButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting;
        markButton.Availability = _ => !Minigame.Instance.AsBoolFast() && AmongUsUtil.InMeeting && MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Animating && MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Results && MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Proceeding && !APICompat.IsOutMeeting();
        markButton.OnClick = _ =>
        {
            RoleMarkMenu.Open((p) =>
            {
                var set = MarkRoleDic[p.PlayerId];
                RoleMarkWindow.OpenRoleSelectWindowUsingTabs(p, assignables, GetAssignableTab(), string.Empty,
                    (a, renderer) =>
                    {
                        if (!set.Add(a))
                        {
                            set.Remove(a);
                            renderer.color = new(0.14f, 0.14f, 0.14f);
                        }
                        else
                            renderer.color = VColor.White.ToUnityColor();
                    });
            });
        };

        RoleMarkAbility.LocalMarkAbility = this;
    }

    public string GetModifierString(byte id)
    {
        if (MarkRoleDic == null) return string.Empty;
        var modifiers = MarkRoleDic[id].Where(a => a is DefinedModifier);
        if (modifiers.Any() && modifiers != null)
        {
            string text = $"{Language.Translate("role.category.modifier").Bold()}";
            foreach (var modifier in modifiers)
                text += $"<br>{modifier.GetRoleIconTag()}{modifier.DisplayColoredName.Bold()}";

            return text;
        }
        return string.Empty;
    }

    static (string?, Predicate<DefinedAssignable>?)[] GetAssignableTab()
    {
        static Predicate<DefinedAssignable> IsRoleOfCategory(RoleCategory category) => a => a is DefinedRole role && role.Category == category;

        return
        [
          (Language.Translate("role.category.impostor").Color(NebulaTeams.ImpostorTeam.Color).Bold(), IsRoleOfCategory(RoleCategory.ImpostorRole)),
          (Language.Translate("role.category.neutral").Color(new VColor(255, 178, 0)).Bold(), IsRoleOfCategory(RoleCategory.NeutralRole)),
          (Language.Translate("role.category.crewmate").Color(NebulaTeams.CrewmateTeam.Color).Bold(), IsRoleOfCategory(RoleCategory.CrewmateRole)),
          (Language.Translate("role.category.modifier").Bold(), a => a is DefinedModifier),
        ];
    }
}