namespace DHMO.Events.Game;

public class TimeMomentStartEvent(Virial.Game.Game game, TimeMoment timeMoment) : Virial.Events.Game.AbstractGameEvent(game)
{
    public TimeMoment TimeMoment { get; init; } = timeMoment;
}

public class TimeMomentEndEvent(Virial.Game.Game game, TimeMoment timeMoment, bool isTimeOver) : Virial.Events.Game.AbstractGameEvent(game)
{
    public TimeMoment TimeMoment { get; init; } = timeMoment;
    public bool IsTimeOver { get; init; } = isTimeOver;
}