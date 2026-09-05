namespace DHMO.Events.Roles;

public class PelicanDevourEvent : Virial.Events.Player.AbstractPlayerEvent
{
    public Virial.Game.Player Pelican { get; init; }
    public bool Cancelled { get; set; }

    public PelicanDevourEvent(Virial.Game.Player devoured, Virial.Game.Player pelican) : base(devoured)
    {
        Pelican = pelican;
    }
}