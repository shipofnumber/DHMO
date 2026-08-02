using AmongUs.Data;
using Virial.Events.Configurations;

namespace DHMO.Patches;

[HarmonyPatch]
public static class ChatControllerPatch
{
/*    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
    [HarmonyPostfix]
    public static void ChatControllerAwakePostfix(ChatController __instance)
    {
        var quickChatButton = __instance.quickChatButton;

        var searchHistoryButton = UnityEngine.Object.Instantiate(quickChatButton, quickChatButton.transform.parent);
        searchHistoryButton.name = "SearchHistoryButton";
        VVector3 postion = quickChatButton.transform.localPosition;
        postion.x = -3.94f;

        searchHistoryButton.transform.localPosition = postion;
        searchHistoryButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        searchHistoryButton.OnClick.AddListener(() =>
        {
            ChatSystem.Instance.IsSearch = !ChatSystem.Instance.IsSearch;
            quickChatButton.gameObject.SetActive(!ChatSystem.Instance.IsSearch);
        });

        ChatSystem.Instance.SearchButton = searchHistoryButton.ModGameObject();
        searchHistoryButton.gameObject.SetActive(false);
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.CoOpen))]
    [HarmonyPrefix]
    public static void CoOpenPrefix()
    {
        if (DataManager.Settings.Multiplayer.ChatMode == InnerNet.QuickChatModes.QuickChatOnly) return;
        ChatSystem.Instance.SearchButton?.SetActive(true);
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.CoClose))]
    [HarmonyPrefix]
    public static void CoClosePrefix()
    {
        if (DataManager.Settings.Multiplayer.ChatMode == InnerNet.QuickChatModes.QuickChatOnly) return;
        ChatSystem.Instance.SearchButton?.SetActive(false);
    }*/

    [HarmonyPatch(typeof(ObjectPoolBehavior), nameof(ObjectPoolBehavior.Awake))]
    [HarmonyPrefix]
    public static void ChatPoolAwakePrefix(ObjectPoolBehavior __instance)
    {
        if (__instance.GetInstanceIdFast() != AmongUsLLImpl.HudManagerBridge.Chat.chatBubblePool.GetInstanceIdFast()) return;
        
        __instance.poolSize = ChatSystem.NumOfChatHistory;
        ILifespan objLifespan = new GameObjectLifespan(__instance.gameObject);
        GameOperatorManager.Instance?.Subscribe<UpdateEvent>(ev =>
        {
            if (__instance.poolSize == ChatSystem.NumOfChatHistory) return;
            __instance.StartCoroutine(CoChangeSize().WrapToIl2Cpp());
        }, objLifespan);

        IEnumerator CoChangeSize()
        {
            var size = ChatSystem.NumOfChatHistory;
            __instance.poolSize = size;

            DLog.Log($"ObjectPool: PoolSize changed to {size}");
            var count = size - __instance.activeChildren.Count;
            var list = __instance.inactiveChildren;
            if (count < 0)
            {
                PoolableBehavior[] poolables = [.. __instance.activeChildren.GetFastEnumerator().Skip(Math.Abs(count))];
                foreach (var poolable in poolables)
                {
                    try
                    {
                        __instance.Reclaim(poolable);
                    }
                    catch (Exception ex)
                    {
                        DLog.Log($"Error reclaiming poolable: {ex.Message}");
                    }
                }
            }

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        __instance.CreateOneInactive(__instance.Prefab);
                    }
                    catch (Exception ex)
                    {
                        DLog.Log($"Error creating inactive poolable: {ex.Message}");
                    }
                }
            }

            yield return null;
        }
    }
    

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.GetPooledBubble))]
    [HarmonyPrefix]
    public static bool OnFreeChatSubmitPrefix()
    {
        if (ChatSystem.Instance.IsSearch)
        {
            return false;
        }

        return true;
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

        float height = Math.Max(0.62f, textArea.TextHeight + 0.2f);
        VVector2 size = back.size;
        size.y = height;
        back.size = size;

        float delta = 0.62f - height;
        VVector3 pos = back.transform.localPosition;
        pos.y = delta / 2f;
        back.transform.localPosition = pos;

        VVector3 selfPos = __instance.transform.localPosition;
        selfPos.y = -2.08f - delta;
        __instance.transform.localPosition = selfPos;

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