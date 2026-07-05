namespace DHMO.Utilities;

public class PrivateChat
{
    public static void RegisterPublicChannel(VColor color, VColor textfieldColor, string chatid, string localizedname, ILifespan lifespan, Func<bool> useChannelPredicate, bool donotSendMessage = false, Action<string>? onSendMessage = null)
    {
        var managerType = AddonHelper.GetAddonAssembly("Plan17ResourcesPlana")?.GetType("Plana.Core.API.PrivateChatManager");
        var instance = managerType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
        var methods = managerType?.GetMethods();

        MethodInfo? registerMethod = null;
        foreach (var method in methods ?? [])
        {
            if (method.Name != "RegisterPublicChannel" || method.GetParameters().Length != 8) continue;
               registerMethod = method;
        }

        try
        {
             registerMethod?.Invoke(instance, [color, textfieldColor, chatid, localizedname, lifespan, useChannelPredicate, donotSendMessage, onSendMessage]);
        }
        catch (Exception e)
        {
            DLog.Log(e);

        }
    }
}