using Virial.Runtime;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class TimeHuntManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule(() => new TimeHuntManager());

        TimeHunt.Register("raven", Raven.RavenHuntDuration, () =>
        {
            if (!Raven.InvokeRavenHunt) return false;
            int aliveCount = APICompat.AlivePlayers.Length;

            return aliveCount <= Raven.RavenHuntAliveNum && GamePlayer.AllPlayers.Any(p => p.Role is Raven.Instance && p.IsAlive) && !NebulaPreSpawnMinigame.PreSpawnMinigame.AsBoolFast() && !AmongUsUtil.InMeeting;
        }, Raven.MyRole.Color, Raven.TaskPhaseRestartHuntTimeDisperse);

        TimeHunt.Register("pelican", Pelican.PelicanHuntDuration, () =>
        {
            if (!Pelican.InvokePelicanHunt) return false;
            var alivePlayers = APICompat.AlivePlayers;
            int totalAlive = alivePlayers.Length;

            var pelicans = alivePlayers.Select(p =>
            {
                p.TryGetRole<Pelican.Instance>(out var pelican);
                return pelican;
            }).NotNull().ToArray();
            int totalDevoured = pelicans.Sum(p => p.DevouredPlayerMask.Count);

            return pelicans.Length > 0 && (totalAlive - totalDevoured) <= Pelican.PelicanHuntAliveNum && !NebulaPreSpawnMinigame.PreSpawnMinigame.AsBoolFast() && !AmongUsUtil.InMeeting;
        }, Pelican.MyRole.Color, Pelican.TaskPhaseRestartPelicanHuntDisperse);
    }

    public TimeHuntManager() => ModSingleton<TimeHuntManager>.Instance = this;

    protected override void OnInjected(Game container) => this.Register(container);

    public bool IsAnyTimeRun { get; set; }
    static Coroutine? FlashCoroutine { get; set; } = null;
    static bool Even { get; set; }

    void OnHudUpdate(GameHudUpdateEvent ev)
    {
        for (int i = 0; i < TimeHunt.AllTimes.Count; i++)
        {
            TimeHunt time = TimeHunt.AllTimes[i];

            if (time.IsRunning)
            {
                if (!time.CanRunning)
                    time.Stop(false);
                else if (time.Time <= 0f)
                    time.Stop(true);
            }
            else if (time.CanRunning)
                time.Start();

            if (time.IsRunning)
                time.Time -= ev.DeltaTime;
        }

        IsAnyTimeRun = TimeHunt.AllTimes.Any(t => t.IsRunning && t.CanRunning);
        bool hasFlashCoroutine = FlashCoroutine != null;

        if (IsAnyTimeRun && !hasFlashCoroutine)
        {
            var hud = AmongUsLLImpl.HudManagerInstance;

            hud.StopOxyFlash();
            hud.StopReactorFlash();
            FlashCoroutine = hud.StartCoroutine(CoTimeHuntFlash().WrapToIl2Cpp());
        }

        if (!IsAnyTimeRun && hasFlashCoroutine)
            StopTimeHuntFlash();
    }

    void OnUpdateVentState(PlayerUpdateVentStateLocalEvent ev) => ev.CannotUseVentTemporary |= IsAnyTimeRun;

    [OnlyHost]
    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        if (GeneralConfigurations.SpawnMethodOption.GetValue() != 0) return;

        var times = TimeHunt.AllTimes.Where(t => t.IsRunning && t.CanRunning && t.ShouldBeDisperse).ToArray();
        if (times.Length > 0) RpcDisperse.Invoke();
    }

    void CheckCanPushEmergencyButton(CheckCanPushEmergencyButtonEvent ev)
    {
        if (IsAnyTimeRun && !MeetingHud.Instance.AsBoolFast())
            ev.DenyButton("role.timeHunt.meetingButtonText");
    }

    static private RemoteProcess RpcDisperse = new("PlayerDisperse", message => NebulaManager.Instance.StartCoroutine(CoDisperse().WrapToIl2Cpp()));

    static IEnumerator CoDisperse()
    {
        var player = GamePlayer.LocalPlayer;
        var vanillaplayer = player?.VanillaPlayer;

        if (player is null || vanillaplayer is null || player.IsDead) yield break;

        if (Minigame.Instance.AsBoolFast(out var minigame))
        {
            try
            {
                minigame.Close();
                minigame.Close();
            }
            catch (Exception)
            {
            }
        }

        if (vanillaplayer.inVent)
        {
            vanillaplayer.MyPhysics.RpcExitVent(Vent.currentVent.Id);
            vanillaplayer.MyPhysics.ExitAllVents();
        }

        var preLocations = APICompat.AllPreLocations;
        vanillaplayer.NetTransform.RpcSnapTo(preLocations.Select(p => p.Position!.Value).ToArray()[System.Random.Shared.Next(preLocations.Length)]);

        if (vanillaplayer.walkingToVent)
        {
            vanillaplayer.inVent = false;
            Vent.currentVent = null;
            vanillaplayer.moveable = true;
            vanillaplayer.MyPhysics.StopAllCoroutines();
        }
    }

    void AppendTaskPanel(PlayerTaskTextLocalEvent ev)
    {
        if (!IsAnyTimeRun) return;
        Even = !Even;
        foreach (var time in TimeHunt.AllTimes.Where(t => (t.IsRunning && t.CanRunning)))
            ev.AppendText(Language.Translate($"timeHunt.{time.Id}").Replace("%TIME%", Mathn.Ceil(time.Time).ToString()).Color(Even ? VColor.Yellow : VColor.Red));
    }

    public static IEnumerator CoTimeHuntFlash()
    {
        var hud = AmongUsLLImpl.HudManagerInstance;
        while (IntroCutscene.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast()) yield return null;

        var wait = new WaitForSeconds(1f);
        var light = false;
        hud.FullScreen.color = (UColor)new VColor(1f, 0f, 0f, 0.37254903f);

        while (true)
        {
            var screen = hud.FullScreen.gameObject;

            screen.SetActive(!screen.activeSelf);
            hud.lightFlashHandle ??= DualshockLightManager.Instance.AllocateLight();
            hud.lightFlashHandle.color = (UColor)new VColor(1f, 0f, 0f, 1f);
            hud.lightFlashHandle.intensity = 1f;
            light = !light;
            var currentColor = hud.lightFlashHandle.color;
            currentColor.a = light ? 1f : 0f;
            hud.lightFlashHandle.color = currentColor;
            yield return wait;
        }
    }

    public static void StopTimeHuntFlash()
    {
        if (FlashCoroutine == null || !AmongUsLLImpl.TryGetHudManager(out var hud)) return;

        hud.StopCoroutine(FlashCoroutine);
        hud.FullScreen.gameObject.SetActive(false);
        FlashCoroutine = null;
        hud.lightFlashHandle?.Dispose();
        hud.lightFlashHandle = null;
    }
}

public class TimeHuntImpl : TimeHunt
{
    string id;
    float maxTime;
    float time;
    VColor color;
    bool shouldBeDisperse;
    bool isRun;
    Func<bool> canRunning;

    public TimeHuntImpl(string id, float time, Func<bool> canRunning, VColor color, bool shouldBeDisperse)
    {
        this.id = id;
        this.maxTime = this.time = time;
        this.color = color;
        this.shouldBeDisperse = shouldBeDisperse;
        this.canRunning = canRunning;

        if (!TimeHunt.AllTimes.Any(t => t.Id == this.id))
            TimeHunt.AllTimes.Add(this);
    }

    public TimeHuntImpl Start()
    {
        time = maxTime;
        isRun = true;
        NebulaAPI.RunEvent(new TimeHuntStartEvent(ModSingleton<TimeHuntManager>.Instance.MyContainer, this));
        return this;
    }

    public TimeHuntImpl Stop(bool isTimeOver)
    {
        isRun = false;
        NebulaAPI.RunEvent(new TimeHuntEndEvent(ModSingleton<TimeHuntManager>.Instance.MyContainer, this, isTimeOver));
        return this;
    }

    string TimeHunt.Id => this.id;
    float TimeHunt.Time
    {
        get => this.time;
        set => this.time = value;
    }

    VColor TimeHunt.Color => this.color;
    bool TimeHunt.ShouldBeDisperse => shouldBeDisperse;
    bool TimeHunt.CanRunning => canRunning.Invoke();

    bool TimeHunt.IsRunning => isRun;

    TimeHunt TimeHunt.Start() => this.Start();
    TimeHunt TimeHunt.Stop(bool isTimeOver) => this.Stop(isTimeOver);
}

public interface TimeHunt
{
    string Id { get; }

    static List<TimeHunt> AllTimes { get; } = [];

    float Time { get; internal set; }
    VColor Color { get; }

    bool CanRunning { get; }
    bool IsRunning { get; }
    bool ShouldBeDisperse { get; }

    public static TimeHunt Register(string id, float time, Func<bool> canRunning, VColor color, bool shouldBeDisperse = false)
    {
        return new TimeHuntImpl(id, time, canRunning, color, shouldBeDisperse);
    }

    internal TimeHunt Start();
    internal TimeHunt Stop(bool isTimeOver);
}