namespace DHMO.Patches;

[HarmonyPatch(typeof(SpectatorsAbility))]
public static class SpectatorsAbilityPatch
{
    [HarmonyPatch(nameof(SpectatorsAbility.AvailableTargets), MethodType.Getter)]
    static void Postfix(SpectatorsAbility __instance, ref GamePlayer[] __result)
    {
        var targets = __result.Where(p => !p.IsDevoured);
        
        __result = targets.ToArray();
    }
}