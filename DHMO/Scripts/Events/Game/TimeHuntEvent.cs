namespace DHMO.Events.Game;

public class TimeHuntStartEvent(Virial.Game.Game game, TimeHunt timeMoment) : Virial.Events.Game.AbstractGameEvent(game)
{
    public TimeHunt TimeHunt { get; init; } = timeMoment;
}

public class TimeHuntEndEvent(Virial.Game.Game game, TimeHunt timeMoment, bool isTimeOver) : Virial.Events.Game.AbstractGameEvent(game)
{
    public TimeHunt TimeHunt { get; init; } = timeMoment;
    public bool IsTimeOver { get; init; } = isTimeOver;
}