using DHMO.Roles.Abilities;
using Virial.Runtime;
using Object = UnityEngine.Object;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public class DGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;

        Lover.MyRole.ConfigurationHolder?.AppendConfiguration(TriangleModeOption);

        string[] simpleSwitch = ["options.switch.off", "options.switch.on"];
        new ClientOption((ClientOption.ClientOptionType)73, "introRoleTextBold", simpleSwitch, 0);

        NebulaInput.modInput[(VirtualKeyInput)120] = new(GetModKeyCodeGetter("pass", KeyCode.Space));
    }

    static DGameManager() => DIManager.Instance.RegisterModule(() => new DGameManager());
    public DGameManager() => ModSingleton<DGameManager>.Instance = this;

    public static BoolConfiguration TriangleModeOption = NebulaAPI.Configurations.Configuration("options.lover.triangleMode", false, () => !Lover.AvengerModeOption);

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

        if (Helpers.CurrentMonth == 9 && System.DateTime.Now.Day >= 25)
        {
            var builder = new TutorialBuilder()
                .AsSimpleTitledTextWidget("DHMO 1st!", Language.Translate("anniversary.1st.message"))
                .ShowEvenIfSomeUiOpen().ShowWhile(() =>
                    AmongUsLLImpl.AmongUsClientInstance.GameState != InnerNet.InnerNetClient.GameStates.Started);
            Tutorial.ShowTutorial(builder);
        }
    }

    private static Func<KeyCode> GetModKeyCodeGetter(string translationKey, KeyCode defaultKey)
    {
        KeyAssignment assignment = new(translationKey, defaultKey);
        return () => assignment.KeyInput;
    }

    public int? GetPlayerDeadTurn(GamePlayer player)
    {
        if (deadPlayers.TryGetValue(player.PlayerId, out var round)) 
            return round;
        
        return null;
    }

    public int CurrentTurn { get; private set; }
    private Dictionary<byte, int> deadPlayers = [];
    
    void OnGameStart(GameStartEvent ev)
    {
        if (GamePlayer.LocalPlayer == null) return;

        CurrentTurn = 1;
        if (RoleMarkAbility.CanUseMark)
        {
            var markAbility = new RoleMarkAbility(this.MyContainer, GamePlayer.LocalPlayer);
            markAbility.RegisterSelf();
        }
    }

    void OnLobbyDestroy(LobbyDestroyEvent ev)
    {
        CurrentTurn = 0;
        deadPlayers = [];
    }

    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        CurrentTurn += 1;
    }

    [EventPriority(-100000)]
    void OnShowIntro(GameShowIntroLocalEvent ev)
    {
        if (ClientOption.GetValue((ClientOption.ClientOptionType)51) == 1)
        {
            var roleText = IntroCutscene.Instance.RoleText;

            roleText.fontWeight = TMPro.FontWeight.Thin;
            roleText.SetOutlineColor(ev.RoleColor.ShadeColor(0.1f).SetAlpha(0.38f));
            roleText.SetOutlineThickness(0.17f);
        }
        
        var vanillaRole = AmongUsLLImpl.LocalPlayer.Data.Role;
        var roleSound = vanillaRole.IntroSound;

        GamePlayer? localPlayer = GamePlayer.LocalPlayer;
        if (localPlayer == null) return;

        var assignable = localPlayer.Role.AssignableOnHelp.ToArray()[0];
        if (assignable is not DefinedRole role) return;
        
        if (role.InternalName.Contains("justice") || role is EvilJudge)
            vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<JudgeRole>()?.IntroSound;
        else if (role.InternalName.Contains("engineer"))
            vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<EngineerRole>()?.IntroSound;
        else if (localPlayer.IsKiller && localPlayer.IsNeutral)
        {
            vanillaRole.IntroSound = AmongUsUtil.GetRolePrefab<ShapeshifterRole>()?.IntroSound;
        }
        else if (localPlayer.IsMadmate)
        {
            vanillaRole.IntroSound = DHMOAsset.DuckIntroSound;
        }

        vanillaRole.IntroSound = role switch
        {
            Pelican => DHMOAsset.PelicanIntroSound,
            Raven => DHMOAsset.RavenIntroSound,
            Jester => DHMOAsset.SillyGooseIntroSound,
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
            Vulture => DHMOAsset.VultureIntroSound,
            _ => roleSound
        };
    }

    void OnPlayerDieOrDisconnected(PlayerDieOrDisconnectEvent ev)
    {
        if (deadPlayers.TryGetValue(ev.Player.PlayerId, out var round))
        {
            deadPlayers[ev.Player.PlayerId] = CurrentTurn;
            return;
        }
        
        deadPlayers.Add(ev.Player.PlayerId, CurrentTurn);
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
    
    void OnMeetingEnd(MeetingEndEvent ev)
    {
        foreach (var player in GamePlayer.AllPlayers)
        {
            try
            {
                if (player.VanillaPlayer.AsBoolFast(out var pc))
                {
                    bool shouldNotReset = player.IsAlive && (NebulaAPI.CurrentGame?.CurrentMap?.IsInMap(player.TruePosition) ?? false) && player.TryGetModifier<Communicator.Instance>(out _);
                    pc.ModResetForMeeting(!shouldNotReset);
                }
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }
        
        ChangeAudiosMixer(false);
        ClientOption.ChangeAmbientVolumeIfNecessary(false, true);
    }

    void CheckPlayerStepSound(PlayerCheckPlayFootSoundEvent ev)
    {
        ev.PlayFootSound &= !APICompat.IsOutMeeting();
    }

    internal static Image? involveImage = NebulaAPI.AddonAsset.GetResource("Button/InvolveButton.png")?.AsImage(120f);

    void OnSetLover(PlayerModifierSetEvent ev)
    {
        if (Lover.AvengerModeOption || !TriangleModeOption || !ev.Player.AmOwner)
            return;

        if (ev.Modifier is not Lover.Instance lover || lover.MyPlayer.IsDead || lover.IsAloneLover)
            return;
        
        if (GamePlayer.AllPlayers.Any(p => p.TryGetAbility<Cupid.Ability>(out var ability) && (ability.selected1 == lover.MyPlayer || ability.selected2 == lover.MyPlayer) && ability.hasCreatedLover))
            return;

        var involveButton = NebulaAPI.Modules.AbilityButton(lover, true)
            .SetImage(involveImage!)
            .SetLabel("lover.involve")
            .SetColorLabel(Lover.MyRole.Color);

        int recordTurn = CurrentTurn == 0 ? 1 : CurrentTurn;
        int deltaTurn = (int)LoveTriangle.InvolveInactiveTurnsOption - CurrentTurn + recordTurn;
        GameObject? lockSprite = null;

        if (LoveTriangle.SealUntilSecondTurnOption)
        {
            lockSprite = involveButton.AddLockedOverlay();
            GameOperatorManager.Instance?.Subscribe<TaskPhaseRestartEvent>(_ =>
            {
                if (lockSprite == null || !HudManager.InstanceExists) return;
                (involveButton as ModAbilityButtonImpl)?.PlayFlashOnce();
                Object.Destroy(lockSprite.gameObject);
                lockSprite = null;
            }, lover);
        }

        var involveTracker = ObjectTrackers.ForPlayer(
            lover,
            0.75f,
            lover.MyPlayer,
            p => ObjectTrackers.StandardPredicate(p) && lockSprite == null && p != lover.MyLover.Get(),
            Lover.MyRole.UnityColor,
            false
        );

        involveButton.Visibility = _ => lover.MyPlayer.IsAlive && lover.MyLover.Get().IsAlive &&
                                        deltaTurn > 0;

        involveButton.Availability = _ =>
            involveTracker.CurrentTarget != null && lover.MyPlayer.CanMove && lockSprite == null;
        involveButton.CoolDownTimer = NebulaAPI.Modules.Timer(lover, LoveTriangle.InvolveCooldownOption)
            .SetAsAbilityTimer().Start();
        involveButton.ShowUsesIcon(4, ((int)LoveTriangle.InvolveInactiveTurnsOption).ToString());

        GameOperatorManager.Instance?.Subscribe<TaskPhaseRestartEvent>(_ =>
        {
            deltaTurn = (int)LoveTriangle.InvolveInactiveTurnsOption - CurrentTurn + recordTurn;
            if (deltaTurn <= 0)
            {
                (involveTracker as FlexibleLifespan)?.Release();
                (involveButton as FlexibleLifespan)?.Release();
            }
            
            involveButton.UpdateUsesIcon(deltaTurn.ToString());
        }, lover, -10);

        involveButton.OnClick = _ =>
        {
            var target = involveTracker.CurrentTarget;
            if (target == null || target.IsDead) return;

            using (RPCRouter.CreateSection("AddLoveTriangle", true))
            {
                lover.MyPlayer.AddModifier(LoveTriangle.MyRole, [lover.LoversId]);
                lover.MyLover.Get().AddModifier(LoveTriangle.MyRole, [lover.LoversId]);
                target.AddModifier(LoveTriangle.MyRole, [lover.LoversId]);

                RpcRemoveLover.Invoke((lover.MyPlayer.PlayerId, lover.LoversId));
                RpcRemoveLover.Invoke((lover.MyLover.Get().PlayerId, lover.LoversId));
            }
        };
    }

    private readonly static RemoteProcess<(byte playerId, int loverId)> RpcRemoveLover = new(
        "RemoveLover", (message, _) =>
        {
            NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Unbox()
                .UnsetModifierLocal((m) => m is Lover.Instance lover && lover.LoversId == message.loverId);
        }
    );

    void OnRoleChanged(PlayerTryToChangeRoleEvent ev)
    {
        var local = GamePlayer.LocalPlayer;
        if (local is null || (local != ev.Player && !(NebulaGameManager.Instance?.CanSeeAllInfo ?? false))) return;
        
        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player.RoleText.transform, new VVector3(0f, 0f, -0.1f), ev.NextRole.Color.ToUnityColor(), ev.Player.RoleText.gameObject.layer, 1.42857146f).StartOnScene());
    }
}

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
internal class ScarletLoversCriteria : AbstractModule<IGameModeStandard>, IGameOperator
{
    static ScarletLoversCriteria() => DIManager.Instance.RegisterModule(() => new ScarletLoversCriteria().RegisterPermanently());

    [OnlyHost]
    void OnUpdate(GameUpdateEvent ev)
    {
        var totalAlive = APICompat.AlivePlayers;
        if (totalAlive.Length > 3) return;

        foreach (var p in totalAlive)
        {
            if (p.Role is not Scarlet.Instance scarlet) continue;

            var favourite = scarlet.GetMyFavorite();
            if (favourite == null || favourite.IsDead) continue;

            NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ScarletWin, GameEndReason.Situation, BitMasks.AsPlayer(p).Add(favourite));
        }
    }
}