using Nebula.Roles.Assignment;

namespace DHMO.Patches;

[HarmonyPatch(typeof(RoleTable))]
internal class RoleAssignPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RoleTable.SetRole))]
    private static void SetRolePrefix(RoleTable __instance, byte player, DefinedRole role, int[]? arguments) => __instance.roles.RemoveAll(entry => entry.playerId == player);
}