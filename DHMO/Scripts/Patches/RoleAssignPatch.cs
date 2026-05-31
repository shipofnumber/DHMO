using Nebula.Roles.Assignment;

[HarmonyPatch(typeof(RoleTable))]
internal static class RoleAssignPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RoleTable.SetRole))]
    private static void SetRolePrefix(RoleTable __instance, byte player, DefinedRole role, int[]? arguments) => __instance.roles.RemoveAll(entry => entry.playerId == player);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RoleTable.EditRole))]
    private static bool EditRolePrefix(RoleTable __instance, byte player, Func<(DefinedRole role, int[] argument), (DefinedRole role, int[]? argument)> editor)
    {
        int index = __instance.roles.FindIndex(entry => entry.playerId == player);
        if (index != -1)
        {
            var current = __instance.roles[index];
            var (role, argument) = editor.Invoke((current.role, current.arguments));
            __instance.roles[index] = (role, argument ?? [], player);
        }
        return false;
    }
}