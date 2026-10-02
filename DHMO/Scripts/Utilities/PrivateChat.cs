namespace DHMO.Utilities;

public static class PrivateChat
{
    public static Assembly? Plan17 { get; private set; }
    public static Type? PrivateChatManagerType { get; private set; }
    public static PropertyInfo? ManagerInstance { get; private set; }

    private static MethodInfo? registerChannelMethod;
    private static MethodInfo? registerPublicChannelMethod;

    private static readonly Type[]? RegisterChannelSignature =
        typeof(PrivateChat).GetMethod(nameof(RegisterChannel))?.GetParameters().Select(p => p.ParameterType).ToArray();

    public static void RegisterChannel(VColor color, VColor textfieldColor, string chatId, Func<string> localizedName, ILifespan lifespan, Func<bool> useChannelPredicate, Func<GamePlayer, GamePlayer, bool> canReceive, Action<string>? onSendMessage = null)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return;
        }

        if (!TryResolveManager()) return;

        if (registerChannelMethod == null)
        {
            registerChannelMethod = FindMethod(nameof(RegisterChannel), 8, RegisterChannelSignature);
            if (registerChannelMethod == null) return;
        }

        InvokeRegister(registerChannelMethod, [color, textfieldColor, chatId, localizedName, lifespan, useChannelPredicate, canReceive, onSendMessage]);
    }

    public static void RegisterPublicChannel(VColor color, VColor textfieldColor, string chatId, string localizedName, ILifespan lifespan, Func<bool> useChannelPredicate, bool donotSendMessage = false, Action<string>? onSendMessage = null)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrEmpty(localizedName))
        {
            return;
        }

        if (!TryResolveManager()) return;

        if (registerPublicChannelMethod == null)
        {
            registerPublicChannelMethod = FindMethod(nameof(RegisterPublicChannel), 8, null);
            if (registerPublicChannelMethod == null) return;
        }

        InvokeRegister(registerPublicChannelMethod, [color, textfieldColor, chatId, localizedName, lifespan, useChannelPredicate, donotSendMessage, onSendMessage]);
    }

    private static bool TryResolveManager()
    {
        Plan17 ??= GetAddonAssembly("Plan17ResourcesPlana");
        if (Plan17 == null)
        {
            return false;
        }

        PrivateChatManagerType ??= Plan17.GetType("Plana.Core.API.PrivateChatManager");
        if (PrivateChatManagerType == null)
        {
            return false;
        }

        ManagerInstance ??= PrivateChatManagerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return ManagerInstance != null;
    }

    private static MethodInfo? FindMethod(string methodName, int parameterCount, Type[]? expectedParameterTypes)
    {
        if (expectedParameterTypes != null && expectedParameterTypes.Length != parameterCount)
        {
            return null;
        }

        foreach (var method in PrivateChatManagerType!.GetMethods())
        {
            if (method.Name != methodName || method.GetParameters().Length != parameterCount) continue;

            if (expectedParameterTypes != null)
            {
                var parameters = method.GetParameters();
                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != expectedParameterTypes[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (!match) continue;
            }

            return method;
        }

        return null;
    }

    private static void InvokeRegister(MethodInfo method, object?[] args)
    {
        var manager = ManagerInstance?.GetValue(null);
        if (manager == null)
        {
            return;
        }

        try
        {
            method.Invoke(manager, args);
        }
        catch (Exception e)
        {
            throw;
        }
    }

    internal static Assembly? GetAddonAssembly(string addonId) => Nebula.Scripts.AddonScriptManager.ScriptAssemblies.FirstOrDefault(a => a.Addon.Id == addonId)?.Assembly;
}