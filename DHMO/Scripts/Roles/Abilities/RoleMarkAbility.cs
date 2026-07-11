namespace DHMO.Roles.Abilities;

public class RoleMarkAbility : FlexibleLifespan, IGameOperator, IBindPlayer
{
    static readonly Image? MarkImage = NebulaAPI.AddonAsset?.GetResource("Button/MarkButton.png")?.AsImage();
    public static BoolConfiguration CanUseMark = NebulaAPI.Configurations.Configuration("options.meeting.canUseMark", true);

    public static Dictionary<byte, HashSet<DefinedAssignable>>? MarkRoleDic { get; set; } = [];
    public static MetaScreen? LastMarkWindow = null;

    private GamePlayer Owner { get; set; }
    public GamePlayer MyPlayer => Owner;

    public RoleMarkAbility(ILifespan lifespan, GamePlayer player) : base(lifespan)
    {
        MarkRoleDic = [];
        foreach (var p in GamePlayer.AllPlayers)
            MarkRoleDic.Add(p.PlayerId, []);

        Owner = player;

        var assignables = Nebula.Roles.Roles.AllAssignables().Where(a => a is not DefinedGhostRole && a.ShowOnHelpScreen);

        var markButton = NebulaAPI.Modules.AbilityButton(this, isLeftSideButton: true, alwaysShow: true).SetImage(MarkImage!).SetLabel("mark");
        markButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting;
        markButton.Availability = _ => !Minigame.Instance.AsBoolFast() && MeetingHud.Instance.AsBoolFast() && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && MeetingHud.Instance.state != MeetingHud.VoteStates.Results && MeetingHud.Instance.state != MeetingHud.VoteStates.Proceeding && !AddonHelper.IsOutMeeting();
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
            },
            (tmPro, p) =>
            {
                if (!MarkRoleDic.TryGetValue(p.PlayerId, out var assignablesSet)) return;

                List<DefinedRole> roles = [];
                foreach (var assignable in assignablesSet)
                {
                    if (assignable is DefinedRole role)
                        roles.Add(role);
                }

                tmPro.text = string.Join(", ", roles.Select(r => r.GetRoleIconTag() + (roles.Count >= 2 ? " " + r.DisplayColoredShort : r.DisplayColoredName)));
            });
        };
    }

    public static string GetModifierString(byte id)
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