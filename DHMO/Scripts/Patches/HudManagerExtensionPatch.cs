namespace DHMO.Patches;

[HarmonyPatch(typeof(HudManagerExtension), nameof(HudManagerExtension.UpdateHudContent))]
public static class HudManagerExtensionPatch
{
    public static void Postfix()
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;

        if (ModSingleton<TimeMomentManager>.Instance.IsAnyTimeRun)
        {
            bridge.ReportButton.ToggleVisible(false);
            bridge.SabotageButton.ToggleVisible(false);
        }
    }
}