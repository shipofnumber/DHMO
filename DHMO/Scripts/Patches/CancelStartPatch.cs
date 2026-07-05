namespace DHMO.Patches;

[HarmonyPatch]
public static class CancelStartPatch
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ResetStartState))]
    [HarmonyPrefix]
    public static void Prefix(GameStartManager __instance)
    {
        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            SoundManager.Instance.StopSound(__instance.gameStartSound);
            if (AmongUsLLImpl.TryGetAmongUsClientInstance(out var client) && client.AmHost)
                AmongUsLLImpl.GameManagerInstance.LogicOptions.SyncOptions();
        }
    }
}