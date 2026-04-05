namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public static class ChatCommandPatch
{
    static ChatController chatController = UnityEngine.Object.FindObjectOfType<ChatController>();

    [HarmonyPatch(typeof(ChatController), nameof(chatController.SendChat))]
    [HarmonyPrefix]
    public static bool Prefix(ChatController __instance)
    {
        string text = __instance.freeChatField.textArea.text;
        if (text.Length < 1)
        {
            return true;
        }

        try
        {
            string[] strs = text.Trim().Split(' ', StringSplitOptions.None);
            string command = strs[0];
            switch (command.ToLower())
            {
                case "/lobbyrule":
                case "/lobbyrules":
                case "/rules":
                case "/rule":
                    __instance.freeChatField.Clear();
                    __instance.quickChatMenu.Clear();
                    __instance.quickChatField.Clear();
                    __instance.UpdateChatMode();
                    if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    {
                        var rulesText = PatchManager.GetLobbyRulesText();
                        RpcSendLobbyRulesGlobal.Invoke((PlayerControl.LocalPlayer, rulesText));
                    }
                    else
                    {
                        RpcRequestLobbyRules.Invoke(PlayerControl.LocalPlayer);
                    }
                    return false;

                case "/jailor":
                case "/j":
                case "/jail":
                    __instance.freeChatField.Clear();
                    __instance.quickChatMenu.Clear();
                    __instance.quickChatField.Clear();
                    __instance.UpdateChatMode();
                    if (strs.Length >= 2 && Jailor.HasPrivateChat && MeetingHud.Instance && Jailor.Ability.Jailed != null && !GamePlayer.LocalPlayer!.IsDead && (Jailor.Ability.Jailed == GamePlayer.LocalPlayer || GamePlayer.LocalPlayer.TryGetAbility<Jailor.Ability>(out _)))
                    {
                        RpcSendJailorChat.Invoke((1, PlayerControl.LocalPlayer, string.Join(" ", strs.Skip(1))));
                    }
                    return false;
            }

        }
        catch (Exception e)
        {
            DLog.Log("SendChatError text:" + text + Environment.NewLine + "Error:" + e.ToString());
        }
        return true;
    }

    internal static RemoteProcess<PlayerControl> RpcRequestLobbyRules = new("RequestLobbyRules",
        (requester, _) =>
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                return;
            }

            if (requester == null)
            {
                return;
            }

            var rulesText = PatchManager.GetLobbyRulesText();
            RpcSendLobbyRules?.Invoke((PlayerControl.LocalPlayer, requester, rulesText));
        });

    internal static RemoteProcess<(PlayerControl host, PlayerControl target, string rulesText)> RpcSendLobbyRules = new("SendLobbyRules",
        (message, _) =>
        {
            var rulesText = message.rulesText;

            if (PlayerControl.LocalPlayer != message.target)
            {
                return;
            }

            var title = $"<color=#8BFDFD>{Language.Translate("lobby.rule.title")}</color>";
            var msg = string.IsNullOrWhiteSpace(rulesText) ? Language.Translate("lobby.rule.error") : $"<size=75%>{rulesText}</size>";
            if (message.host is not null && message.target is not null)
            {
                DHMOUtilities.AddCustomChat(message.target, message.host, title, msg);
            }
        });

    public static RemoteProcess<(PlayerControl host, string rulesText)> RpcSendLobbyRulesGlobal = new("SendLobbyRulesGlobal",
        (message, _) =>
    {
        if (!message.host.AmHost()) return;
        var title = $"<color=#8BFDFD>{Language.Translate("lobby.rule.title")}</color>";
        var msg = string.IsNullOrWhiteSpace(message.rulesText) ? Language.Translate("lobby.rule.error") : $"<size=75%>{message.rulesText}</size>";
        DHMOUtilities.AddCustomChat(PlayerControl.LocalPlayer, message.host, title, msg);
    });

    internal static RemoteProcess<(int type, PlayerControl player, string chatText)> RpcSendJailorChat = new("SendMeetingJailChat", (message, __) =>
    {
        if (message.player == null) return;
        var nsp = message.player.ToGamePlayer();
        var jailed = Jailor.Ability.Jailed;
        try
        {
            if (message.type == 1)
            {
                if (NebulaGameManager.Instance!.CanBeSpectator && GamePlayer.LocalPlayer!.IsDead)
                {
                    DHMOUtilities.AddCustomChat(message.player, message.player, message.player.name + Language.Translate("chat.jailor").Color(Jailor.MyRole.UnityColor), message.chatText);
                }
                else if (jailed != null)
                {
                    if (nsp == jailed || nsp.TryGetAbility<Jailor.Ability>(out _) && (GamePlayer.LocalPlayer == jailed || GamePlayer.LocalPlayer!.TryGetAbility<Jailor.Ability>(out _)))
                    {
                        DHMOUtilities.AddCustomChat(message.player, jailed.VanillaPlayer, message.player.name + Language.Translate("chat.jailor").Color(Jailor.MyRole.UnityColor), message.chatText);
                    }
                }
            }
        }
        catch (Exception e)
        {
            DLog.Log(e);
        }
    });
}