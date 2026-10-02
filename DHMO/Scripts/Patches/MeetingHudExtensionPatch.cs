using Nebula.Patches;
using Random = UnityEngine.Random;

namespace DHMO.Patches;

[HarmonyPatch]
public static class MeetingHudExtensionPatch
{
    public static List<GameObject> RightContents { get; } = [];

    extension(MeetingHudExtension)
    {
        public static void AddRightContent(GameObject obj)
        {
            obj.transform.SetParent(MeetingHud.Instance.transform);
            obj.layer = LayerExpansion.GetUILayer();
            obj.transform.localPosition = new VVector3(4.625f, 1.4f - 1.2f * RightContents.Count, -40f);
            RightContents.Add(obj);
            MeetingHudExtension.ActionCoolDown = 0.8f;
        }
    }

    [HarmonyPatch(typeof(MeetingHudExtension), nameof(MeetingHudExtension.InitMeetingTimer))]
    [HarmonyPostfix]
    public static void InitMeetingTimerPostfix()
    {
        var judgeMeetingManager = ModSingleton<JudgeMeetingManager>.Instance;
        if (!judgeMeetingManager.IsSpecialMeeting) return;

        MeetingHudExtension.DiscussionTimer = Mathn.Max(0f, judgeMeetingManager.LastMeetingDiscussionTime);
        MeetingHudExtension.VotingTimer = Mathn.Max(15f, judgeMeetingManager.LastMeetingVotingTime);
    }

    [HarmonyPatch(typeof(MeetingModRpc), nameof(MeetingModRpc.ForcelyVotingComplete))]
    [HarmonyPrefix]
    public static void VoteCompletePrefix([HarmonyArgument(2)] List<MeetingHud.VoterState> finalStates, ref byte exiled, ref byte[] exiledAll)
    {
        var judgeMeetingManager = ModSingleton<JudgeMeetingManager>.Instance;
        if (!judgeMeetingManager.IsSpecialMeeting) return;
        
        var alivePlayers = APICompat.AlivePlayers;
        var judges = alivePlayers.Where(p => p.Role.GetAbility<EvilJudge.Ability>() != null).ToArray();
        if (judges.Length == 0) return;

        var judgeIds = judges.Select(j => j.PlayerId).ToHashSet();
        var skipVoters = finalStates
            .Where(v => !v.AmDead && v.SkippedVote && !judgeIds.Contains(v.VoterId))
            .Select(v => GamePlayer.GetPlayer(v.VoterId))
            .NotNull()
            .DistinctBy(p => p.PlayerId)
            .ToArray();

        if (skipVoters.Length < alivePlayers.Length / 2) return;

        var sacrificeTargets = skipVoters
            .Where(p => !judges.Any(j => j.IsSameSideOf(p)) 
                        && EvilJudge.RoleFilterOption.Contains(p.Role.Role) 
                        && p.Role.Role is not Jester)
            .ToArray();
        if (sacrificeTargets.Length <= 0) return;

        var sacrificialExile = sacrificeTargets[System.Random.Shared.Next(sacrificeTargets.Length)];
        if (exiledAll.Contains(sacrificialExile.PlayerId)) return;

        judgeMeetingManager.WillSacrificialExile = sacrificialExile;
        if (exiled == byte.MaxValue)
        {
            exiled = sacrificialExile.PlayerId;
            exiledAll = [exiled, ..exiledAll];
        }
        else
        {
            exiledAll = [..exiledAll, sacrificialExile.PlayerId];
        }
    }

    [HarmonyPatch(typeof(MeetingDestroyPatch), nameof(MeetingDestroyPatch.Postfix))]
    [HarmonyPrefix]
    public static bool MeetingHudOnDestoryPatch()
    {
        return false;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ResetForMeeting))]
    [HarmonyPrefix]
    public static bool ResetForMeetingPrefix(PlayerControl __instance)
    {
        if (!__instance.AsBoolFast()) return true;

        __instance.ModResetForMeeting(!MeetingHud.Instance.AsBoolFast());
        return false;
    }

    public static void ModResetForMeeting(this PlayerControl player, bool spawn = true)
    {
        if (!AmongUsLLImpl.TryGetShipStatus(out var ship)) return;

        if (!player.GetComponent<DummyBehaviour>().enabled)
        {
            player.MyPhysics.ExitAllVents();
            if (spawn)
            {
                ship.SpawnPlayer(player, GameData.Instance.PlayerCount, false);
            }
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
                    vector = list[System.Random.Shared.Next(list.Count)];
                }
                pet.SetGettingPet(false, vector);
                return;
            }
            pet.SetGettingPet(false, pet.transform.position);
        }
    }
}