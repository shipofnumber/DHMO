namespace DHMO.Patches;

[HarmonyPatch]
public static class PlayerControlPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter)]
    [HarmonyPostfix]
    public static void PlayerCanMovePatch(ref bool __result)
    {
        if (Minigame.Instance || AmongUsLLImpl.HudManagerBridge.Chat.IsOpenOrOpening || AmongUsLLImpl.HudManagerInstance.KillOverlay.IsOpen || AmongUsLLImpl.HudManagerInstance.GameMenu.IsOpen) return;
        if (GamePlayer.LocalPlayer is null) return;
        if (!__result && AddonHelper.IsOutMeeting() && NebulaAPI.CurrentGame != null && (GamePlayer.LocalPlayer.Role is Raven.Instance || GamePlayer.LocalPlayer.TryGetAbility<LucidDreamer.Ability>(out _)))
            __result = true;
    }

    [HarmonyPatch(typeof(PlayerControl), "CmdReportDeadBody")]
    [HarmonyPrefix]
    public static bool ReportDeadBodyPatch(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        if (Raven.Instance.IsInRavenTime) return false;
        try
        {
            if (target == null) return true;
            var reporter = __instance.ToGamePlayer();
            var reported = GamePlayer.GetPlayer(target.PlayerId);
            if (reporter != null && reported != null && reporter.Role.GetAbility<Bomber.Ability>() != null)
            {
                if (reported.Role.GetAbility<Bait.Ability>() != null || reported.Modifiers.Any(r => r.Modifier.InternalName.Contains("bait")))
                {
                    if (NebulaGameManager.Instance != null && NebulaGameManager.Instance.HavePassed(reported.DeathTime ?? NebulaGameManager.Instance.CurrentTime, Mathf.Min(0.5f, (NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelay")?.Value ?? 0f) + NebulaAPI.Configurations.GetSharableVariable<int>("options.role.bait.reportDelayDispersion")?.Value ?? 0f) + 1f))
                        return true;
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

    [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation"), HarmonyPrefix]
    public static void PlayerHandleAnimationPatch(PlayerPhysics __instance, ref bool amDead)
    {
        if (!amDead && NebulaAPI.CurrentGame != null)
        {
            PlayerControl playerControl = __instance.myPlayer;
            var player = playerControl.ToGamePlayer();
            bool outMeet = AddonHelper.IsOutMeeting();
            bool forceGhost = false;

            if (player?.TryGetAbility<LucidDreamer.Ability>(out _) ?? false)
                forceGhost = outMeet;

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