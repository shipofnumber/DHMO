using Il2CppInterop.Runtime.Injection;

namespace DHMO.Behaviour;

public class SelectPlayerMenu : AbstractPlayerMenuMinigame
{
    static SelectPlayerMenu() => ClassInjector.RegisterTypeInIl2Cpp<SelectPlayerMenu>();
    public SelectPlayerMenu(nint ptr) : base(ptr) { }
    public SelectPlayerMenu() : base(ClassInjector.DerivedConstructorPointer<SelectPlayerMenu>()) => ClassInjector.DerivedConstructorBody(this);
}