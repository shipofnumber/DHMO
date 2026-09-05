namespace DHMO.Events.Roles;

public class PlayerSpeculateEvent : Virial.Events.Player.AbstractPlayerEvent
{
    public bool Correct { get; init; }
    public bool NoSpeculated { get; init; }

    public PlayerSpeculateEvent(Virial.Game.Player journalist, bool correct, bool noSpeculated) : base(journalist)
    {
        this.Correct = correct;
        this.NoSpeculated = noSpeculated;
    }
}