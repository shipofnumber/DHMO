namespace DHMO.Utilities;

[NebulaRPCHolder]
public static class APICompat
{
    public static void Destroy(this UnityEngine.Object obj) => UnityEngine.Object.Destroy(obj);

    public static void DestroyImmediate(this UnityEngine.Object obj) => UnityEngine.Object.DestroyImmediate(obj);

    public static NebulaPreSpawnLocation[] AllPreLocations
    {
        get
        {
            byte mapId = NebulaAPI.AmongUs.MapId;
            var cand = NebulaPreSpawnLocation.Locations[mapId];

            if (cand.Length == 0) cand = [.. NebulaPreSpawnLocation.Locations[mapId].Where(l => l.VanillaIndex.HasValue)];

            return cand;
        }
    }

    internal static Virial.Game.Player[] AlivePlayers => GamePlayer.AllPlayers.Where(p => p.IsAlive).ToArray();

    public static void AddLobbyNotification(string key, string message, UnityEngine.Color color, Image? image = null, bool playSound = true)
    {
        var notifier = AmongUsLLImpl.HudManagerInstance.Notifier;
        int messageKey = key.Sum(c => c);
        string text = $"<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">{message}</font>";

        bool isClear = false;
        if (color.Compare((UColor)VColor.Clear)) isClear = true;

        if (notifier.lastMessageKey == messageKey && notifier.activeMessages.Count > 0)
            notifier.activeMessages[^1].UpdateMessage(text);
        else
        {
            notifier.lastMessageKey = messageKey;
            LobbyNotificationMessage newMessage = GameObject.Instantiate<LobbyNotificationMessage>(notifier.notificationMessageOrigin, VVector3.Zero, Quaternion.identity, notifier.transform);
            newMessage.ModGameObject(false).LocalPosition = new VVector3(0f, 0f, -2f);

            if (image == WarningImage) newMessage.Icon.ModGameObject(false).LocalScale = new VVector3(1.009f, 1.009f, 1.009f);
            newMessage.SetUp(text, image?.GetSprite() ?? notifier.settingsChangeSprite, isClear ? notifier.settingsChangeColor : color, (Il2CppSystem.Action)(() => notifier.OnMessageDestroy(newMessage)));
            notifier.ShiftMessages();
            notifier.AddMessageToQueue(newMessage);
        }

        if (playSound) AmongUsLLImpl.SoundManagerInstance.PlaySoundImmediate(notifier.settingsChangeSound, false, 1f, 1f, null);
    }

    public static Image? WarningImage { get; internal set; }

    public readonly static RemoteProcess<(string key, string message, VColor color, int sprtieId, bool playSound)> RpcAddLobbyNotification = new("AddLobbyNotificationMod", (message, _) =>
    {
        Image? image = null;
        switch (message.sprtieId)
        {
            case 0: break;
            case 1: image = WarningImage; break;
        }

        AddLobbyNotification(message.key, message.message, (UnityEngine.Color)message.color, image, message.playSound);
    });

    public static GamePlayer? GetClosestPlayer(GamePlayer myPlayer, float detectDistance)
    {
        var myPos = myPlayer.Position;

        var candidates = GamePlayer.AllPlayers
            .Where(p => p != myPlayer && !p.IsDead && !p.IsInvisible)
            .Select(p => new
            { 
                Player = p,
                p.Position,
                Distance = myPos.Distance(p.Position)
            })
            .Where(x => x.Distance <= detectDistance)
            .Where(x =>
            {
                var diff = x.Position - myPos;
                return !NebulaPhysicsHelpers.AnyNonTriggersBetween(
                    myPos,
                    diff.Normalized,
                    diff.Magnitude,
                    Constants.ShipAndObjectsMask,
                    out _);
            })
            .ToList();

        if (candidates.Count == 0)
            return null;

        var minDistance = candidates.Min(x => x.Distance);
        var closest = candidates.Where(x => x.Distance.AlmostEqual(minDistance)).ToList();

        if (closest.Count == 1)
            return closest[0].Player;

        return closest.Where(x => !(NebulaAPI.CurrentGame?.CurrentMap?.AnyShadowsBetween(myPos, x.Position) ?? true)).Select(x => x.Player).FirstOrDefault();
    }

    public static bool AlmostEqual(this double a, double b, double absoluteTolerance = 1e-9, double relativeTolerance = 1e-9)
    {
        if (double.IsNaN(a) || double.IsNaN(b)) return false;
        if (double.IsInfinity(a) || double.IsInfinity(b)) return a == b;

        double diff = Math.Abs(a - b);
        if (diff <= absoluteTolerance) return true;
        return diff <= Math.Max(Math.Abs(a), Math.Abs(b)) * relativeTolerance;
    }

    public static bool AlmostEqual(this float a, float b, float absoluteTolerance = 1e-6f, float relativeTolerance = 1e-6f)
    {
        if (float.IsNaN(a) || float.IsNaN(b)) return false;
        if (float.IsInfinity(a) || float.IsInfinity(b)) return a == b;

        float diff = Math.Abs(a - b);
        if (diff <= absoluteTolerance) return true;
        return diff <= Math.Max(Math.Abs(a), Math.Abs(b)) * relativeTolerance;
    }

    public static bool ModAbilityMeetingButton() => AmongUsUtil.InMeeting && MeetingHud.Instance.state is not MeetingHud.VoteStates.Animating and not MeetingHud.VoteStates.Discussion and not MeetingHud.VoteStates.Results and not MeetingHud.VoteStates.Proceeding;

    public static bool IsOutMeeting() => AmongUsUtil.InMeeting && MeetingHud.Instance.ModGameObject(false).LocalPosition.x > 15;

    public static void AddCustomChat(this ChatController chatController, PlayerControl sourcePlayer, PlayerControl cosmetics, string title, string chatText, bool censor = true)
    {
        var sourcePlayerData = sourcePlayer.Data;
        ChatBubble pooledBubble = chatController.GetPooledBubble();

        try
        {
            var bubbleObj = pooledBubble.ModGameObject();

            bubbleObj.GetUnityTransform().SetParent(chatController.scroller.Inner);
            bubbleObj.LocalScale = VVector3.One;

            if (sourcePlayer.AmOwner)
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

            if (!sourcePlayer.AmOwner && !chatController.IsOpenOrOpening)
            {
                var soundPlayer = AmongUsLLImpl.SoundManagerInstance.PlaySound(chatController.messageSound, false, 1f, null);
                soundPlayer.pitch = 0.5f + sourcePlayer.PlayerId / 15f;

                chatController.chatNotification.SetUp(sourcePlayer, chatText);
            }
        }
        catch (Exception e)
        {
            DLog.Log(e);
            chatController.chatBubblePool.Reclaim(pooledBubble);
        }
    }
}