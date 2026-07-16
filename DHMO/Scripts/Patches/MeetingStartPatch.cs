using Random = UnityEngine.Random;

namespace DHMO.Patches;

[HarmonyPatch]
public static class MeetingStartPatch
{
    [HarmonyPatch(typeof(MeetingHudExtension), nameof(MeetingHudExtension.ModCoStartMeeting)), HarmonyPrefix]
    public static bool ModCoStartMeetingPrefix(PlayerControl reporter, NetworkedPlayerInfo deadBody, int reportType, ref IEnumerator __result)
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
        while (!MeetingHud.Instance.AsBoolFast()) yield return null;

        MeetingRoomManager.Instance.RemoveSelf();
        AmongUsLLImpl.HudManagerInstance.InitMap();
        MapBehaviour.Instance.SetPreMeetingPosition((GamePlayer.LocalPlayer?.Position ?? VVector2.Zero).AsUnityVector3(0f), false);

        foreach (var player in GamePlayer.AllPlayers)
        {
            try
            {
                if (player.VanillaPlayer.AsBoolFast()) player.VanillaPlayer.ModResetForMeeting(false);
            }
            catch (Exception e)
            {
                DLog.Log(e);
            }
        }

        if (MapBehaviour.Instance.AsBoolFast(out var map)) map.Close();
        if (Minigame.Instance.AsBoolFast(out var minigame)) minigame.ForceClose();

        AmongUsLLImpl.ShipStatusInstance.OnMeetingCalled();
        KillAnimation.SetMovement(reporter, true);
        GameData.TimeLastMeetingStarted = Time.realtimeSinceStartup;

        var meetingHud = MeetingHud.Instance;
        meetingHud.StartCoroutine(MeetingHudExtension.ModCoMeetingHudIntro(meetingHud, reporter, deadBody, (MeetingHudExtension.ReportType)reportType).WrapToIl2Cpp());
        yield break;
    }

    public static void ModResetForMeeting(this PlayerControl player, bool spawn = true)
    {
        if (!AmongUsLLImpl.TryGetShipStatus(out var ship)) return;

        if (!player.GetComponent<DummyBehaviour>().enabled)
        {
            player.MyPhysics.ExitAllVents();
            if (spawn && ship.AsBoolFast())
                ship.SpawnPlayer(player, GameData.Instance.PlayerCount, false);
        }
        player.RemoveProtection();
        player.NetTransform.enabled = true;
        player.MyPhysics.ResetMoveState(true);

        foreach (var anim in player.currentRoleAnimations.GetFastEnumerator())
        {
            if (anim == null)
            {
                player.logger.Error("Encountered a null Role Animation while destroying.", null);
                continue;
            }
            if (anim.gameObject.AsBoolFast(out var obj))
            {
                obj.Destroy();
            }
        }

        player.inMovingPlat = false;
        player.isKilling = false;
        player.currentRoleAnimations.Clear();
        if (player.cosmetics.CurrentPet.AsBoolFast(out var pet))
        {
            if (player.cosmetics.petHiddenByViper)
            {
                player.cosmetics.TogglePet(true);
                VVector2 vector = player.transform.position;
                if (ship is AirshipStatus)
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
                pet.SetGettingPet(false, vector);
                return;
            }
            pet.SetGettingPet(false, pet.transform.position);
        }
    }
}