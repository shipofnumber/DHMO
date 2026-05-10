namespace DHMO.Patches;

[HarmonyPatch]
public static class HudPatch
{
    [HarmonyPatch(typeof(HudManagerExtension), "UpdateHudContent"), HarmonyPostfix]
    public static void UpdateHudContent(HudManager manager)
    {
        var localPlayer = GamePlayer.LocalPlayer;
        if (!PlayerControl.LocalPlayer || localPlayer is null || NebulaAPI.CurrentGame is null) return;

        if (GeneralConfigurations.CurrentGameMode == GameModes.AeroGuesser)
        {
            if (manager.MapButton.isActiveAndEnabled) manager.ToggleMapButton(false);
            if (!manager.Chat.isActiveAndEnabled) manager.Chat.SetVisible(true);
        }

        if (Raven.Instance.IsInRavenTime)
        {
            manager.ReportButton.ToggleVisible(false);
            manager.ImpostorVentButton.ToggleVisible(false);
            manager.SabotageButton.ToggleVisible(false);
        }

        if (AmongUsUtil.InMeeting && manager.TaskPanel.gameObject.activeSelf)
            manager.TaskPanel.gameObject.SetActive(false);
    }
}