using AmongUs.Data;
using System.Reflection.Emit;

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
        NebulaManager.Instance.StartCoroutine(CoLoadHistory(__instance).WrapToIl2Cpp());
    }

    static IEnumerator CoLoadHistory(HudManager __instance)
    {
        ChatController chatController = __instance.Chat;
        var quickChatButton = chatController.quickChatButton;
        var freeChatField = chatController.freeChatField;

        while (!chatController.AsBoolFast() || !quickChatButton.AsBoolFast() || !freeChatField.AsBoolFast()) yield return null;

        var searchButton = UnityEngine.Object.Instantiate(quickChatButton, quickChatButton.transform.parent);
        var buttonObj = searchButton.ModGameObject();

        var searchField = UnityEngine.Object.Instantiate(freeChatField, freeChatField.transform.parent);
        searchField.name = "SearchField";

        TextMeshPro textPro = searchField.submitButton.text;
        var translator = textPro.GetComponent<TextTranslatorTMP>();
        translator?.Destroy();
        textPro.text = Language.Translate("ui.chat.history.search");

        searchField.gameObject.SetActive(false);

        searchButton.name = "SearchButton";
        VVector3 postion = quickChatButton.transform.localPosition;
        postion.x = -3.94f;

        buttonObj.LocalPosition = postion;

        bool lastShowState = buttonObj.ActiveSelf;
        buttonObj.AddComponent<ScriptBehaviour>().UpdateHandler += () =>
        {
            bool shouldShow = !chatController.quickChatMenu.AsBoolFast() || !chatController.quickChatMenu.gameObject.activeSelf;

            if (shouldShow != lastShowState)
            {
                bool isSearch = ChatSystem.Instance.IsSearch;
                if (!shouldShow && isSearch) ChatSystem.Instance.IsSearch = false;
                buttonObj.SetActive(shouldShow);
                lastShowState = shouldShow;
            }
        };

        searchButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        searchButton.OnClick.AddListener(() =>
        {
            ChatSystem.Instance.IsSearch = !ChatSystem.Instance.IsSearch;
            var isSerach = ChatSystem.Instance.IsSearch;

            searchField.SetVisible(isSerach);
            freeChatField.SetVisible(!isSerach);
            quickChatButton.gameObject.SetActive(!isSerach);
        });

        ChatSystem.Instance.SearchField = searchField;
        ChatSystem.Instance.SearchButton = buttonObj;
        buttonObj.SetActive(false);
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

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.CoOpen))]
    [HarmonyPostfix]
    public static void CoOpenPostfix()
    {
        ChatSystem system = ChatSystem.Instance;
        system.IsSearch = false;

        system.SearchField?.SetVisible(false);
        if (DataManager.Settings.Multiplayer.ChatMode == InnerNet.QuickChatModes.QuickChatOnly) return;
        system.SearchButton?.SetActive(true);
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.CoClose))]
    [HarmonyPostfix]
    public static void CoClosePostfix()
    {
        ChatSystem system = ChatSystem.Instance;
        FreeChatInputField? searchField = system.SearchField;

        system.SearchButton?.SetActive(false);
        searchField?.Unfocus();
        searchField?.ForceKeyboardClose();
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
        GameOperatorManager.Instance?.Subscribe<UpdateEvent>(ev =>
        {
            if (field.characterLimit == ChatSystem.NumOfFreeChatMaxChar) return;

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
        __instance.charCountText.text = string.Format("{0}/{1}", length, textArea.characterLimit);

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