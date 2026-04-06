namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public static class ChatCommandPatch
{
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
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