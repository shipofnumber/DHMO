namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class TimeSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static TimeSystem() => DIManager.Instance.RegisterModule(() => new TimeSystem());
    public TimeSystem() => ModSingleton<TimeSystem>.Instance = this;

    protected override void OnInjected(Game container)
    {
        this.Register(container);
        TimeMoment.Register("raven", Raven.RavenTimeDuration, () =>
        {
            if (!Raven.RavenTimeOption) return false;
            var aliveCount = AddonHelper.GetAlivePlayers();

            return aliveCount <= Raven.RavenTimeAliveNum && GamePlayer.AllPlayers.Any(p => p.Role is Raven.Instance && p.IsAlive) && !AmongUsUtil.InMeeting;
        });
    }

    static bool IsAnyTimeRun { get; set; } = false;
    static Coroutine? FlashCoroutine { get; set; } = null;
    static bool Even { get; set; }

    void OnHudUpdate(GameHudUpdateEvent ev)
    {
        foreach (var time in TimeMoment.AllTimes)
        {
            if (time.CurrentTimer.isActive && !time.CurrentTimer.IsProgressing)
            {
                time.Stop();
                continue;
            }

            bool isRunning = time.IsRunning;
            bool canRun = time.CanRunning.Invoke();

            if (!isRunning && canRun)
                time.Start();
            else if (isRunning && !canRun)
                time.Stop();
        }
        IsAnyTimeRun = TimeMoment.AllTimes.Any(t => t.IsRunning && t.CanRunning.Invoke());

        var hud = AmongUsLLImpl.HudManagerInstance;
        bool hasFlashCoroutine = FlashCoroutine != null;

        if (IsAnyTimeRun && !hasFlashCoroutine)
        {
            hud.StopOxyFlash();
            hud.StopReactorFlash();
            FlashCoroutine = AmongUsLLImpl.HudManagerInstance.StartCoroutine(CoTimeMomentFlash().WrapToIl2Cpp());
        }
        else if (!IsAnyTimeRun && hasFlashCoroutine)
            StopTimeMomentFlash();
    }

    void AppendTaskPanel(PlayerTaskTextLocalEvent ev)
    {
        if (!IsAnyTimeRun) return;
        Even = !Even;
        foreach (var time in TimeMoment.AllTimes.Where(t => (t.IsRunning && t.CanRunning.Invoke())))
            ev.AppendText(Language.Translate($"timeMoment.{time.Id}").Replace("%TIME%", Mathn.Ceil(time.CurrentTimer.CurrentTime).ToString()).Color(Even ? VColor.Yellow : VColor.Red));
    }

    public static IEnumerator CoTimeMomentFlash()
    {
        var hud = AmongUsLLImpl.HudManagerInstance;
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
        if (FlashCoroutine == null) return;
        AmongUsLLImpl.TryGetHudManager(out var hud);
        hud.StopCoroutine(FlashCoroutine);
        hud.FullScreen.gameObject.SetActive(false);
        FlashCoroutine = null;
        hud.lightFlashHandle?.Dispose();
        hud.lightFlashHandle = null;
    }
}

public class TimeMomentImp1 : DependentLifespan, IGameOperator, TimeMoment
{
    string id {  get; set; }
    TimerImpl currentTimer { get; set; }
    Func<bool> canRunning { get; set; }

    public TimeMomentImp1(string id, float time, Func<bool> canRunning)
    {
        if (NebulaAPI.CurrentGame != null) this.Register(NebulaAPI.CurrentGame);

        this.id = id;
        this.currentTimer = new TimerImpl(time).Register(this);
        this.canRunning = canRunning;

        if (!TimeMoment.AllTimes.Any(t => t.Id == this.id))
            TimeMoment.AllTimes.Add(this);
    }

    public TimeMomentImp1 Start(bool reset = true, float? time = null)
    {
        if (reset)
            currentTimer.Reset().Start();
        else
        {
            if (time != null)
                currentTimer.Start(time);
        }
        NebulaAPI.RunEvent(new TimeMomentStartEvent(this));

        return this;
    }

    public TimeMomentImp1 Stop()
    {
        currentTimer.Pause();
        NebulaAPI.RunEvent(new TimeMomentEndEvent(this));
        return this;
    }

    void IGameOperator.OnReleased()
    {
        this.Stop();
        TimeMoment.AllTimes.Remove(this);
    }

    string TimeMoment.Id => this.id;
    TimerImpl TimeMoment.CurrentTimer => this.currentTimer;
    Func<bool> TimeMoment.CanRunning => canRunning;
    bool TimeMoment.IsRunning => this.currentTimer.isActive && this.currentTimer.IsProgressing;

    TimeMoment TimeMoment.Start(bool reset, float? time) => this.Start(reset, time);
    TimeMoment TimeMoment.Stop() => this.Stop();
}

public interface TimeMoment
{
    public string Id { get; }

    public static List<TimeMoment> AllTimes { get; } = [];

    public TimerImpl CurrentTimer { get; }

    Func<bool> CanRunning { get; }
    public bool IsRunning { get; }

    public static TimeMoment Register(string id, float time, Func<bool> canRunning)
    {
        return new TimeMomentImp1(id, time, canRunning);
    }

    public TimeMoment Start(bool reset = true, float? time = null);
    public TimeMoment Stop();
}

public class TimeMomentStartEvent(TimeMoment timeMoment) : Virial.Events.Event
{
    public TimeMoment TimeMoment { get; init; } = timeMoment;
}

public class TimeMomentEndEvent(TimeMoment timeMoment) : Virial.Events.Event
{
    public TimeMoment TimeMoment { get; init; } = timeMoment;
}