namespace DHMO.Patches.Player;

[HarmonyPatch(typeof(PlayerModInfo))]
public static class PlayerModInfoPatch
{
    [HarmonyPatch(nameof(PlayerModInfo.GetStateText))]
    static void Postfix(PlayerModInfo __instance, ref string __result)
    {
        if (NebulaAPI.CurrentGame?.GameMode is not IGameModeStandard or IGameModeFreePlay) return;
        if (__instance.MyState == null || !__instance.IsDead) return;

        var round = ModSingleton<DGameManager>.Instance.GetPlayerDeadRound(__instance).ToString();
        var state = __instance.MyState.Text;
        
        if (round == null) return;

        var appendText = $"({Language.Translate("game.player.deadRound")
            .Replace("%ROUND%", round)})".Color(PlayerModInfo.FakeTaskColor);
        __result = __result.Replace(state, $"{state} {appendText}");
    }
}