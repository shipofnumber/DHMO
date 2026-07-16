using BepInEx;
using DHMO.Roles.Abilities;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;
    protected override void OnInjected(Game container)
    {
        this.Register(container);
        NebulaManager.commands.Add(new NebulaManager.MetaCommand("help.command.showRule",
            () => container != null && !LobbyRules.IsShown,
            () =>
            {
                if (AmongUsLLImpl.LocalPlayer.AmHost())
                {
                    var files = Directory.GetFiles(Path.Combine(Paths.GameRootPath, "LobbyRules"), "*.txt", SearchOption.AllDirectories);
                    if (files.Length > 1)
                        LobbyRules.CreateSelectFileScreen();
                    else
                    {
                        LobbyRules.currentPath = files.FirstOrDefault();
                        LobbyRules.selectedPath = files.FirstOrDefault();
                        LobbyRules.OpenHostRuleScreen();
                    }
                }
                else
                    LobbyRules.RpcRequestRules.Invoke(AmongUsLLImpl.LocalPlayer.PlayerId);
            })
        { DefaultKeyInput = new(KeyCode.F7), });
    }

    void OnGameStart(GameStartEvent ev)
    {
        if (GamePlayer.LocalPlayer != null && RoleMarkAbility.CanUseMark)
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
                if (player.VanillaPlayer.AsBoolFast()) player.VanillaPlayer.ModResetForMeeting();
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