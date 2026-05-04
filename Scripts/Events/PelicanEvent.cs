namespace DHMO.Events;

public class PelicanDevourEvent(GamePlayer player, GamePlayer devoured) : Virial.Events.Player.AbstractPlayerEvent(player)
{
    public GamePlayer Devoured { get; init; } = devoured;
}