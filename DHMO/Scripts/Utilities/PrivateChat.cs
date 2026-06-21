namespace DHMO.Utilities;

public class PrivateChat
{
    public static void Register(Virial.Color color, Virial.Color textfieldColor, string chatid, string localizedname, ILifespan lifespan, Func<bool> useChannelPredicate, Func<GamePlayer, GamePlayer, bool> canReceive, Action<string>? onSendMessage = null)
    {
        Register(color.ToUnityColor(), textfieldColor.ToUnityColor(), chatid, localizedname, lifespan, useChannelPredicate, canReceive, onSendMessage);
    }

    public static void Register(Virial.Color color, Virial.Color textfieldColor, string chatid, Func<string> localizedname, ILifespan lifespan, Func<bool> useChannelPredicate, Func<GamePlayer, GamePlayer, bool> canReceive, Action<string>? onSendMessage = null)
    {
        Register(color.ToUnityColor(), textfieldColor.ToUnityColor(), chatid, localizedname, lifespan, useChannelPredicate, canReceive, onSendMessage);
    }

    public static void Register(UnityEngine.Color color, UnityEngine.Color textfieldColor, string chatid, string localizedname, ILifespan lifespan, Func<bool> useChannelPredicate, Func<GamePlayer, GamePlayer, bool> canReceive, Action<string>? onSendMessage = null)
    {
        var managerType = AddonHelper.GetAddonAssembly("Plan17ResourcesPlana")?.GetType("Plana.Core.API.PrivateChatManager");
        var instance = managerType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
        var methods = managerType?.GetMethods();

        MethodInfo? registerMethod = null;
        foreach (var method in methods ?? [])
        {
            if (method.Name != "Register") continue;
            if (method.GetParameters()[3].ParameterType == typeof(string))
                registerMethod = method;
        }

        try
        {
            registerMethod?.Invoke(instance, [color, textfieldColor, chatid, localizedname, lifespan, useChannelPredicate, canReceive, onSendMessage]);
        }
        catch (Exception e)
        {
            DLog.Log(e);

        }
    }

    public static void Register(UnityEngine.Color color, UnityEngine.Color textfieldColor, string chatid, Func<string> localizedname, ILifespan lifespan, Func<bool> useChannelPredicate, Func<GamePlayer, GamePlayer, bool> canReceive, Action<string>? onSendMessage = null)
    {
        var managerType = AddonHelper.GetAddonAssembly("Plan17ResourcesPlana")?.GetType("Plana.Core.API.PrivateChatManager");
        var instance = managerType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
        var methods = managerType?.GetMethods();

        MethodInfo? registerMethod = null;
        foreach (var method in methods ?? [])
        {
            if (method.Name != "Register") continue;
            if (method.GetParameters()[3].ParameterType == typeof(Func<string>))
                registerMethod = method;
        }

        try
        {
            registerMethod?.Invoke(instance, [color, textfieldColor, chatid, localizedname, lifespan, useChannelPredicate, canReceive, onSendMessage]);
        }
        catch (Exception e)
        {
            DLog.Log(e);
        }
    }
}

[HarmonyPatch(typeof(ChatController))]
public static class AddChatPatch
{
    static string? Id = string.Empty;
    [HarmonyPatch(nameof(ChatController.AddChat))]
    public static bool Prefix(ChatController __instance, PlayerControl sourcePlayer, string chatText, bool censor = true)
    {
        if (string.IsNullOrEmpty(Id))
        {
            var managerType = AddonHelper.GetAddonAssembly("Plan17ResourcesPlana")?.GetType("Plana.Core.API.PrivateChatManager");
            if (managerType is null) return true;

            var currentChatField = managerType.GetField("currentChat", BindingFlags.Instance | BindingFlags.Public);
            var instance = currentChatField?.GetValue(managerType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null));
            if (instance is null) return true;
            var chatId = currentChatField?.FieldType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance) as string;
            Id = chatId;
        }

        if (string.IsNullOrEmpty(Id) || !Id.Contains("Jailor"))
            return true;

        var sender = sourcePlayer?.ToGamePlayer();
        if (sender == null || !sender.TryGetAbility<Jailor.Ability>(out var jailorAbility))
            return true;

        var targetPlayer = jailorAbility.jailed?.VanillaPlayer;
        if (targetPlayer == null)
            return true;

        string chatPrefix = $"{Language.Translate("role.jailor.name")}{$"({Language.Translate("chat.jailortext")})"}".Color(Jailor.MyRole.UnityColor);
        if (sourcePlayer is not null)
        {
            AddonHelper.AddCustomChat(sourcePlayer, targetPlayer, chatPrefix, chatText, censor);
            return false;
        }
        return true;
    }
}