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
        
        string[] simpleSwitch = ["options.switch.off", "options.switch.on"];
        new ClientOption((ClientOption.ClientOptionType)51, "introRoleTextBold", simpleSwitch, 0);
        NebulaInput.modInput[(VirtualKeyInput)120] = new(GetModKeyCodeGetter("pass", KeyCode.Space));
    }

    static DGameManager() => DIManager.Instance.RegisterModule(() => new DGameManager());
    public DGameManager() => ModSingleton<DGameManager>.Instance = this;

    protected override void OnInjected(Game container)
    {
        this.Register(container);
        
        string[] audioStrings =
        [
            "AMB_EngineShock",
            "AMB_EngineSteam",
            "Decontamination_doorOpen",
            "Decontamination_doorClose",
            "Door_balconyClose",
            "Door_balconyOpen",
            "Decontamination_Spray",
            "door_close",
            "door_open",
            "AMB_shower",
            "AMB_Electricshock1",
            "AMB_Electricshock2",
            "AMB_Electricshock3",
            "AMB_Electricshock4",
            "AMB_Jungle",
            "AMB_Reactor"
        ];

        List<AudioClip> clips = [];
        foreach (var name in audioStrings)
        {
            var clip = VanillaAsset.GetAudioClip(name);
            if (clip == null)
            {
                DLog.Log($"{name} is null.");
                continue;
            }
            
            clips.Add(clip);
        }

        shouldChangeInMeeting = [.. clips];
    }

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

    public int CurrentRound { get; private set; }
    private Dictionary<byte, int> deadPlayers = [];
    
    void OnGameStart(GameStartEvent ev)
    {
        if (GamePlayer.LocalPlayer == null) return;
        
        CurrentRound = 1;
        if (RoleMarkAbility.CanUseMark)
        {
            var markAbility = new RoleMarkAbility(this.MyContainer, GamePlayer.LocalPlayer);
            markAbility.RegisterSelf();
        }
    }

    void OnLobbyDestroy(LobbyDestroyEvent ev)
    {
        CurrentRound = 0;
        deadPlayers = [];
    }

    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        CurrentRound += 1;
    }

    [EventPriority(-100000)]
    void OnShowIntro(GameShowIntroLocalEvent ev)
    {
        var vanillaRole = AmongUsLLImpl.LocalPlayer.Data.Role;
        var roleSound = vanillaRole.IntroSound;

        if (GamePlayer.LocalPlayer!.Role.AssignableOnHelp.FirstOrDefault(r => r is DefinedRole) is not DefinedRole role) return;
        
        if (role.InternalName.Contains("justice") || role is EvilJudge)
            vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<JudgeRole>()?.IntroSound;
        else if (role.InternalName.Contains("engineer"))
            vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<EngineerRole>()?.IntroSound;

        if (role.Category == RoleCategory.NeutralRole)
        {
            if (role.IsKiller)
                vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<ShapeshifterRole>()?.IntroSound;
            
            if (role.InternalName.Contains("moran"))
                vanillaRole.IntroSound = NebulaAsset.GetAudioClip(NebulaAudioClip.SniperEquip);
        }

        vanillaRole.IntroSound = role switch
        {
            Paparazzo or Journalist => NebulaAsset.GetAudioClip(NebulaAudioClip.Camera),
            Oracle or Investigator or Seer => AmongUsUtil.GetRolePrefab<DetectiveRole>()?.IntroSound,
            NiceTracker or EvilTracker => AmongUsUtil.GetRolePrefab<TrackerRole>()?.IntroSound,
            Doctor => AmongUsUtil.GetRolePrefab<ScientistRole>()?.IntroSound,
            Sheriff => APICompat.HnSRefreshClip.Clip,
            Noisemaker => AmongUsUtil.GetRolePrefab<NoisemakerRole>()?.IntroSound,
            Bubblegun => NebulaAsset.GetAudioClip(NebulaAudioClip.Bubble1),
            Sniper => NebulaAsset.GetAudioClip(NebulaAudioClip.SniperEquip),
            Effacer => AmongUsUtil.GetRolePrefab<PhantomRole>()?.IntroSound,
            Viper => AmongUsUtil.GetRolePrefab<ViperRole>()?.IntroSound,
            _ => roleSound
        };

        if (GamePlayer.LocalPlayer.IsMadmate)
            vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<ImpostorRole>()?.IntroSound;

        if (ClientOption.GetValue((ClientOption.ClientOptionType)51) == 0) return;

        var roleText = IntroCutscene.Instance.RoleText;

        roleText.fontWeight = TMPro.FontWeight.Thin;
        roleText.SetOutlineColor(ev.RoleColor.ShadeColor(0.1f).SetAlpha(0.38f));
        roleText.SetOutlineThickness(0.17f);
    }

    void OnPlayerDieOrDisconnected(PlayerDieOrDisconnectEvent ev)
    {
        if (deadPlayers.TryGetValue(ev.Player.PlayerId, out var round))
        {
            deadPlayers[ev.Player.PlayerId] = CurrentRound;
            return;
        }
        
        deadPlayers.Add(ev.Player.PlayerId, CurrentRound);
    }

    void OnPlayerRevive(PlayerReviveEvent ev)
    {
        deadPlayers.Remove(ev.Player.PlayerId);
    }

    private static AudioClip[] shouldChangeInMeeting = [];

    private static void ChangeAudiosMixer(bool inMeeting)
    {
        if (!AmongUsLLImpl.SoundManagerInstance.AsBoolFast(out var soundManager)) return;
        if (shouldChangeInMeeting.Length == 0) return;

        var targetChannel = inMeeting ? soundManager.AmbienceChannel : soundManager.SfxChannel;
        var soundPlayers = soundManager.soundPlayers;

        foreach (var audioClip in shouldChangeInMeeting)
        {
            if (soundManager.allSources.TryGetValue(audioClip, out var source))
            {
                source.outputAudioMixerGroup = targetChannel;
                continue;
            }

            foreach (var player in soundPlayers)
            {
                if (player.Player.clip.EqualsFast(audioClip))
                {
                    player.Player.outputAudioMixerGroup = targetChannel;
                }
            }
        }
    }

    
    void OnMeetingPreStart(MeetingPreStartEvent ev)
    {
        ChangeAudiosMixer(true);
        MeetingHudExtensionPatch.RightContents.Clear();
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
        
        ChangeAudiosMixer(false);
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