namespace DHMO.Patches;

[HarmonyPatch]
public class ChatPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Start))]
    public static void TextBoxPostfix(TextBoxTMP __instance)
    {
        __instance.allowAllCharacters = true;
        __instance.AllowEmail = true;
        __instance.AllowSymbols = true;
    }
}