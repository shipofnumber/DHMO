/*namespace DHMO.Roles;

[NebulaRPCHolder]
public class Sorcerer : DefinedSingleAbilityRoleTemplate<Sorcerer.Ability>, DefinedRole, HasCitation
{
    private Sorcerer() : base("sorcerer", NebulaTeams.ImpostorTeam.Color, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, [HasKillButton, GazeTime, AfterMeetingTime]){}

    static public readonly BoolConfiguration HasKillButton = NebulaAPI.Configurations.Configuration("options.role.sorcerer.cankill", true);
    public static readonly FloatConfiguration GazeTime = NebulaAPI.Configurations.Configuration("options.role.sorcerer.gazeTime", (5f, 30f, 0.5f), 5f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration AfterMeetingTime = NebulaAPI.Configurations.Configuration("options.role.sorcerer.aftermeetingGaze", (0f, 20f, 0.5f), 5f, FloatConfigurationDecorator.Second);

    Citation? HasCitation.Citation => DHMOCitations.GGD;

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player);

    public static TranslatableTag Cursed = new ("state.cursedGaze");
    static public Sorcerer MyRole = new();

    public class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private byte target = 0;
        readonly PoolablePlayer icon = null!;
        readonly Arrow arrow = null!;
        static float time = 0f;
        bool IPlayerAbility.HideKillButton => !HasKillButton;

        void OnGameStart(GameStartEvent ev)
        {
            time = -10f;
        }
        public Ability(GamePlayer player) : base(player)
        {
            if (AmOwner)
            {
                var holder = HudContent.InstantiateStretchContent(this, "SorcererIcons", true, true);
                icon = AmongUsUtil.GetPlayerIcon(MyPlayer.Unbox().DefaultOutfit, holder, UnityEngine.Vector3.zero, UnityEngine.Vector3.one * 0.5f);
                icon.ToggleName(true);
                icon.SetAlpha(0.35f);
                icon.SetName(Mathf.Ceil(time * 100 / GazeTime).ToString() + "%", UnityEngine.Vector3.one * 4f, UnityEngine.Color.white, -1f);

                arrow = new Arrow().SetColor(Palette.ImpostorRed);
                arrow.Bind(this);
                arrow.RegisterSelf();
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => arrow.IsActive = !MyPlayer.IsDead, this);

                OnChangeTarget();
            }
        }

        void OnChangeTarget(GamePlayer? excluded = null)
        {
            var arr = PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => !p.AmOwner && p.PlayerId != excluded?.PlayerId && !p.Data.IsDead && MyPlayer.CanKill(p.GetModInfo()!)).ToArray();
            if (arr.Length == 0) target = byte.MaxValue;
            else target = arr[System.Random.Shared.Next(arr.Length)].PlayerId;

            if (target == byte.MaxValue)
                icon.gameObject.SetActive(false);
            else
            {
                icon.gameObject.SetActive(true);
                icon.UpdateFromPlayerOutfit(NebulaGameManager.Instance!.GetPlayer(target)!.Unbox().DefaultOutfit.Outfit.outfit, PlayerMaterial.MaskType.None, false, true);
            }
            UpdateArrow();
        }

        void UpdateTimer()
        {
            if (MyPlayer.HasAttribute(PlayerAttributes.Eyesight)) MyPlayer.RemoveAttribute(PlayerAttributes.Eyesight);
            if (float.IsNegative(time)) icon.SetName("0%");
            else icon.SetName(Mathf.Ceil(time * 100 / GazeTime).ToString() + "%");
            if (light)
            {
                if (!MeetingHud.Instance && !MyPlayer.IsDead)
                {
                    time += Time.deltaTime;
                    if (time >= GazeTime)
                    {
                        MyPlayer.MurderPlayer(NebulaGameManager.Instance!.GetPlayer(target)!, Cursed, PlayerStates.Dead, KillParameter.RemoteKill, KillCondition.BothAlive);
                        time = -(AfterMeetingTime);
                    }
                }
            }
            UpdateArrow();
        }

        [Local]
        void LocalUpdate(GameUpdateEvent ev) => UpdateTimer();

        void OnDead(PlayerDieOrDisconnectEvent ev)
        {
            if (ev.Player.VanillaPlayer.PlayerId == target)
            {
                OnChangeTarget();
            }
        }

        void OnTaskPhase(TaskPhaseStartEvent ev)
        {
            time = -(AfterMeetingTime);
        }

        void UpdateArrow()
        {
            var target = NebulaGameManager.Instance?.GetPlayer(this.target);
            if (target == null)
            {
                arrow.IsActive = false;
            }
            else
            {
                arrow.IsActive = true;
                arrow.TargetPos = target.VanillaPlayer.transform.localPosition;
            }
        }
    }
}*/