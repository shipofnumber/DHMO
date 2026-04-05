using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Innersloth.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using static UnityEngine.UIElements.BaseVerticalCollectionView;
using Object = UnityEngine.Object;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace DHMO.Core;

public static class DHMOUtilities
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

    public static void AddCustomChat(PlayerControl sourcePlayer, PlayerControl cosmetics, string title, string chatText, bool censor = true)
    {
        if (sourcePlayer == null || PlayerControl.LocalPlayer == null || string.IsNullOrEmpty(chatText))
        {
            return;
        }

        ChatController chatController = Object.FindObjectOfType<ChatController>();
        if (chatController == null)
        {
            return;
        }

        NetworkedPlayerInfo sourcePlayerData = sourcePlayer.Data;
        if (PlayerControl.LocalPlayer.Data == null || sourcePlayerData == null)
        {
            return;
        }

        NetworkedPlayerInfo cosmeticsData = cosmetics?.Data ?? sourcePlayerData;

        ChatBubble pooledBubble = chatController.GetPooledBubble();
        if (pooledBubble == null)
        {
            return;
        }

        try
        {
            if (chatController.scroller == null || chatController.scroller.Inner == null)
            {
                return;
            }
            pooledBubble.transform.SetParent(chatController.scroller.Inner);
            pooledBubble.transform.localScale = Vector3.one;

            bool isLocalPlayer = sourcePlayer == PlayerControl.LocalPlayer;
            if (isLocalPlayer)
            {
                pooledBubble.SetRight();
            }
            else
            {
                pooledBubble.SetLeft();
            }

            bool didVote = MeetingHud.Instance != null && MeetingHud.Instance.DidVote(sourcePlayer.PlayerId);

            pooledBubble.SetCosmetics(cosmeticsData);
            pooledBubble.SetName(title ?? sourcePlayerData.PlayerName, sourcePlayerData.IsDead, didVote, PlayerNameColor.Get(sourcePlayerData));

            if (censor && AmongUs.Data.DataManager.Settings?.Multiplayer?.CensorChat == true)
            {
                chatText = BlockedWords.CensorWords(chatText, false);
            }
            pooledBubble.SetText(chatText);
            pooledBubble.AlignChildren();

            chatController.AlignAllBubbles();

            if (!chatController.IsOpenOrOpening && chatController.notificationRoutine == null)
            {
                chatController.notificationRoutine = chatController.StartCoroutine(chatController.BounceDot());
            }

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
            {
                chatController.chatBubblePool.Reclaim(pooledBubble);
            }
        }
    }
}