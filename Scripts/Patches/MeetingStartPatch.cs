using Random = UnityEngine.Random;

namespace DHMO.Patches;

[HarmonyPatch]
public class MeetingStartPatch
{
    [HarmonyPatch(typeof(MeetingHudExtension), nameof(MeetingHudExtension.ModCoStartMeeting)), HarmonyPrefix]
    public static bool ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo deadBody, int reportType, ref IEnumerator __result)
    {
        if (NebulaGameManager.Instance is not null)
        {
            __result = ModCoStartMeeting(reporter, deadBody, reportType);
            return false;
        }
        else
            return true;
    }

    private static IEnumerator ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, int reportType)
    {
        while (!MeetingHud.Instance) yield return null;

        MeetingRoomManager.Instance.RemoveSelf();
        DestroyableSingleton<HudManager>.Instance.InitMap();
        MapBehaviour.Instance.SetPreMeetingPosition(PlayerControl.LocalPlayer.transform.position, false);
        foreach (var player in GamePlayer.AllPlayers)
            if (player.VanillaPlayer) ModResetForMeeting(player.VanillaPlayer, false);

        if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
        if (Minigame.Instance) Minigame.Instance.ForceClose();
        ShipStatus.Instance.OnMeetingCalled();
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
                ShipStatus.Instance.SpawnPlayer(player, GameData.Instance.PlayerCount, false);
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
                Vector2 vector = player.transform.position;
                if (ShipStatus.Instance is AirshipStatus)
                {
                    List<Vector2> list =
                    [
                        new Vector2(8.2f, 15.2f),
                        new Vector2(8.25f, 15.9f),
                        new Vector2(8.2f, 14.3f),
                        new Vector2(11f, 14.3f),
                        new Vector2(9.8f, 14.3f),
                        new Vector2(13f, 14.3f)
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