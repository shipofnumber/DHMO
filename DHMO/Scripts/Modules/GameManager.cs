using DHMO.Roles.Abilities;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;
    protected override void OnInjected(Game container) => this.Register(container);

    void OnGameStart(GameStartEvent ev)
    {
        if (GamePlayer.LocalPlayer != null && RoleMarkAbility.CanUseMark)
        {
            var markAbility = new RoleMarkAbility(this.MyContainer, GamePlayer.LocalPlayer);
            markAbility.RegisterSelf();
        }
    }

    void OnMeetingPreEnd(MeetingVoteEndEvent ev)
    {
        if (NebulaAPI.CurrentGame is null) return;
        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player.VanillaPlayer) MeetingStartPatch.ModResetForMeeting(player.VanillaPlayer);
        }
    }

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game is null || local is null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new VVector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
    }
}