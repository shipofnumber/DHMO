namespace DHMO.Utilities;

public static class ModAbilityButtonExtensions
{
    public static GameObject? UsesIcon = null;
    public static TextMeshPro? UsesIconText = null;
    public static void SetUsesIcon(this ModAbilityButton button, string text)
    {
        Transform template = HudManager.Instance.AbilityButton.transform.GetChild(2);
        var usesObject = GameObject.Instantiate(template.gameObject);
        usesObject.transform.SetParent(((ModAbilityButtonImpl)button).VanillaButton.gameObject.transform);
        usesObject.transform.localScale = template.localScale;
        usesObject.transform.localPosition = template.localPosition * 1.2f;

        var renderer = usesObject.GetComponent<SpriteRenderer>();
        renderer.color = ((ModAbilityButtonImpl)button).VanillaButton.buttonLabelText.outlineColor;
        var textMesh = usesObject.transform.GetChild(0).GetComponent<TMPro.TextMeshPro>();
        textMesh.text = text;
        UsesIconText = textMesh;
        UsesIcon = usesObject;
    }

    public static void UpdateUsesText(this ModAbilityButton button, string text)
    {
        if (button is null && !UsesIconText) return;
        UsesIconText?.text = text;
    }
    public static void DestroyUsesIcon(this ModAbilityButton button) { if (button is not null && UsesIcon) UsesIcon?.Destroy(); }
}

public static class AddonHelper
{
    public static (int totalAlive, List<GamePlayer> alivePlayers) GetAlivePlayers()
    {
        int totalAlive = 0;
        List<GamePlayer> alivePlayers = [];

        foreach (var player in GameData.Instance.AllPlayers)
        {
            if (player == null || player.Object == null) continue;

            var p = player.Object.ToGamePlayer();
            if (p == null || p.IsDead) continue;

            totalAlive++;
            alivePlayers.Add(p);
        }

        return (totalAlive, alivePlayers);
    }

    public static bool ModAbilityMeetingButton()
    {
        if (AmongUsUtil.InMeeting && MeetingHud.Instance.state is not MeetingHud.VoteStates.Animating and not MeetingHud.VoteStates.Discussion and not MeetingHud.VoteStates.Results and not MeetingHud.VoteStates.Proceeding)
            return true;
        else return false;
    }

    public static void AddCustomChat(PlayerControl sourcePlayer, PlayerControl cosmetics, string title, string chatText, bool censor = true)
    {
        if (sourcePlayer == null || PlayerControl.LocalPlayer == null || string.IsNullOrEmpty(chatText)) return;

        ChatController chatController = UnityEngine.Object.FindObjectOfType<ChatController>();
        NetworkedPlayerInfo sourcePlayerData = sourcePlayer.Data;
        NetworkedPlayerInfo cosmeticsData = cosmetics?.Data ?? sourcePlayerData;
        ChatBubble pooledBubble = chatController.GetPooledBubble();

        try
        {
            pooledBubble.transform.SetParent(chatController.scroller.Inner);
            pooledBubble.transform.localScale = Vector3.one;

            bool isLocalPlayer = sourcePlayer == PlayerControl.LocalPlayer;
            if (isLocalPlayer)
                pooledBubble.SetRight();
            else
                pooledBubble.SetLeft();

            bool didVote = MeetingHud.Instance != null && MeetingHud.Instance.DidVote(sourcePlayer.PlayerId);

            pooledBubble.SetCosmetics(cosmeticsData);
            pooledBubble.SetName(title ?? sourcePlayerData.PlayerName, sourcePlayerData.IsDead, didVote, PlayerNameColor.Get(sourcePlayerData));

            if (censor && AmongUs.Data.DataManager.Settings?.Multiplayer?.CensorChat == true)
                chatText = BlockedWords.CensorWords(chatText, false);

            pooledBubble.SetText(chatText);
            pooledBubble.AlignChildren();

            chatController.AlignAllBubbles();

            if (!chatController.IsOpenOrOpening && chatController.notificationRoutine == null)
                chatController.notificationRoutine = chatController.StartCoroutine(chatController.BounceDot());

            if (!isLocalPlayer && !chatController.IsOpenOrOpening)
            {
                if (SoundManager.Instance != null && chatController.messageSound != null)
                {
                    var soundPlayer = SoundManager.Instance.PlaySound(chatController.messageSound, false, 1f, null);
                    soundPlayer.pitch = 0.5f + sourcePlayer.PlayerId / 15f;
                }

                chatController.chatNotification?.SetUp(sourcePlayer, chatText);
            }
        }
        catch (Exception ex)
        {
            DLog.Log(ex);
            if (pooledBubble != null && chatController.chatBubblePool != null)
                chatController.chatBubblePool.Reclaim(pooledBubble);
        }
    }
}

public static class APICompat
{
    public static T DontDestroy<T>(this T obj) where T : UnityEngine.Object
    {
        obj.hideFlags |= HideFlags.HideAndDontSave;
        return obj.DontDestroyOnLoad();
    }

    public static T DontUnload<T>(this T obj) where T : UnityEngine.Object
    {
        obj.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        return obj;
    }

    public static T DontDestroyOnLoad<T>(this T obj) where T : UnityEngine.Object
    {
        UnityEngine.Object.DontDestroyOnLoad(obj);
        return obj;
    }

    public static void Destroy(this UnityEngine.Object obj) => UnityEngine.Object.Destroy(obj);

    public static void DestroyImmediate(this UnityEngine.Object obj) => UnityEngine.Object.DestroyImmediate(obj);

    static public GamePlayer ToGamePlayer(this PlayerControl player) => GamePlayer.GetPlayer(player.PlayerId)!;

    public static IEnumerable<T> ToEnumerable<T>(this IEnumerator<T> enumerator)
    {
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }
    public static void AddValueV2(this Dictionary<byte, int> self, byte target, int num)
    {
        if (self.TryGetValue(target, out var last))
            self[target] = last + num;
        else
            self[target] = num;
    }
    public static KeyValuePair<byte, int> MaxPairV2(this Dictionary<byte, int> self, out bool tie)
    {
        tie = true;
        KeyValuePair<byte, int> result = new(PlayerVoteArea.SkippedVote, 0);
        foreach (KeyValuePair<byte, int> keyValuePair in self)
        {
            if (keyValuePair.Value > result.Value)
            {
                result = keyValuePair;
                tie = false;
            }
            else if (keyValuePair.Value == result.Value)
            {
                tie = true;
            }
        }
        return result;
    }

    static public FieldInfo? GetPrivateFieldInfo(this object instance, string fieldname)
    {
        return instance.GetType().GetField(fieldname, BindingFlags.Instance | BindingFlags.NonPublic);
    }
    static public T? GetPrivateField<T>(this object instance, string fieldname)
    {
        return (T?)instance.GetPrivateFieldInfo(fieldname)?.GetValue(instance);
    }
    static public void SetPrivateField(this object instance, string fieldname, object value)
    {
        instance.GetPrivateFieldInfo(fieldname)?.SetValue(instance, value);
    }
    static public MethodInfo? GetPrivateMethodInfo(this object instance, string method)
    {
        if (instance is Type)
        {
            return (instance as Type)!.GetPrivateMethodInfoType(method);
        }
        return instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
    }
    static public MethodInfo? GetPrivateMethodInfoType(this Type type, string method)
    {
        return type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
    }
    static public MethodInfo? GetPrivateStaticMethodInfo(this object instance, string method)
    {
        return instance.GetType().GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
    }
    static public MethodInfo? GetPrivateStaticMethodInfoType(this Type type, string method)
    {
        return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
    }
    static public T? CallPrivateMethod<T>(this object instance, string method, params object[] param)
    {
        return (T?)instance.GetPrivateMethodInfo(method)?.Invoke(instance, param);
    }
    static public T? CallPrivateStaticMethod<T>(this object instance, string method, params object[] param)
    {
        return (T?)instance.GetPrivateStaticMethodInfo(method)?.Invoke(instance, param);
    }
    static public Type? GetPrivateChildType(this Type t, string name)
    {
        return t.GetNestedType(name, BindingFlags.NonPublic);
    }
}