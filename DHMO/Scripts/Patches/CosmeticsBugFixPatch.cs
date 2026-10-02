using Nebula.Modules.Cosmetics;

namespace DHMO.Patches;

[HarmonyPatch]
public static class CosmeticsBugFixPatch
{
    [HarmonyPatch(typeof(SetVisorMaterialPatch), nameof(SetVisorMaterialPatch.Postfix))]
    [HarmonyPrefix]
    public static bool VisorLayerUpdatePrefix([HarmonyArgument(0)] VisorLayer layer)
    {
        return layer.AsBoolFast();
    }

    [HarmonyPatch(typeof(HatParentUpdateMaterialPatch), nameof(HatParentUpdateMaterialPatch.Prefix))]
    [HarmonyPrefix]
    public static bool HatParentUpdatePrefix([HarmonyArgument(0)] HatParent parent)
    {
        return parent.AsBoolFast();
    }
}