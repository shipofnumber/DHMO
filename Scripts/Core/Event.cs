using InnerNet;

namespace DHMO.Core;

public class RavenTimeStartEvent : Virial.Events.Event
{
    internal RavenTimeStartEvent() { }
}

public class LeaveMeetingEvent : Virial.Events.Event
{
    internal LeaveMeetingEvent() { }
}

public class ReturnMeetingEvent : Virial.Events.Event
{
    internal ReturnMeetingEvent() { }
}
public class BombPassEvent : AbstractPlayerEvent
{
    public GamePlayer? Passer => Player;
    public GamePlayer? Recipient { get; init; }
    public float BombTimer { get; init; }
    internal BombPassEvent(GamePlayer passer, GamePlayer? recipient, float bombtimer) : base(passer)
    {
        this.Recipient = recipient;
        this.BombTimer = bombtimer;
    }
}

public class PlayerJoinEvent(ClientData data) : Virial.Events.Event
{
    public ClientData ClientData { get; init; } = data;
}
public class PlayerLeaveEvent(ClientData data, DisconnectReasons reason) : Virial.Events.Event
{
    public ClientData ClientData { get; init; } = data;

    public DisconnectReasons Reason { get; init; } = reason;
}