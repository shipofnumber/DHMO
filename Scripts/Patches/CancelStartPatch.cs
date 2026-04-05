namespace DHMO.Patches;

[HarmonyPatch]
public static class CancelStartPatch
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ResetStartState))]
    [HarmonyPrefix]
    public static void Prefix(GameStartManager __instance)
    {
        if (__instance.startState is GameStartManager.StartingStates.Countdown)
        {
            SoundManager.Instance.StopSound(__instance.gameStartSound);
            if (AmongUsClient.Instance.AmHost)
            {
                GameManager.Instance.LogicOptions.SyncOptions();
            }
        }
    }
}