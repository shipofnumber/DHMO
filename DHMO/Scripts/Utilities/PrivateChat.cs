namespace DHMO.Utilities;

public static class PrivateChat
{
    public static Assembly? Plan17 { get; private set; }
    public static Type? PrivateChatManagerType { get; private set; }
    public static PropertyInfo? ManagerInstance { get; private set; }
    
    public static void RegisterPublicChannel(VColor color, VColor textfieldColor, string chatid, string localizedname, ILifespan lifespan, Func<bool> useChannelPredicate, bool donotSendMessage = false, Action<string>? onSendMessage = null)
    {
        Plan17 ??= PrivateChat.GetAddonAssembly("Plan17ResourcesPlana");
        if (Plan17 == null) return;
        
        PrivateChatManagerType ??= Plan17.GetType("Plana.Core.API.PrivateChatManager");
        
        ManagerInstance ??= PrivateChatManagerType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var methods = PrivateChatManagerType?.GetMethods();

        MethodInfo? registerMethod = null;
        foreach (var method in methods ?? [])
        {
            if (method.Name != "RegisterPublicChannel" || method.GetParameters().Length != 8) continue;
            registerMethod = method;
        }

        try
        {
            registerMethod?.Invoke(ManagerInstance?.GetValue(null), [color, textfieldColor, chatid, localizedname, lifespan, useChannelPredicate, donotSendMessage, onSendMessage]);
        }
        catch (Exception e)
        {
            DLog.Log(e);
        }
    }

    internal static Assembly? GetAddonAssembly(string addonId) => Nebula.Scripts.AddonScriptManager.ScriptAssemblies.FirstOrDefault(a => a.Addon.Id == addonId)?.Assembly;
}