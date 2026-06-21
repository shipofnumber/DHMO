namespace DHMO.Patches;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal class BaitExplodedPatch
{
    public static bool Prefix(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        try
        {
            if (target == null) return true;
            var reporter = __instance.ToGamePlayer();
            var reported = GamePlayer.GetPlayer(target.PlayerId);
            if (reporter != null && reported != null && reporter.Role.GetAbility<Bomber.Ability>() != null)
            {
                if (reported.Role.GetAbility<Bait.Ability>() != null || reported.Modifiers.Any(r => r.Modifier.InternalName is "baitM"))
                {
                    if (NebulaGameManager.Instance != null)
                    {
                        if (NebulaGameManager.Instance.HavePassed(reported.DeathTime ?? NebulaGameManager.Instance.CurrentTime, Mathf.Min(0.5f, (NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelay")?.Value ?? 0f) + NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelayDispersion")?.Value ?? 0f) + 1f))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            DLog.Log(e);
        }
        return true;
    }
}
