namespace DHMO.Events;

public class RavenTimeStartEvent : Virial.Events.Event
{
}

public class LeaveMeetingEvent(GamePlayer player, DefinedAssignable target) : Virial.Events.Player.AbstractPlayerEvent(player)
{
    public DefinedAssignable? Target { get; init; } = target;
}

public class ReturnMeetingEvent(GamePlayer player, bool killed) : Virial.Events.Player.AbstractPlayerEvent(player)
{
    public bool Killed { get; init; } = killed;
}