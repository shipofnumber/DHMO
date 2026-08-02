namespace DHMO.Roles.Impostor;

public class Overclocker : DefinedSingleAbilityRoleTemplate<Overclocker.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Overclocker() : base("overclocker", VColor.ImpostorColor, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
    [KillCooldown, MinKillCooldown, OutTimeCooldown, MinOutTimeCooldown, ChangeTimePercentage, RandomChangeCD])
    {
    }

    public static readonly IRelativeCooldownConfiguration KillCooldown = NebulaAPI.Configurations.KillConfiguration("options.role.overclocker.killCooldown", CoolDownType.Immediate, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public static readonly FloatConfiguration MinKillCooldown = NebulaAPI.Configurations.Configuration("options.role.overclocker.minKillCooldown", (0f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration OutTimeCooldown = NebulaAPI.Configurations.Configuration("options.role.overclocker.outTimeCooldown", (0f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration MinOutTimeCooldown = NebulaAPI.Configurations.Configuration("options.role.overclocker.minOutTimeCooldown", (0f, 30f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    public static readonly FloatConfiguration ChangeTimePercentage = NebulaAPI.Configurations.Configuration("options.role.overclocker.changeTimePercentage", (0f, 100f, 10f), 30f, FloatConfigurationDecorator.Percentage);
    public static readonly BoolConfiguration RandomChangeCD = NebulaAPI.Configurations.Configuration("options.role.overclocker.randomChangeCooldown", true);

    Citation? HasCitation.Citation => DHMOCitations.DHMO;

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(Snatcher.clockButtonSprite, "role.overclocker.ability.outtime");
    }

    static public Overclocker MyRole = new();

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public ModAbilityButtonImpl? killButton, outTimeButton;
        bool IPlayerAbility.HideKillButton => true;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var killTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeLocalKillablePredicate(p), VColor.ImpostorColor.ToUnityColor(), Nebula.Roles.Impostor.Impostor.CanKillHidingPlayerOption);

                OutTimer killTimer = new OutTimer(-MinKillCooldown, KillCooldown.Cooldown).Register(this);

                killButton = new ModAbilityButtonImpl(isArrangedAsKillButton: true).SetLabel("kill").SetLabelType(ModAbilityButton.LabelType.Impostor).Register(this);
                killButton.Visibility = button => MyPlayer.IsAlive;
                killButton.Availability = button => killTracker.CurrentTarget != null && MyPlayer.CanMove && !MyPlayer.WillDie;
                killButton.CoolDownTimer = killTimer.SetAsKillCoolDown().Start();
                killButton.OnClick = button =>
                {
                    if (killTracker.CurrentTarget == null) return;
                    var cancelable = GameOperatorManager.Instance?.Run(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, killTracker.CurrentTarget));
                    if (!(cancelable?.IsCanceled ?? false))
                    {
                        MyPlayer.MurderPlayer(killTracker.CurrentTarget, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);
                    }

                    if (cancelable?.ResetCooldown ?? false) killTimer.Increase(KillCooldown.Cooldown);
                };

                var killButtoncooldownText = killButton.VanillaButton.cooldownTimerText;
                killButton.cooldownTextColorObserver = new(false, inEffect => killButtoncooldownText.color = new VColor(255, 106, 106).ToUnityColor(), true);

                OutTimer outTimer = new OutTimer(-MinOutTimeCooldown, OutTimeCooldown).Register(this);

                var outTimeTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeStandardPredicate(p));
                outTimeButton = new ModAbilityButtonImpl().SetLabel("overclocker.outtime").SetLabelType(ModAbilityButton.LabelType.Impostor).Register(this);
                outTimeButton.SetSprite(Snatcher.clockButtonSprite.GetSprite());
                outTimeButton.Visibility = button => MyPlayer.IsAlive;
                outTimeButton.Availability = button => outTimeTracker.CurrentTarget != null && MyPlayer.CanMove;
                outTimeButton.RelatedAbility = this;
                outTimeButton.CoolDownTimer = outTimer.SetAsAbilityCoolDown().Start();
                outTimeButton.OnClick = button =>
                {
                    if (outTimeTracker.CurrentTarget == null || outTimeTracker.CurrentTarget is not GamePlayer player) return;
                    var time = Mathn.Abs(outTimer.CurrentTime * (ChangeTimePercentage / 100f));
                    RpcReduceCooldown.Invoke((MyPlayer, player, time));

                    outTimer.Increase(OutTimeCooldown + time);
                };

                var outTimeButtoncooldownText = outTimeButton.VanillaButton.cooldownTimerText;
                outTimeButton.cooldownTextColorObserver = new(false, inEffect => outTimeButtoncooldownText.color = new VColor(255, 106, 106).ToUnityColor(), true);
            }
        }

        public static RemoteProcess<(GamePlayer overclocker, GamePlayer player, float changeTime)> RpcReduceCooldown = new("OverclockerReduceCD", (message, _) =>
        {
            if (message.player.AmOwner)
            {
                var buttons = GameOperatorManager.Instance?.AllOperators.OfType<ModAbilityButtonImpl>().Where(b => b.IsVisible && b.CoolDownTimer != null && b.CoolDownTimer is GameTimer).ToArray();
                if (buttons == null) return;

                if (Overclocker.RandomChangeCD)
                {
                    var randomButton = buttons[System.Random.Shared.Next(buttons.Length)];
                    var timer = randomButton.CoolDownTimer as GameTimer;

                    if (message.overclocker.IsSameSideOf(message.player))
                        OutTimer.CoChangeTime(timer, message.changeTime, true).StartOnScene();
                    else
                    {
                        float changeTime = timer?.CurrentTime - message.changeTime ?? 0f;
                        OutTimer.CoChangeTime(timer, changeTime).StartOnScene();
                    }
                }
                else
                {
                    buttons.Do(b =>
                    {
                        var timer = b.CoolDownTimer as GameTimer;

                        if (message.overclocker.IsSameSideOf(message.player))
                            OutTimer.CoChangeTime(timer, message.changeTime, true).StartOnScene();
                        else
                        {
                            float changeTime = timer?.CurrentTime - message.changeTime ?? 0f;
                            OutTimer.CoChangeTime(timer, changeTime).StartOnScene();
                        }
                    });
                }
            }
        });
    }
}

public class OutTimer : FlexibleLifespan, GameTimer, IGameOperator
{
    private Func<bool>? predicate = null;
    private bool isActive;
    protected float currentTime;
    protected float min, max;

    public float Max => max;

    /// <summary>
    /// タイマーの進行を強制的に止めます。
    /// IsProgressingはfalseを返します。
    /// </summary>
    /// <returns></returns>
    public OutTimer StopForcely() => SetTime(0f);

    public OutTimer Pause()
    {
        isActive = false;
        return this;
    }
    public virtual OutTimer Start(float? time = null)
    {
        isActive = true;
        currentTime = time ?? max;
        return this;
    }
    public OutTimer Resume()
    {
        isActive = true;
        return this;
    }
    public OutTimer Reset()
    {
        currentTime = max;
        return this;
    }
    public OutTimer SetTime(float time)
    {
        currentTime = time;
        return this;
    }
    public OutTimer SetRange(float min, float max)
    {
        if (min > max)
        {
            this.max = min;
            this.min = max;
        }
        else
        {
            this.max = max;
            this.min = min;
        }
        return this;
    }
    public OutTimer Expand(float time)
    {
        this.max += time;
        return this;
    }

    public OutTimer Increase(float time)
    {
        CoIncreaseTime(this, time).StartOnScene();
        return this;
    }

    public static IEnumerator CoIncreaseTime(OutTimer outTimer, float time)
    {
        outTimer.Pause();
        for (int i = 0; i < (int)time; i++)
        {
            outTimer.currentTime += 1f;
            yield return new WaitForSeconds(0.05f);
        }
        outTimer.Resume();
    }

    public static IEnumerator CoChangeTime(GameTimer? timer, float time, bool reduce = false)
    {
        if (!reduce) yield return ManagedEffects.Wait(5f);
        timer?.Pause();

        for (int i = 0; i < (int)time + 5; i++)
        {
            if (reduce)
                timer?.SetTime(timer.CurrentTime - 1f);
            else
                timer?.SetTime(timer.CurrentTime + 1f);

            yield return ManagedEffects.Wait(0.05f);
        }

        timer?.Resume();
    }

    public float CurrentTime { get => currentTime; }
    public virtual float Percentage { get => currentTime > 0f ? currentTime / max : 0f; }
    public bool IsProgressing => CurrentTime >= 0f;

    void Update(UpdateEvent ev)
    {
        if (isActive && (predicate?.Invoke() ?? true))
        {
            float deltaTime = ev.DeltaTime;
            if (AffectedByCooldownEffect)
            {
                float coeff = GamePlayer.LocalPlayer?.Unbox().CalcAttributeVal(PlayerAttributes.CooldownSpeed, true) ?? 1f;
                deltaTime *= coeff;
            }

            currentTime = Mathn.Clamp(currentTime - deltaTime, min, max);
        }
    }

    public OutTimer(float max) : this(float.MinValue, max) { }

    public OutTimer(float min, float max)
    {
        SetRange(min, max);
        Reset();
        Pause();
    }

    GameTimer GameTimer.SetCondition(System.Func<bool> progressWhile) => SetPredicate(progressWhile);
    public OutTimer SetPredicate(Func<bool>? predicate)
    {
        this.predicate = predicate;
        return this;
    }

    public Func<bool>? Predicate => this.predicate;
    public bool AffectedByCooldownEffect = false;

    public OutTimer SetAsKillCoolDown()
    {
        AffectedByCooldownEffect = true;
        return SetPredicate(() => AmongUsLLImpl.LocalPlayer.IsKillTimerEnabled || AmongUsLLImpl.LocalPlayer.ForceKillTimerContinue);
    }

    public OutTimer SetAsAbilityCoolDown()
    {
        AffectedByCooldownEffect = true;
        return SetPredicate(() =>
        {
            var localPlayer = AmongUsLLImpl.LocalPlayer;
            if (localPlayer.CanMove) return true;
            if (localPlayer.inMovingPlat || localPlayer.onLadder) return true;

            var minigame = Minigame.Instance;

            if (minigame &&
            ((bool)minigame.MyNormTask
            || minigame.IsFast<SwitchMinigame>()
            || minigame.IsFast<IDoorMinigame>()
            || minigame.IsFast<VitalsMinigame>()
            || minigame.IsFast<MultistageMinigame>()
            || minigame.IsFast<AutoMultistageMinigame>()
            )) return true;
            return false;
        });
    }

    GameTimer GameTimer.SetAsKillCoolTimer() => SetAsKillCoolDown();

    GameTimer GameTimer.SetAsAbilityTimer() => SetAsAbilityCoolDown();

    IVisualTimer IVisualTimer.Start(float? time) => Start(time);

    GameTimer GameTimer.Pause() => Pause();

    GameTimer GameTimer.Resume() => Resume();

    GameTimer GameTimer.SetRange(float min, float max) => SetRange(min, max);

    GameTimer GameTimer.SetTime(float time) => SetTime(time);

    GameTimer GameTimer.Expand(float time) => Expand(time);
    string IVisualTimer.TimerText => Mathn.CeilToInt(currentTime).ToString();
    internal class TimerCoolDownHelper : IGameOperator
    {
        private OutTimer myTimer;
        public TimerCoolDownHelper(OutTimer timer)
        {
            this.myTimer = timer;
        }

        void ResetVentCoolDownOnTaskPhaseRestart(TaskPhaseRestartEvent ev) => myTimer?.Start();
        void ResetVentCoolDownOnGameStart(GameStartEvent ev) => myTimer?.Start();
    }

    GameTimer GameTimer.ResetsAtTaskPhase()
    {
        new TimerCoolDownHelper(this).Register(this);
        return this;
    }
}