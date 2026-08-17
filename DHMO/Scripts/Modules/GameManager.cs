using DHMO.Roles.Abilities;
using Virial.Runtime;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;
        NebulaInput.modInput[(VirtualKeyInput)120] = new(GetModKeyCodeGetter("pass", KeyCode.Space));
    }

    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;
    protected override void OnInjected(Game container) => this.Register(container);

    internal static Func<KeyCode> GetModKeyCodeGetter(string translationKey, KeyCode defaultKey)
    {
        KeyAssignment assignment = new(translationKey, defaultKey);
        return () => assignment.KeyInput;
    }

    void OnGameStart(GameStartEvent ev)
    {
        if (GamePlayer.LocalPlayer == null) return;

        if (RoleMarkAbility.CanUseMark)
        {
            var markAbility = new RoleMarkAbility(this.MyContainer, GamePlayer.LocalPlayer);
            markAbility.RegisterSelf();
        }
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

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var local = GamePlayer.LocalPlayer;
        if (local is null || (local != ev.Player && (!NebulaGameManager.Instance?.CanSeeAllInfo ?? false))) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new VVector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
    }
}