using System.Numerics;

namespace DHMO.Roles.Crewmate;

public class Uniter : DefinedSingleAbilityRoleTemplate<Uniter.Ability>, DefinedSingleAssignable, DefinedAssignable, DefinedRole, IAssignableDocument, HasCitation
{
    private Uniter() : base("uniter", new(64, 226, 193), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [NumOfUnitingOption, NumOfCanUnitOption, MaxLeftVotingTimeForUniting])
    {
    }

    static private readonly IntegerConfiguration NumOfUnitingOption = NebulaAPI.Configurations.Configuration("options.role.uniter.numOfuniting", (1, 15), 3);
    static private readonly IntegerConfiguration NumOfCanUnitOption = NebulaAPI.Configurations.Configuration("options.role.uniter.numOfcanUnit", (1, 5), 2);
    static private readonly FloatConfiguration MaxLeftVotingTimeForUniting = NebulaAPI.Configurations.Configuration("options.role.uniter.maxLeftVotingTimeForUniting", (0f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.Get(0, NumOfUnitingOption));
    Citation? HasCitation.Citation => DHMOCitations.DHMO;
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("RoleIcon/UniterIcon.png")?.AsImage(115f);

    public static readonly Uniter MyRole = new();

    bool IAssignableDocument.HasTips => true;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerAbility, IPlayerAbility, IBindPlayer, IGameOperator, ILifespan
    {
        Image? buttonImage = NebulaAPI.AddonAsset.GetResource("Button/UniterMeetingButton.png")?.AsImage(1200f);
        int leftUniting = NumOfUnitingOption;

        public EditableBitMask<GamePlayer> selected = BitMasks.AsPlayer();

        public Ability(GamePlayer player, int leftUses) : base(player)
        {
            leftUniting = leftUses;

            if (AmOwner)
            {
                string prefix = Language.Translate("roles.uniter.leftUniting");
                Helpers.TextHudContent("UniterText", this, (tmPro) => tmPro.text = $"{prefix}: {leftUniting}", true);
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (leftUniting <= 0) return;
            selected.Clear();

            NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>()?.RegisterMeetingAction(new(buttonImage!, state =>
            {
                var p = state.MyPlayer;
                if (!state.IsSelected)
                {
                    if (BitOperations.PopCount(selected.AsRawPattern) < NumOfCanUnitOption)
                    {
                        selected.Add(p);
                        state.SetSelect(true);
                    }
                }
                else
                {
                    selected.Remove(p);
                    state.SetSelect(false);
                }
            }, (p) => (!MeetingHud.Instance.GetPlayer(MyPlayer.PlayerId).DidVote || p.IsSelected) && MeetingHudExtension.VotingTimer > MaxLeftVotingTimeForUniting && leftUniting > 0 && p.MyPlayer.IsAlive && !p.MyPlayer.AmOwner && MyPlayer.IsAlive));
        }

        [OnlyMyPlayer]
        void OnVote(PlayerVoteCastLocalEvent ev)
        {
            if (selected.ForEach(GamePlayer.AllPlayers).Any()) --leftUniting;
            RpcUpdateStatus.Invoke((MyPlayer, selected.AsRawPattern));
        }

        void FixVote(PlayerFixVoteHostEvent ev)
        {
            if (MyPlayer.IsDead) return;
            var uniterArea = MeetingHud.Instance.GetPlayer(MyPlayer.PlayerId);

            if (selected.Test(ev.Player))
            {
                if (uniterArea?.DidVote ?? false && ev.DidVote)
                {
                    ev.VoteTo = NebulaGameManager.Instance?.GetPlayer(uniterArea.VotedForId.Value);
                }
            }
        }
    }

    static private readonly RemoteProcess<(GamePlayer uniter, uint mask)> RpcUpdateStatus = new("UpdateUniter", (message, _) => 
    {
        if (!message.uniter.TryGetAbility<Uniter.Ability>(out var uniter)) return;
        uniter.selected = BitMasks.AsPlayer(message.mask);
    });
}