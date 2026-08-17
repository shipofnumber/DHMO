using Nebula.Roles.Assignment;

namespace DHMO.Patches;

[HarmonyPatch(typeof(RoleTable))]
public class RoleAssignPatch
{
    [HarmonyPrefix] 
    [HarmonyPatch("SetRole")]
    static void SetRolePrefix(RoleTable __instance, byte player, DefinedRole role, int[]? arguments) => __instance.roles.RemoveAll(entry => entry.playerId == player);
}