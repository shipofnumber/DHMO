namespace DHMO.Patches;

[HarmonyPatch]
public static class NebulaHudPatch
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void Postfix(HudManager __instance)
    {
        if (GeneralConfigurations.CurrentGameMode == GameModes.AeroGuesser)
        {
            if (NebulaAPI.CurrentGame == null) return;
            if (__instance.MapButton.isActiveAndEnabled)
            {
                __instance.ToggleMapButton(false);
            }
            if (!__instance.Chat.isActiveAndEnabled)
            {
                __instance.Chat.SetVisible(true);
                __instance.Chat.gameObject.SetActive(true);
            }
        }
    }
}
