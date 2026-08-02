using Virial.Runtime;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class TimeMomentManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static TimeMomentManager() => DIManager.Instance.RegisterModule(() => new TimeMomentManager());

    static public void Preprocess(NebulaPreprocessor preprocessor)
    {
        TimeMoment.Register("raven", Raven.RavenTimeDuration, () =>
        {
            if (!Raven.InvokeRavenTime) return false;
            int aliveCount = AddonHelper.AlivePlayers.Length;

            return aliveCount <= Raven.RavenTimeAliveNum && GamePlayer.AllPlayers.Any(p => p.Role is Raven.Instance && p.IsAlive) && !AmongUsUtil.InMeeting;
        }, Raven.MyRole.Color, Raven.TaskPhaseRestartRavenTimeDisperse);

        TimeMoment.Register("pelican", Pelican.PelicanTimeDuration, () =>
        {
            if (!Pelican.InvokePelicanTime) return false;
            var alivePlayers = AddonHelper.AlivePlayers;
            int totalAlive = alivePlayers.Length;

            var pelicans = alivePlayers.Select(p =>
            {
                p.TryGetRole<Pelican.Instance>(out var pelican);
                return pelican;
            }).NotNull().ToArray();
            int totalDevoured = pelicans.Sum(p => p.DevouringTotal);

            return pelicans.Length > 0 && (totalAlive - totalDevoured) <= Pelican.PelicanTimeAliveNum && !AmongUsUtil.InMeeting;
        }, Pelican.MyRole.Color, Pelican.TaskPhaseRestartPelicanTimeDisperse);
    }

    public TimeMomentManager() => ModSingleton<TimeMomentManager>.Instance = this;

    protected override void OnInjected(Game container) => this.Register(container);

    public bool IsAnyTimeRun { get; set; }
    static Coroutine? FlashCoroutine { get; set; } = null;
    static bool Even { get; set; }

    void OnHudUpdate(GameHudUpdateEvent ev)
    {
        for (int i = 0; i < TimeMoment.AllTimes.Count; i++)
        {
            TimeMoment time = TimeMoment.AllTimes[i];

            if (!time.CanRunning)
                time.Stop();
            else if (!time.IsRunning && time.CanRunning)
                time.Start();

            if (time.IsRunning)
                time.Time -= ev.DeltaTime;
        }

        IsAnyTimeRun = TimeMoment.AllTimes.Any(t => t.IsRunning && t.CanRunning);
        bool hasFlashCoroutine = FlashCoroutine != null;

        if (IsAnyTimeRun && !hasFlashCoroutine)
        {
            var hud = AmongUsLLImpl.HudManagerInstance;

            hud.StopOxyFlash();
            hud.StopReactorFlash();
            FlashCoroutine = hud.StartCoroutine(CoTimeMomentFlash().WrapToIl2Cpp());
        }

        if (!IsAnyTimeRun && hasFlashCoroutine)
            StopTimeMomentFlash();
    }

    [OnlyHost]
    void OnTaskPhaseRestart(TaskPhaseRestartEvent ev)
    {
        if (GeneralConfigurations.SpawnMethodOption.GetValue() != 0) return;

        var times = TimeMoment.AllTimes.Where(t => t.IsRunning && t.CanRunning && t.ShouldBeDisperse).ToArray();
        if (times.Length > 0) RpcDisperse.Invoke();
    }

    void CheckCanPushEmergencyButton(CheckCanPushEmergencyButtonEvent ev)
    {
        if (IsAnyTimeRun && !MeetingHud.Instance.AsBoolFast())
            ev.DenyButton("role.timeMoment.meetingButtonText");
    }

    static private RemoteProcess RpcDisperse = new("PlayerDisperse", message => NebulaManager.Instance.StartCoroutine(CoDisperse().WrapToIl2Cpp()));

    static IEnumerator CoDisperse()
    {
        var player = GamePlayer.LocalPlayer;
        var vanillaplayer = player?.VanillaPlayer;

        if (player is null || vanillaplayer is null || player.IsDead) yield break;

        if (Minigame.Instance.AsBoolFast(out var minigame)) minigame.ForceClose();

        if (vanillaplayer.inVent)
        {
            vanillaplayer.MyPhysics.RpcExitVent(Vent.currentVent.Id);
            vanillaplayer.MyPhysics.ExitAllVents();
        }

        vanillaplayer.NetTransform.RpcSnapTo(NebulaPreSpawnLocation.PreLocations.Select(p => p.Position!.Value).ToArray()[System.Random.Shared.Next(NebulaPreSpawnLocation.PreLocations.Length)]);

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
        foreach (var time in TimeMoment.AllTimes.Where(t => (t.IsRunning && t.CanRunning)))
            ev.AppendText(Language.Translate($"timeMoment.{time.Id}").Replace("%TIME%", Mathn.Ceil(time.Time).ToString()).Color(Even ? VColor.Yellow : VColor.Red));
    }

    public static IEnumerator CoTimeMomentFlash()
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

    public static void StopTimeMomentFlash()
    {
        if (FlashCoroutine == null || !AmongUsLLImpl.TryGetHudManager(out var hud)) return;

        hud.StopCoroutine(FlashCoroutine);
        hud.FullScreen.gameObject.SetActive(false);
        FlashCoroutine = null;
        hud.lightFlashHandle?.Dispose();
        hud.lightFlashHandle = null;
    }
}

public class TimeMomentImpl : TimeMoment
{
    string id;
    float maxTime;
    float time;
    VColor color;
    bool shouldBeDisperse;
    bool isRun;
    Func<bool> canRunning;

    public TimeMomentImpl(string id, float time, Func<bool> canRunning, VColor color, bool shouldBeDisperse)
    {
        this.id = id;
        this.maxTime = this.time = time;
        this.color = color;
        this.shouldBeDisperse = shouldBeDisperse;
        this.canRunning = canRunning;

        if (!TimeMoment.AllTimes.Any(t => t.Id == this.id))
            TimeMoment.AllTimes.Add(this);
    }

    public TimeMomentImpl Start()
    {
        time = maxTime;
        isRun = true;
        NebulaAPI.RunEvent(new TimeMomentStartEvent(this));
        return this;
    }

    bool isTimeOver;

    public TimeMomentImpl Stop()
    {
        isRun = false;
        NebulaAPI.RunEvent(new TimeMomentEndEvent(this, isTimeOver));
        return this;
    }

    string TimeMoment.Id => this.id;
    float TimeMoment.Time
    {
        get => this.time;
        set => this.time = value;
    }

    VColor TimeMoment.Color => this.color;
    bool TimeMoment.ShouldBeDisperse => shouldBeDisperse;
    bool TimeMoment.CanRunning
    {
        get
        {
            if (!canRunning.Invoke())
            {
                this.isTimeOver = false;
                return false;
            }

            if (time <= 0f)
            {
                time = maxTime;
                this.isTimeOver = true;
                return false;
            }

            return true;
        }
    }

    bool TimeMoment.IsRunning => isRun;

    TimeMoment TimeMoment.Start() => this.Start();
    TimeMoment TimeMoment.Stop() => this.Stop();
}

public interface TimeMoment
{
    string Id { get; }

    static List<TimeMoment> AllTimes { get; } = [];

    float Time { get; internal set; }
    VColor Color { get; }

    bool CanRunning { get; }
    bool IsRunning { get; }
    bool ShouldBeDisperse { get; }

    public static TimeMoment Register(string id, float time, Func<bool> canRunning, VColor color, bool shouldBeDisperse = false)
    {
        return new TimeMomentImpl(id, time, canRunning, color, shouldBeDisperse);
    }

    internal TimeMoment Start();
    internal TimeMoment Stop();
}

public class TimeMomentStartEvent(TimeMoment timeMoment) : Virial.Events.Event
{
    public TimeMoment TimeMoment { get; init; } = timeMoment;
}

public class TimeMomentEndEvent(TimeMoment timeMoment, bool isTimeOver) : Virial.Events.Event
{
    public TimeMoment TimeMoment { get; init; } = timeMoment;
    public bool IsTimeOver { get; init; } = isTimeOver;
}