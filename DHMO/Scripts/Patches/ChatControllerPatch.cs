using System.Reflection.Emit;
using AmongUs.Data;
using Virial.Events.Configurations;

namespace DHMO.Patches;

[HarmonyPatch]
public static class ChatControllerPatch
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    [HarmonyPostfix]
    public static void HudManagerStartPostfix(HudManager __instance)
    {
        APICompat.WarningImage = new CacheSpriteLoader(() => __instance.LobbyTimerExtensionUI.gameObject.transform.TryDig("WarningContainer", "Icon")?.GetComponent<SpriteRenderer>().sprite!);

        __instance.Chat.chatBubblePool.poolSize = ChatSystem.NumOfChatHistory;
    }

    static bool ShouldSkipNotification()
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;
        var chat = bridge.Chat;
        if (!chat.AsBoolFast()) return false;

        var chatButton = chat.chatButton;
        if (!chatButton.AsBoolFast()) return false;

        return !chatButton.isActiveAndEnabled;
    }

    [HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.SetUp))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var shipStatusInstanceGetter = AccessTools.PropertyGetter(typeof(ShipStatus), "Instance");
        var skipCheckMethod = AccessTools.Method(typeof(ChatControllerPatch), nameof(ShouldSkipNotification));

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].opcode == OpCodes.Call && codes[i].operand is System.Reflection.MethodInfo method && method == shipStatusInstanceGetter &&
                (codes[i + 1].opcode == OpCodes.Brtrue_S || codes[i + 1].opcode == OpCodes.Brtrue))
            {
                var newCall = new CodeInstruction(OpCodes.Call, skipCheckMethod);

                if (codes[i].labels != null && codes[i].labels.Count > 0)
                {
                    newCall.labels.AddRange(codes[i].labels);
                }

                codes[i] = newCall;
                break;
            }
        }

        return codes.AsEnumerable();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Start))]
    public static void TextBoxPostfix(TextBoxTMP __instance)
    {
        __instance.allowAllCharacters = true;
        __instance.AllowEmail = true;
        __instance.AllowSymbols = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
    public static void InputFieldAwakePostfix(FreeChatInputField __instance)
    {
        var field = __instance.textArea;
        field.characterLimit = ChatSystem.NumOfFreeChatMaxChar;

        __instance.UpdateCharCount();
        GameOperatorManager.Instance?.Subscribe<SharableEntryUpdateEvent>(ev =>
        {
            if (ChatSystem.NumOfFreeChatMaxChar is not IntegerConfigurationImpl integerConfigurationImpl) return;
            if (!__instance.AsBoolFast() || field.characterLimit == ChatSystem.NumOfFreeChatMaxChar || ev.SharableEntry.Id != integerConfigurationImpl.val.Id) return;

            field.characterLimit = ChatSystem.NumOfFreeChatMaxChar;
            if (field.characterLimit < __instance.textArea.text.Length)
            {
                int difference = __instance.textArea.text.Length - field.characterLimit;
                __instance.textArea.text = __instance.textArea.text[..^difference];
            }
            __instance.UpdateCharCount();
        }, new GameObjectLifespan(__instance.gameObject));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
    public static bool UpdateCharCountPrefix(FreeChatInputField __instance)
    {
        var textArea = __instance.textArea;
        var back = __instance.Background;
        ModGameObject backObj = back.ModGameObject(false);
        ModGameObject fieldObj = __instance.ModGameObject(false);

        float height = Math.Max(0.62f, textArea.TextHeight + 0.2f);
        VVector2 size = back.size;
        size.y = height;
        back.size = size;

        float delta = 0.62f - height;
        VVector3 pos = backObj.LocalPosition;
        pos.y = delta / 2f;
        backObj.LocalPosition = pos;

        VVector3 selfPos = fieldObj.LocalPosition;
        selfPos.y = -2.08f - delta;
        fieldObj.LocalPosition = selfPos;

        int length = textArea.text.Length;
        __instance.charCountText.text = $"{length}/{textArea.characterLimit}";

        float percentage = (float)length / textArea.characterLimit;
        if (percentage < 0.5f)
            __instance.charCountText.color = (UColor)VColor.Black; //black
        else if (percentage < 0.75f)
            __instance.charCountText.color = (UColor)VColor.Yellow; //yellow
        else if (percentage < 0.9f)
            __instance.charCountText.color = (UColor)new VColor(1f, 0.5f, 0f); //orange
        else
            __instance.charCountText.color = (UColor)VColor.Red; //red

        return false;
    }
}