using InnerNet;

namespace DHMO.Patches;

[HarmonyPatch(typeof(AmongUsClient))]
internal static class AmongUsClientPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CreatePlayer))]
    public static void CreatePlayerPatch(ClientData clientData)
    {
        var joinEvent = new PlayerJoinEvent(clientData);
        NebulaAPI.RunEvent(joinEvent);

        if (!AmongUsClient.Instance.AmHost)
        {
            return;
        }

        if (clientData.Id == AmongUsClient.Instance.HostId)
        {
            return;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    public static void PlayerLeftPatch(ClientData data, DisconnectReasons reason)
    {
        var leftEvent = new PlayerLeaveEvent(data, reason);
        NebulaAPI.RunEvent(leftEvent);
    }
}