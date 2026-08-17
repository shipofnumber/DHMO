namespace DHMO.Patches;

[HarmonyPatch]
public static class PlayerControlPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter)]
    [HarmonyPriority(1)]
    [HarmonyPostfix]
    public static void PlayerCanMovePatch(PlayerControl __instance, ref bool __result)
    {
        if (Minigame.Instance.AsBoolFast() || AmongUsLLImpl.HudManagerBridge.Chat.IsOpenOrOpening || AmongUsLLImpl.HudManagerInstance.KillOverlay.IsOpen || AmongUsLLImpl.HudManagerInstance.GameMenu.IsOpen) return;
        var modInfo = __instance.GetModInfo();
        if (modInfo != null && !__result && APICompat.IsOutMeeting() && NebulaAPI.CurrentGame != null && (modInfo.Role is Raven.Instance || modInfo.TryGetAbility<LucidDreamer.Ability>(out _)))
            __result = true;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
    [HarmonyPriority(100)]
    [HarmonyPrefix]
    public static bool ReportDeadBodyPatch(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        if (NebulaGameManager.Instance == null || target == null) return true;
        if (ModSingleton<TimeMomentManager>.Instance.IsAnyTimeRun) return false;

        try
        {
            var reporter = __instance.GetModInfo();
            var reported = GamePlayer.GetPlayer(target.PlayerId);

            if (reporter == null || reported == null || reporter.Role.GetAbility<Bomber.Ability>() == null)
                return true;

            if (!(reported.Role.GetAbility<Bait.Ability>() != null || reported.Modifiers.Any(m => m.Modifier.InternalName.Contains("bait")))) return true;

            int reportDelay = NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelay")?.Value ?? 0;
            int dispersion = NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelayDispersion")?.Value ?? 0;

            float totalDelay = reportDelay + dispersion;
            float threshold = Mathn.Min(0.5f, totalDelay + 1f);
            float deathTime = reported.DeathTime ?? NebulaGameManager.Instance.CurrentTime;

            return NebulaGameManager.Instance.HavePassed(deathTime, threshold);
        }
        catch (Exception e)
        {
            DLog.Log(e);
        }

        return true;
    }

    [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation"), HarmonyPrefix]
    public static void PlayerHandleAnimationPatch(PlayerPhysics __instance, ref bool amDead)
    {
        if (!amDead && NebulaAPI.CurrentGame != null)
        {
            PlayerControl playerControl = __instance.myPlayer;
            var player = playerControl.GetModInfo();
            bool outMeet = APICompat.IsOutMeeting();
            bool forceGhost = false;

            if (player?.Role is Raven.Instance) 
                forceGhost = outMeet || Raven.Instance.IsInRavenTime;

            if (forceGhost)
            {
                amDead = true; 
                playerControl.gameObject.layer = LayerExpansion.GetGhostLayer();
            }
            else 
                playerControl.gameObject.layer = LayerExpansion.GetPlayersLayer();
        }
    }
}