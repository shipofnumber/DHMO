namespace DHMO.Patches;

[HarmonyPatch]
public class ModAbilityButtonPatch
{
    private static bool wasVisible = false;
    private static bool wasAvailable = false;

    [HarmonyPatch(typeof(ModAbilityButtonImpl), nameof(ModAbilityButtonImpl.UpdateVisibility))]
    [HarmonyPostfix]
    public static void Postfix(ModAbilityButtonImpl __instance)
    {
        bool isVisible = __instance.IsVisible;
        bool isAvailable = __instance.IsAvailable;
        if (isVisible && !wasVisible) GameOperatorManager.Instance?.Run(new ButtonVisibleEvent(__instance));
        else if (!isVisible && wasVisible) GameOperatorManager.Instance?.Run(new ButtonInvisibleEvent(__instance));

        if (isAvailable && !wasAvailable) GameOperatorManager.Instance?.Run(new ButtonAvailableEvent(__instance));
        else if (!isAvailable && wasAvailable) GameOperatorManager.Instance?.Run(new ButtonUnavailableEvent(__instance));

        wasVisible = isVisible;
        wasAvailable = isAvailable;
    }
}