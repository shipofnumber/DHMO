namespace DHMO.Roles.Modifiers;

public class Radar : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Radar() : base("radar", "RAD", new(255, 0, 128), [DetectDistanceOption, UpdateIntervalOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    static internal FloatConfiguration DetectDistanceOption = NebulaAPI.Configurations.Configuration("options.role.radar.detectDistance", (10f, 50f, 1f), 20f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration UpdateIntervalOption = NebulaAPI.Configurations.Configuration("options.role.radar.updateInterval", (0f, 10f, 1f), 1f, FloatConfigurationDecorator.Second);

    Image? DefinedAssignable.IconImage => Nebula.Roles.Crewmate.NiceTracker.MyRole.GetRoleIcon();
    static public Radar MyRole = new();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance(GamePlayer player) : RuntimeAssignableTemplate(player), RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var trackAbility = new RadarTrackingArrowAbility(null, MyPlayer, UpdateIntervalOption, MyRole.Color);
            trackAbility.RegisterSelf().Bind(this);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += MyRole.GetRoleIconTagSmall();
        }
    }
}

public class RadarTrackingArrowAbility(GamePlayer? target, GamePlayer radar, float interval, VColor color) : FlexibleLifespan, IGameOperator
{
    public GamePlayer? MyPlayer => target;

    GamePlayer? target = target;
    GamePlayer radar = radar; 
    float interval = interval;
    float timer = -1f;
    Arrow? arrow = null;
    VColor color = color;

    public void SetTarget(GamePlayer? target)
    {
        if (target != null) this.target = target;
    }

    void Update(GameUpdateEvent ev)
    {
        if (ExileController.Instance.AsBoolFast())
            timer = -1f;
        else
        {
            timer -= ev.DeltaTime;

            if (timer < 0f)
            {
                arrow ??= new Arrow(null, true, false) { IsAffectedByComms = true }.SetColor(color).Register(this);

                if (target != null)
                    arrow.TargetPos = target.Position;

                timer = interval;
            }
        }

        var player = APICompat.GetClosestPlayer(radar, Radar.DetectDistanceOption);
        if (player != null) this.SetTarget(player);

        arrow?.IsActive = target != null && radar.Position.Distance(target.Position) <= Radar.DetectDistanceOption && !target.IsDead && radar.IsAlive && !AmongUsUtil.InMeeting;
    }
}