namespace DHMO.Roles.Abilities;

public class RoleMarkAbility : FlexibleLifespan, IGameOperator, IBindPlayer
{
    static readonly Image? MarkImage = NebulaAPI.AddonAsset?.GetResource("Button/MarkButton.png")?.AsImage();
    public static BoolConfiguration CanUseMark = NebulaAPI.Configurations.Configuration("options.meeting.canUseMark", true);

    public static Dictionary<byte, HashSet<DefinedAssignable>>? MarkRoleDic { get; set; }
    public static MetaScreen? LastMarkWindow = null;

    private GamePlayer Owner {  get; set; }
    public GamePlayer MyPlayer => Owner;

    public RoleMarkAbility(ILifespan lifespan, GamePlayer player) : base(lifespan)
    {
        MarkRoleDic = [];
        foreach (var p in GamePlayer.AllPlayers)
            MarkRoleDic.Add(p.PlayerId, []);

        Owner = player;

        var assignables = Nebula.Roles.Roles.AllAssignables().Where(a => a is not DefinedGhostRole && a.ShowOnHelpScreen);

        var markButton = NebulaAPI.Modules.AbilityButton(this, isLeftSideButton: true, alwaysShow: true).SetImage(MarkImage!).SetLabel("mark");
        markButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && CanUseMark;
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
                            renderer.color = VColor.White;
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

    public static string GetModifierString(GamePlayer player)
    {
        if (MarkRoleDic == null) return string.Empty;
        var modifiers = MarkRoleDic[player.PlayerId].Where(a => a is DefinedModifier);
        if (modifiers.Any() && modifiers != null)
        {
            string text = $"<b>{Language.Translate("help.rolePreview.inner.modifiers")}</b>";
            foreach (var modifier in modifiers)
                text += $"<br>{modifier.GetRoleIconTag()}<b>{modifier.DisplayColoredName}</b>";

            return text;
        }
        return string.Empty;
    }

    static (string?, Predicate<DefinedAssignable>?)[] GetAssignableTab()
    {
        static Predicate<DefinedAssignable> IsRoleOfCategory(RoleCategory category) => a => a is DefinedRole role && role.Category == category;

        return
        [
          ("<b>" + Language.Translate("help.rolePreview.category.impostor").Color(NebulaTeams.ImpostorTeam.Color) + "</b>", IsRoleOfCategory(RoleCategory.ImpostorRole)),
          ("<b>" + Language.Translate("help.rolePreview.category.neutral").Color(new VColor(255, 178, 0)) + "</b>", IsRoleOfCategory(RoleCategory.NeutralRole)),
          ("<b>" + Language.Translate("help.rolePreview.category.crewmate").Color(NebulaTeams.CrewmateTeam.Color) + "</b>", IsRoleOfCategory(RoleCategory.CrewmateRole)),
          ("<b>" + Language.Translate("help.rolePreview.inner.modifiers") + "</b>", a => a is DefinedModifier),
        ];
    }
}