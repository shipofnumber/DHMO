namespace DHMO.Patches;

[HarmonyPatch(typeof(LastGameHistory))]
public static class GameHistoryPatch
{
    [HarmonyPatch(nameof(LastGameHistory.SetHistory))]
    static bool Prefix(ref string endCondition)
    {
        int? round = ModSingleton<DGameManager>.Instance.CurrentRound;
        if (round == null) return true;
        
        var appendText = $" ({Language.Translate("game.end.round").Replace("%ROUND%", round.Value.ToString())})".Color(PlayerModInfo.FakeTaskColor);
        if (!endCondition.EndsWith(appendText))
        {
            endCondition += appendText;
        }

        return true;
    }
}