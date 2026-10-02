using Nebula.Bridge;

namespace DHMO.Patches;

[HarmonyPatch]
public static class HudManagerPatch
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    [HarmonyPostfix]
    public static void HudManagerStartPostfix(HudManager __instance)
    {
        if (__instance.LobbyTimerExtensionUI.gameObject.transform.TryFindChild(out var image, "WarningContainer",
                "Icon"))
        {
            APICompat.WarningImage = new CacheSpriteLoader(() => image.GetComponent<SpriteRenderer>().sprite);
        }
        
        Application.targetFrameRate = 120;
        __instance.Chat.chatBubblePool.poolSize = ChatSystem.NumOfChatHistory;
    }

    static HudManagerPatch()
    {
        ClientOption.AllOptions[ClientOption.ClientOptionType.SmallHud].OnValueChanged += () =>
        {
            if (!AmongUsLLImpl.HudManagerInstance.AsBoolFast()) return;

            var value = ClientOption.GetValue(ClientOption.ClientOptionType.SmallHud) == 1;
            ResizeUpperRightButtons(ModSingleton<UpperRightButtons>.Instance, value);
        };
    }

    static VVector3 holderOriginalScale;
    static VVector3 chatOriginalScale;
    static VVector3 settingsOriginalScale;
    static VVector3 mapOriginalScale;
    static VVector3 listOriginalScale;
    static Coroutine? resizeRoutine;

    private const float smallHolderScale = 0.8f;
    private const float uiButtonScale = 0.9427f;
    private const float listButtonScale = 0.2522f;
    private const float animationDuration = 0.1f;

    [HarmonyPatch(typeof(UpperRightButtons), nameof(UpperRightButtons.SetUp))]
    [HarmonyPostfix]
    private static void SetUpPostfix(UpperRightButtons __instance)
    {
        holderOriginalScale = __instance.holder.transform.localScale;

        var hud = AmongUsLLImpl.HudManagerInstance;
        var bridge = AmongUsLLImpl.HudManagerBridge;

        chatOriginalScale = bridge.Chat.chatButton.transform.localScale;
        settingsOriginalScale = hud.SettingsButton.transform.localScale;
        mapOriginalScale = bridge.MapButton.transform.localScale;

        if (FriendsListManager.InstanceExists && !TutorialManager.InstanceExists)
        {
            listOriginalScale = FriendsListManager.Instance.FriendsListButton.transform.GetChild(0).localScale;
        }

        ResizeUpperRightButtons(__instance, ClientOption.GetValue(ClientOption.ClientOptionType.SmallHud) == 1);
    }

    private static void ResizeUpperRightButtons(UpperRightButtons upperRightButtons, bool small)
    {
        if (!AmongUsLLImpl.HudManagerInstance.AsBoolFast()) return;

        var holderTf = upperRightButtons.holder.transform;
        var chatTf = AmongUsLLImpl.HudManagerBridge.Chat.chatButton.transform;
        var settingsTf = AmongUsLLImpl.HudManagerInstance.SettingsButton.transform;
        var mapTf = AmongUsLLImpl.HudManagerBridge.MapButton.transform;

        upperRightButtons.holder.DistanceFromEdge = small
            ? new VVector3(0.4f, 0.4f, -400f)
            : new VVector3(0.48f, 0.51f, -400f);

        var targetHolder = small ? new VVector3(smallHolderScale, smallHolderScale, holderTf.localScale.z) : holderOriginalScale;
        var targetChat = small ? new VVector3(uiButtonScale, uiButtonScale, chatTf.localScale.z) : chatOriginalScale;
        var targetSettings = small ? new VVector3(uiButtonScale, uiButtonScale, settingsTf.localScale.z) : settingsOriginalScale;
        var targetMap = small ? new VVector3(uiButtonScale, uiButtonScale, mapTf.localScale.z) : mapOriginalScale;

        Transform? listTf = null;
        var targetList = VVector3.One;
        if (FriendsListManager.InstanceExists && !TutorialManager.InstanceExists)
        {
            listTf = FriendsListManager.Instance.FriendsListButton.transform.GetChild(0);
            targetList = small ? new VVector3(listButtonScale, listButtonScale, listTf.localScale.z) : listOriginalScale;
        }

        if (resizeRoutine != null) NebulaManager.Instance.StopCoroutine(resizeRoutine);
        resizeRoutine = NebulaManager.Instance.StartCoroutine(AnimateScale(holderTf, targetHolder,
            chatTf, targetChat,
            settingsTf, targetSettings,
            mapTf, targetMap,
            listTf, targetList).WrapToIl2Cpp());
    }

    private static IEnumerator AnimateScale(Transform holderTf, VVector3 targetHolder,
        Transform chatTf, VVector3 targetChat,
        Transform settingsTf, VVector3 targetSettings,
        Transform mapTf, VVector3 targetMap,
        Transform? listTf, VVector3 targetList)
    {
        var startHolder = holderTf.localScale;
        var startChat = chatTf.localScale;
        var startSettings = settingsTf.localScale;
        var startMap = mapTf.localScale;
        var startList = listTf != null ? listTf.localScale : UVector3.one;

        for (float t = 0f; t < animationDuration; t += Time.deltaTime)
        {
            var k = Mathn.SmoothStep(0f, 1f, t / animationDuration);
            holderTf.localScale = VVector3.Lerp(startHolder, targetHolder, k);
            chatTf.localScale = VVector3.Lerp(startChat, targetChat, k);
            settingsTf.localScale = VVector3.Lerp(startSettings, targetSettings, k);
            mapTf.localScale = VVector3.Lerp(startMap, targetMap, k);
            listTf?.localScale = VVector3.Lerp(startList, targetList, k);

            yield return null;
        }

        holderTf.localScale = targetHolder;
        chatTf.localScale = targetChat;
        settingsTf.localScale = targetSettings;
        mapTf.localScale = targetMap;
        listTf?.localScale = targetList;

        resizeRoutine = null;
    }

    [HarmonyPatch(typeof(HudManagerExtension), nameof(HudManagerExtension.UpdateHudContent))]
    [HarmonyPostfix]
    public static void UpdateHudContentPostfix()
    {
        var bridge = AmongUsLLImpl.HudManagerBridge;

        if (ModSingleton<TimeHuntManager>.Instance.IsAnyTimeRun)
        {
            bridge.ReportButton.ToggleVisible(false);
        }
    }
}