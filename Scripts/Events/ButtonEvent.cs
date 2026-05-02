namespace DHMO.Events;

public class ButtonVisibleEvent(ModAbilityButtonImpl button) : Virial.Events.Event
{
    public ModAbilityButton Button { get; init; } = button;
    internal ModAbilityButtonImpl ButtonImpl { get; init; } = button;
}
public class ButtonInvisibleEvent(ModAbilityButtonImpl button) : Virial.Events.Event
{
    public ModAbilityButton Button { get; init; } = button;
    internal ModAbilityButtonImpl ButtonImpl { get; init; } = button;
}

public class ButtonAvailableEvent(ModAbilityButtonImpl button) : Virial.Events.Event
{
    public ModAbilityButton Button { get; init; } = button;
    internal ModAbilityButtonImpl ButtonImpl { get; init; } = button;
}

public class ButtonUnavailableEvent(ModAbilityButtonImpl button) : Virial.Events.Event
{
    public ModAbilityButton Button { get; init; } = button;
    internal ModAbilityButtonImpl ButtonImpl { get; init; } = button;
}