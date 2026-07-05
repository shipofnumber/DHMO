namespace DHMO.Utilities;

public static class AddonHelper
{
    internal static Assembly? GetAddonAssembly(string addonId) => Nebula.Scripts.AddonScriptManager.scriptAssemblies.FirstOrDefault(a => a.Addon.Id == addonId)?.Assembly;

    public static int GetAlivePlayers(Predicate<GamePlayer>? predicate = null) => GamePlayer.AllPlayers.Where(p => predicate == null || predicate(p)).Count(p => p.IsAlive);

    public static void RemoveAllListeners(this PassiveButton button)
    {
        button.OnClick.RemoveAllListeners();
        button.OnMouseOver.RemoveAllListeners();
        button.OnMouseOut.RemoveAllListeners();
    }

    public static bool ModAbilityMeetingButton()
    {
        if (AmongUsUtil.InMeeting && MeetingHud.Instance.state is not MeetingHud.VoteStates.Animating and not MeetingHud.VoteStates.Discussion and not MeetingHud.VoteStates.Results and not MeetingHud.VoteStates.Proceeding)
            return true;
        else
            return false;
    }

    public static bool IsOutMeeting() => AmongUsUtil.InMeeting && MeetingHud.Instance.ModGameObject(false).LocalPosition.x > 15;

    public static void AddCustomChat(this ChatController chatController, PlayerControl sourcePlayer, PlayerControl cosmetics, string title, string chatText, bool censor = true)
    {
        AmongUsLLImpl.TryGetLocalPlayer(out var localPlayer);

        if (string.IsNullOrEmpty(chatText) || !chatController.AsBoolFast()) return;

        var sourcePlayerData = sourcePlayer.Data;
        ChatBubble pooledBubble = chatController.GetPooledBubble();

        try
        {
            var bubbleObj = pooledBubble.ModGameObject();

            bubbleObj.GetUnityTransform().SetParent(chatController.scroller.Inner);
            bubbleObj.LocalScale = VVector3.One;

            bool isLocalPlayer = sourcePlayer == localPlayer;
            if (isLocalPlayer)
                pooledBubble.SetRight();
            else
                pooledBubble.SetLeft();

            bool didVote = MeetingHud.Instance.AsBoolFast(out var meetingHud) && meetingHud.DidVote(sourcePlayer.PlayerId);

            pooledBubble.SetCosmetics(cosmetics.Data);
            pooledBubble.SetName(title ?? sourcePlayerData.PlayerName, sourcePlayerData.IsDead, didVote, PlayerNameColor.Get(sourcePlayerData));

            if (censor && AmongUs.Data.DataManager.Settings.Multiplayer.CensorChat == true)
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
        catch (Exception e)
        {
            DLog.Log(e);
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

    static public GamePlayer? ToGamePlayer(this PlayerControl player) => GamePlayer.GetPlayer(player.PlayerId);

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