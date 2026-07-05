namespace DHMO.Patches;

[HarmonyPatch]
public static class HudManagerPatch
{
    [HarmonyPatch(typeof(HudManagerExtension), "UpdateHudContent"), HarmonyPostfix]
    public static void UpdateHudContent()
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;

        if (Raven.Instance.IsInRavenTime)
        {
            bridge.ReportButton.ToggleVisible(false);
            bridge.ImpostorVentButton.ToggleVisible(false);
            bridge.SabotageButton.ToggleVisible(false);
        }
    }
}