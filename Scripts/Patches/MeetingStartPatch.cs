using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DHMO.Patche;

[HarmonyPatch]
public class MeetingStartPatch
{
    [HarmonyPatch(typeof(MeetingHudExtension), "ModCoStartMeeting"), HarmonyPrefix]
    public static bool ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo deadBody, int reportType, ref IEnumerator __result)
    {
        if (NebulaGameManager.Instance != null)
        {
            __result = ModCoStartMeeting(reporter, deadBody, reportType);
            return false;
        }
        return true;
    }

    private static IEnumerator ModCoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo? deadBody, int reportType)
    {
        while (!MeetingHud.Instance) yield return null;

        MeetingRoomManager.Instance.RemoveSelf();
        HudManager.Instance.InitMap();
        MapBehaviour.Instance.SetPreMeetingPosition(PlayerControl.LocalPlayer.transform.position, false);

        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player.VanillaPlayer is not { } vp) continue;
            if (!vp.GetComponent<DummyBehaviour>().enabled) vp.MyPhysics.ExitAllVents();
            vp.RemoveProtection();
            vp.NetTransform.enabled = true;
            vp.MyPhysics.ResetMoveState(true);

            for (int i = 0; i < vp.currentRoleAnimations.Count; i++)
            {
                if (vp.currentRoleAnimations[i]?.gameObject != null)
                    Object.Destroy(vp.currentRoleAnimations[i].gameObject);
            }
            vp.logger.Error("Encountered a null Role Animation while destroying.", null);

            vp.inMovingPlat = false;
            vp.isKilling = false;
            vp.currentRoleAnimations.Clear();

            if (vp.cosmetics.CurrentPet is not { } pet) continue;
            if (vp.cosmetics.petHiddenByViper)
            {
                vp.cosmetics.TogglePet(true);
                var vector = vp.transform.position;
                if (ShipStatus.Instance.TryCast<AirshipStatus>())
                {
                    var list = new List<Vector2>
                        {
                            new(8.2f, 15.2f), new(8.25f, 15.9f), new(8.2f, 14.3f),
                            new(11f, 14.3f), new(9.8f, 14.3f), new(13f, 14.3f)
                        };
                    vector = list[Random.Range(0, list.Count)];
                }
                pet.SetGettingPet(false, vector);
                continue;
            }
            pet.SetGettingPet(false, pet.transform.position);
        }

        if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
        if (Minigame.Instance) Minigame.Instance.ForceClose();
        ShipStatus.Instance.OnMeetingCalled();
        KillAnimation.SetMovement(reporter, true);
        GameData.TimeLastMeetingStarted = Time.realtimeSinceStartup;

        MeetingHud instance = MeetingHud.Instance;
        instance.StartCoroutine(MeetingHudExtension.ModCoMeetingHudIntro(instance, reporter, deadBody, (MeetingHudExtension.ReportType)reportType).WrapToIl2Cpp());
    }
}