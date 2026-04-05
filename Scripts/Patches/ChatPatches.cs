using Color = UnityEngine.Color;

namespace DHMO.Patches;

[HarmonyPatch]
public static class ChatPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Start))]
    public static void TextBoxPostfix(TextBoxTMP __instance)
    {
        __instance.allowAllCharacters = true;
        __instance.AllowEmail = true;
        __instance.AllowSymbols = true;
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    public static void UpdatePostfix(ChatController __instance)
    {
        var field = __instance.freeChatField?.textArea;
        if (field == null) return;

        field.characterLimit = 300;

        __instance?.freeChatField?.UpdateCharCount();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
    public static void AwakePostfix(FreeChatInputField __instance)
    {
        if (__instance.charCountText != null && __instance.textArea != null)
        {
            int length = __instance.textArea.text.Length;
            int limit = __instance.textArea.characterLimit;
            __instance.charCountText.text = $"{length}/{limit}";
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
    public static void UpdateCharCountPostfix(FreeChatInputField __instance)
    {
        int length = __instance.textArea.text.Length;
        int limit = __instance.textArea.characterLimit;

        __instance.charCountText.text = $"{length}/{limit}";

        if (length < 175)
        {
            __instance.charCountText.color = Color.black;
            return;
        }

        if (length < 222)
        {
            __instance.charCountText.color = new Color(1f, 1f, 0f, 1f);
            return;
        }

        if (length < 250)
        {
            __instance.charCountText.color = new Color(1f, 0.5f, 0f, 1f);
            return;
        }

        __instance.charCountText.color = Color.red;
    }
}