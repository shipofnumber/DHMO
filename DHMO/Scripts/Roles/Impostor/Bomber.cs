using DHMO.Roles.Script;

namespace DHMO.Roles.Impostor;

public class Bomber : DefinedSingleAbilityRoleTemplate<Bomber.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Bomber() : base("bomber", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, 
        [IgniteBombCooldown, BombIgniteOfOption, ActivateBombTime, BombExplodeTime, AfterMeetingResetBombTime, BombKillLeftDeadBody])
    {
    }

    public static readonly IRelativeCooldownConfiguration IgniteBombCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.bomber.igniteBombCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 5f, (0.125f, 2f, 0.125f), 1.125f);
    private static readonly ValueConfiguration<int> BombIgniteOfOption = NebulaAPI.Configurations.Configuration("options.role.bomber.bombIgniteOf", ["options.role.bomber.bombIgniteOf.self", "options.role.bomber.bombIgniteOf.other"], 0);
    public static readonly FloatConfiguration ActivateBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.activatebombTime", (0f, 60f, 1f), 5f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration BombExplodeTime = NebulaAPI.Configurations.Configuration("options.role.bomber.bombExplodeTime", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    public static readonly BoolConfiguration AfterMeetingResetBombTime = NebulaAPI.Configurations.Configuration("options.role.bomber.afterMeetingResetBomb", true);
    public static readonly BoolConfiguration BombKillLeftDeadBody = NebulaAPI.Configurations.Configuration("options.role.bomber.bombkillleftDeadbody", false);

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/BomberIcon.png")?.AsImage();
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

        private ObjectTracker<GamePlayer>? igniteTracker = null;

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                if (BombIgniteOfOption.GetValue() == 1)
                    igniteTracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, p => ObjectTrackers.StandardPredicate(p), VColor.ImpostorColor.ToUnityColor(), false, false);

                igniteBomb = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: true).BindKey(VirtualKeyInput.Kill)
                 .SetLabel("bomber.ignitebomb")
                 .SetLabelType(ModAbilityButton.LabelType.Impostor)
                 .SetAsUsurpableButton(this);

                igniteBomb.Visibility = _ => MyPlayer.IsAlive;
                igniteBomb.Availability = _ => MyPlayer.CanMove && (igniteTracker == null || igniteTracker.CurrentTarget != null);
                igniteBomb.SetImage(igniteBombImage!);
                igniteBomb.CoolDownTimer = NebulaAPI.Modules.Timer(this, IgniteBombCooldown.Cooldown).SetAsAbilityTimer().Start();

                igniteBomb.OnClick = button =>
                {
                    if (igniteTracker != null)
                    {
                        var target = igniteTracker.CurrentTarget;
                        if (target != null) Bomb.RPCSetBomb.Invoke((target, MyPlayer, ActivateBombTime + BombExplodeTime));
                    }
                    else
                        Bomb.RPCSetBomb.Invoke((MyPlayer, MyPlayer, ActivateBombTime + BombExplodeTime));

                    button.StartCoolDown();
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(igniteBomb.GetKillButtonLike());
            }
        }
    }
}