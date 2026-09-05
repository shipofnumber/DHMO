namespace DHMO.Patches;

[HarmonyPatch(typeof(LastGameHistory))]
public static class GameHistoryPatch
{
    [HarmonyPatch(nameof(LastGameHistory.SetHistory))]
    static bool Prefix(ref string endCondition)
    {
        if (NebulaAPI.CurrentGame?.GameMode is not IGameModeStandard or IGameModeFreePlay) return true;
        int round = ModSingleton<DGameManager>.Instance.CurrentRound;
        
        var appendText = $" ({Language.Translate("game.end.round").Replace("%ROUND%", round.ToString())})".Color(PlayerModInfo.FakeTaskColor);
        if (!endCondition.EndsWith(appendText))
        {
            endCondition += appendText;
        }

        return true;
    }
}