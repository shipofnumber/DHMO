namespace DHMO.Patches;

[HarmonyPatch(typeof(LastGameHistory))]
public static class GameHistoryPatch
{
    [HarmonyPatch(nameof(LastGameHistory.SetHistory))]
    static void Prefix(ref string endCondition)
    {
        if (NebulaAPI.CurrentGame?.GameMode is not IGameModeStandard) return;
        int round = ModSingleton<DGameManager>.Instance.CurrentRound;
        
        var appendText = $"({Language.Translate("game.end.round").Replace("%ROUND%", round.ToString())})".Sized(75).Color(PlayerModInfo.FakeTaskColor);
        if (!endCondition.EndsWith(appendText))
        {
            endCondition += appendText;
        }
    }
}