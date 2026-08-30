using DHMO.Roles.Abilities;
using Virial.Runtime;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public class DGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;
        NebulaInput.modInput[(VirtualKeyInput)120] = new(GetModKeyCodeGetter("pass", KeyCode.Space));
    }

    static DGameManager() => DIManager.Instance.RegisterModule(() => new DGameManager());
    public DGameManager() => ModSingleton<DGameManager>.Instance = this;

    protected override void OnInjected(Game container) => this.Register(container);

    private static Func<KeyCode> GetModKeyCodeGetter(string translationKey, KeyCode defaultKey)
    {
        KeyAssignment assignment = new(translationKey, defaultKey);
        return () => assignment.KeyInput;
    }

    public int? GetPlayerDeadRound(GamePlayer player)
    {
        if (deadPlayers.TryGetValue(player.PlayerId, out var round)) 
            return round;
        
        return null;
    }

    public int? CurrentRound { get; private set; }
    private Dictionary<byte, int> deadPlayers = [];
    
    void OnGameStart(GameStartEvent ev)
    {
        if (GamePlayer.LocalPlayer == null) return;

        deadPlayers = [];
        if (RoleMarkAbility.CanUseMark)
        {
            var markAbility = new RoleMarkAbility(this.MyContainer, GamePlayer.LocalPlayer);
            markAbility.RegisterSelf();
        }
    }

    void OnTaskPhaseStart(TaskPhaseStartEvent ev)
    {
        CurrentRound = 1;
    }

    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        if (CurrentRound == null) return;
        ++CurrentRound;
    }

    void OnPlayerDieOrDisconnected(PlayerDieOrDisconnectEvent ev)
    {
        if (CurrentRound == null) return;
        deadPlayers.Add(ev.Player.PlayerId, CurrentRound.Value);
    }

    void OnPlayerRevive(PlayerReviveEvent ev)
    {
        deadPlayers.Remove(ev.Player.PlayerId);
    }
    
    void OnMeetingPreEnd(MeetingPreEndEvent ev)
    {
        foreach (var player in GamePlayer.AllPlayers)
        {
            try
            {
                if (player.VanillaPlayer.AsBoolFast(out var pc)) pc.ModResetForMeeting(!player.TryGetModifier<Communicator.Instance>(out var _));
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }
    }

    void CheckPlayerStepSound(PlayerCheckPlayFootSoundEvent ev)
    {
        ev.PlayFootSound &= !APICompat.IsOutMeeting();
    }

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var local = GamePlayer.LocalPlayer;
        if (local is null || (local != ev.Player && !(NebulaGameManager.Instance?.CanSeeAllInfo ?? false))) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new VVector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class ScarletLoversCriteria : AbstractModule<IGameModeStandard>, IGameOperator
{
    static ScarletLoversCriteria() => DIManager.Instance.RegisterModule(() => new ScarletLoversCriteria().RegisterPermanently());

    [OnlyHost]
    void OnUpdate(GameUpdateEvent ev)
    {
        int totalAlive = NebulaGameManager.Instance!.AllPlayerInfo.Count((p) => !p.IsDead);
        if (totalAlive > 3) return;

        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            if (p.IsDead || p.Role is not Scarlet.Instance scarlet) continue;

            var favourite = scarlet.GetMyFavorite();
            if (favourite == null || favourite.IsDead) continue;

            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ScarletWin, GameEndReason.Situation, BitMasks.AsPlayer(p));
        }
    }
}