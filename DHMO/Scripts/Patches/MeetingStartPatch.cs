using Random = UnityEngine.Random;

namespace DHMO.Patches;

[HarmonyPatch]
public static class MeetingStartPatch
{
    [HarmonyPatch(typeof(MeetingHudExtension), nameof(MeetingHudExtension.ModCoStartMeeting)), HarmonyPrefix]
    public static bool ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo deadBody, int reportType, ref IEnumerator __result)
    {
        try
        {
            __result = ModCoStartMeeting(reporter, deadBody, reportType);
            return false;
        }
        catch (Exception e)
        {
            DLog.Log(e);
            return true;
        }
    }

    private static IEnumerator ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, int reportType)
    {
        while (!MeetingHud.Instance) yield return null;

        MeetingRoomManager.Instance.RemoveSelf();
        AmongUsLLImpl.HudManagerInstance.InitMap();
        MapBehaviour.Instance.SetPreMeetingPosition(AmongUsLLImpl.LocalPlayer.transform.position, false);
        foreach (var player in GamePlayer.AllPlayers)
        {
            try
            {
                if (player.VanillaPlayer) ModResetForMeeting(player.VanillaPlayer, false);
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }

        if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
        if (Minigame.Instance) Minigame.Instance.ForceClose();
        AmongUsLLImpl.ShipStatusInstance.OnMeetingCalled();
        KillAnimation.SetMovement(reporter, true);
        GameData.TimeLastMeetingStarted = Time.realtimeSinceStartup;

        var meetingHud = MeetingHud.Instance;
        meetingHud.StartCoroutine(MeetingHudExtension.ModCoMeetingHudIntro(meetingHud, reporter, deadBody, (MeetingHudExtension.ReportType)reportType).WrapToIl2Cpp());
        yield break;
    }

    public static void ModResetForMeeting(PlayerControl player, bool spawn = true)
    {
        if (!player.GetComponent<DummyBehaviour>().enabled)
        {
            player.MyPhysics.ExitAllVents();
            if (spawn)
                AmongUsLLImpl.ShipStatusInstance.SpawnPlayer(player, GameData.Instance.PlayerCount, false);
        }
        player.RemoveProtection();
        player.NetTransform.enabled = true;
        player.MyPhysics.ResetMoveState(true);
        for (int i = 0; i < player.currentRoleAnimations.Count; i++)
        {
            if (player.currentRoleAnimations[i] != null && player.currentRoleAnimations[i].gameObject != null)
            {
                player.currentRoleAnimations[i].gameObject.Destroy();
            }
        }
        player.inMovingPlat = false;
        player.isKilling = false;
        player.currentRoleAnimations.Clear();
        if (player.cosmetics.CurrentPet != null)
        {
            if (player.cosmetics.petHiddenByViper)
            {
                player.cosmetics.TogglePet(true);
                VVector2 vector = player.transform.position;
                if (AmongUsLLImpl.ShipStatusInstance is AirshipStatus)
                {
                    List<VVector2> list =
                    [
                        new VVector2(8.2f, 15.2f),
                        new VVector2(8.25f, 15.9f),
                        new VVector2(8.2f, 14.3f),
                        new VVector2(11f, 14.3f),
                        new VVector2(9.8f, 14.3f),
                        new VVector2(13f, 14.3f)
                    ];
                    vector = list[Random.Range(0, list.Count)];
                }
                player.cosmetics.CurrentPet.SetGettingPet(false, vector);
                return;
            }
            player.cosmetics.CurrentPet.SetGettingPet(false, player.cosmetics.CurrentPet.transform.position);
        }
    }
}