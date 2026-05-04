namespace DHMO.Patches;

[HarmonyPatch]
public class PlayerControlPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter), HarmonyPostfix]
    public static void CanMovePatch(ref bool __result)
    {
        if (GamePlayer.LocalPlayer is null) return;
        if (!__result && Raven.Instance.IsOutMeeting() && NebulaAPI.CurrentGame != null && (GamePlayer.LocalPlayer.Role is Raven.Instance || GamePlayer.LocalPlayer.TryGetAbility<LucidDreamer.Ability>(out _)))
            __result = true;
    }

    [HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation"), HarmonyPrefix]
    public static void PlayerHandleAnimationPatch(PlayerPhysics __instance, ref bool amDead)
    {
        var gamePlayer = __instance.myPlayer.ToGamePlayer();
        if (!amDead && (Raven.Instance.IsOutMeeting() || Raven.Instance.IsInRavenTime) && NebulaAPI.CurrentGame != null && (gamePlayer.Role is Raven.Instance || gamePlayer.TryGetAbility<LucidDreamer.Ability>(out _)))
        {
            amDead = true;
            __instance.myPlayer.gameObject.layer = LayerExpansion.GetGhostLayer();
            __instance.myPlayer.cosmetics.SetGhost();
        }
    }

    [HarmonyPatch(typeof(PlayerControl), "CmdReportDeadBody"), HarmonyPrefix]
    public static bool ReportDeadBodyPatch() => !Raven.Instance.IsInRavenTime;
}