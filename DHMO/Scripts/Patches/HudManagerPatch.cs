namespace DHMO.Patches;

[HarmonyPatch]
public static class HudManagerPatch
{
    [HarmonyPatch(typeof(HudManagerExtension), "UpdateHudContent"), HarmonyPostfix]
    public static void UpdateHudContent()
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;
        if (GeneralConfigurations.CurrentGameMode == GameModes.AeroGuesser)
        {
            if (bridge.MapButton.isActiveAndEnabled) AmongUsLLImpl.HudManagerInstance.ToggleMapButton(false);
            if (!bridge.Chat.isActiveAndEnabled) bridge.Chat.SetVisible(true);
        }

        if (Raven.Instance.IsInRavenTime)
        {
            bridge.ReportButton.ToggleVisible(false);
            bridge.ImpostorVentButton.ToggleVisible(false);
            bridge.SabotageButton.ToggleVisible(false);
        }
    }
}