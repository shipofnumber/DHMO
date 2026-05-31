namespace DHMO.Patches;

[HarmonyPatch]
public class PlayerControlPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CanMove), MethodType.Getter), HarmonyPostfix]
    public static void CanMovePatch(ref bool __result)
    {
        if (Minigame.Instance || DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening || DestroyableSingleton<HudManager>.Instance.KillOverlay.IsOpen || DestroyableSingleton<HudManager>.Instance.GameMenu.IsOpen) return;
        if (GamePlayer.LocalPlayer is null) return;
        if (!__result && AddonHelper.IsOutMeeting() && NebulaAPI.CurrentGame != null && (GamePlayer.LocalPlayer.Role is Raven.Instance || GamePlayer.LocalPlayer.TryGetAbility<LucidDreamer.Ability>(out _)))
            __result = true;
    }

    [HarmonyPatch(typeof(PlayerControl), "CmdReportDeadBody"), HarmonyPrefix]
    public static bool ReportDeadBodyPatch() => !Raven.Instance.IsInRavenTime;
}