using DHMO.Roles.Script;

namespace DHMO.Roles.Impostor;

public class Bomber : DefinedSingleAbilityRoleTemplate<Bomber.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Bomber() : base("bomber", NebulaTeams.ImpostorTeam.Color, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, [IgniteBombCooldown, ActivateBombTime, BombExplodeTime, AfterMeetingResetBombTime, BombKillLeftDeadBody])
    {
    }
    public static readonly IRelativeCooldownConfiguration IgniteBombCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.bomber.igniteBombCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 5f, (0.125f, 2f, 0.125f), 1.125f);
    public static readonly FloatConfiguration ActivateBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.activatebombTime", (0f, 60f, 1f), 5f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration BombExplodeTime = NebulaAPI.Configurations.Configuration("options.role.bomber.bombExplodeTime", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration AfterMeetingResetBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.afterMeetingResetBomb", true);
    public static readonly BoolConfiguration BombKillLeftDeadBody = NebulaAPI.Configurations.Configuration("options.role.bomber.bombkillleftDeadbody", false);

    Citation? HasCitation.Citation => DHMOCitations.GGD;

    static public Bomber MyRole = new();

    bool IAssignableDocument.HasTips => true;

    public static Image? igniteBombImage = NebulaAPI.AddonAsset.GetResource("Button/IgniteBombButton.png")?.AsImage(115f)!;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public static ModAbilityButton? igniteBomb;
        bool IPlayerAbility.HideKillButton => igniteBomb != null && !igniteBomb.IsBroken;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                igniteBomb = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: true).BindKey(VirtualKeyInput.Kill)
                 .SetLabel("bomber.ignitebomb")
                 .SetLabelType(ModAbilityButton.LabelType.Impostor)
                 .SetAsUsurpableButton(this);

                igniteBomb.Visibility = _ => !MyPlayer.IsDead;
                igniteBomb.Availability = _ => MyPlayer.CanMove && !Bomb.HasBomb.Contains(MyPlayer.PlayerId);
                igniteBomb.SetImage(igniteBombImage!);
                igniteBomb.CoolDownTimer = NebulaAPI.Modules.Timer(this, IgniteBombCooldown.Cooldown).SetAsAbilityTimer().Start(null);

                igniteBomb.OnClick = _ =>
                {
                    Bomb.RPCSetBomb.Invoke((MyPlayer, MyPlayer, ActivateBombTime + BombExplodeTime));
                    igniteBomb.StartCoolDown();
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(igniteBomb.GetKillButtonLike());
            }
        }
    }
}