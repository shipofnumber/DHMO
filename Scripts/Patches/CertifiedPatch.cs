using Nebula.Modules.Cosmetics;
using Color = UnityEngine.Color;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public static class CertifiedPatch
{
    static string au = string.Empty;
    static string nos = string.Empty;
    public static List<string> unmatched = [];
    private static readonly byte[] HashBuffer = new byte[4096];

    private static RemoteProcess<(PlayerControl player, int epoch, int build, int addonHash, string vanilla, string[] id, int[] hash)> RpcHandshake = new(
        "DHMOHandshake", (message, _) => {
            var player = message.player;
            if (player?.gameObject.TryGetComponent(out UncertifiedPlayer certification) ?? false)
            {
                if (message.vanilla != Application.version)
                {
                    au = Application.version;
                    certification.Reject(UncertifiedReason.UnmatchedVanilla);
                }
                else if (message.epoch != NebulaPlugin.PluginEpoch)
                {
                    nos = NebulaPlugin.VisualVersion;
                    certification.Reject(UncertifiedReason.UnmatchedEpoch);
                }
                else if (message.build != NebulaPlugin.PluginBuildNum)
                {
                    nos = NebulaPlugin.VisualVersion;
                    certification.Reject(UncertifiedReason.UnmatchedBuild);
                }
                else if (message.addonHash != NebulaAddon.AddonHandshakeHash)
                {
                    certification.Reject(UncertifiedReason.UnmatchedAddon);
                    unmatched.Clear();
                    
                    var dict = NebulaAddon.AllAddons
                        .Where(a => a.NeedHandshake)
                        .ToDictionary(a => a.Id, a => a.HandshakeHash);
                    
                    var paramIdSet = new HashSet<string>(message.id);
                    unmatched.AddRange(dict.Keys.Except(paramIdSet));
                    for (int i = 0; i < message.id.Length; i++)
                    {
                        var id = message.id[i];
                        if (!dict.TryGetValue(id, out int localHash) || localHash != message.hash[i])
                        {
                            unmatched.Add(id);
                        }
                    }
                }
                else
                    certification.Certify();
            }
        }, false);

    [HarmonyPatch(typeof(NebulaAddon), nameof(NebulaAddon.HandshakeHash), MethodType.Getter)]
    [HarmonyPostfix]
    public static void HashPostfix(NebulaAddon __instance, ref int __result)
    {
        __result = AddonHash(__instance);
    }

    [HarmonyPatch(typeof(Certification), nameof(Certification.Handshake))]
    [HarmonyPrefix]
    public static bool HandshakePrefix()
    {
        au = string.Empty;
        nos = string.Empty;
        if (NebulaAddon.AllAddons == null) return true;
        List<NebulaAddon> handshakeAddons = [.. NebulaAddon.AllAddons.Where(a => a.NeedHandshake)];
        string[] ids = [.. handshakeAddons.Select(a => a.Id)];
        int[] hashes = [.. handshakeAddons.Select(a => a.HandshakeHash)];
        
        RpcHandshake.Invoke((PlayerControl.LocalPlayer, NebulaPlugin.PluginEpoch, NebulaPlugin.PluginBuildNum, NebulaAddon.AddonHandshakeHash, Application.version, ids, hashes));
        Certification.RpcShareAchievement.Invoke((PlayerControl.LocalPlayer.PlayerId, NebulaAchievementManager.MyTitleData));
        ModSingleton<ShowUp>.Instance?.ShareLocalAfk();
        DynamicPalette.RpcShareMyColor();
        NebulaAchievementManager.SendLastClearedAchievements();
        if (AmongUsClient.Instance.AmHost)
        {
            ModSingleton<ShowUp>.Instance?.ShareSocialSettingsAsHost();
            ConfigurationValues.ShareAll();
        }
        return false;
    }

    [HarmonyPatch(typeof(UncertifiedPlayer), nameof(UncertifiedPlayer.Start))]
    [HarmonyPrefix]
    public static bool StartPrefix(UncertifiedPlayer __instance)
    {
        __instance.State = UncertifiedReason.Waiting;

        __instance.myShower = UnityHelper.CreateObject("UncertifiedHolder", __instance.gameObject.transform, new Vector3(0, 0, -20f), LayerExpansion.GetPlayersLayer());
        (new MetaWidgetOld.Text(TextAttributeOld.BoldAttr)
        {
            TranslationKey = UncertifiedPlayer.ReasonToTranslationKey(UncertifiedReason.Uncertified),
            PostBuilder = (text) => __instance.myText = text
        }).Generate(__instance.myShower, Vector2.zero, out _);
        __instance.myText.color = Color.red.RGBMultiplied(0.92f);
        __instance.myText.gameObject.layer = LayerExpansion.GetPlayersLayer();

        var button = __instance.myShower.SetUpButton(false);
        var collider = __instance.myShower.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.6f, 0.2f);
        button.OnMouseOver.AddListener(() =>
        {
            NebulaManager.Instance.SetHelpWidget(button, new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, RawText = Language.Translate(UncertifiedPlayer.ReasonToTranslationKey(__instance.State) + (AmongUsClient.Instance.AmHost ? ".detail" : ".client")) + UnmatchedAddonDetail(__instance.State) });
        });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        __instance.OnStateChanged();

        IEnumerator CoWaitAndUpdate()
        {
            yield return new WaitForSeconds(0.8f);

            int waitCount = 0;
            while (__instance.State == UncertifiedReason.Waiting && waitCount < 16)
            {
                yield return new WaitForSeconds(0.5f);
                waitCount++;
            }

            if (__instance.State == UncertifiedReason.Waiting)
                __instance.Reject(UncertifiedReason.Uncertified);
        }
        __instance.StartCoroutine(CoWaitAndUpdate().WrapToIl2Cpp());
        return false;
    }

    internal static string UnmatchedAddonDetail(UncertifiedReason reason)
    {
        StringBuilder sb = new();
        switch (reason)
        {
            case UncertifiedReason.UnmatchedVanilla:
                sb.AppendLine($"\n- <b>Among Us</b>: <b><color=red>{au}</color></b>");
                return sb.ToString();
            case UncertifiedReason.UnmatchedEpoch:
            case UncertifiedReason.UnmatchedBuild:
                sb.AppendLine($"\n- <b>Nebula on the Ship</b>: <b><color=red>{nos}</color></b>");
                return sb.ToString();
            case UncertifiedReason.UnmatchedAddon:
                foreach (var addon in unmatched)
                {
                    sb.AppendLine($"\n- <b><color=red>{addon}</color></b>");
                }
                return sb.ToString();
            default: return string.Empty;
        }
    }

    private static int AddonHash(this NebulaAddon addon)
    {
        try
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            foreach (var entry in addon.Archive.Entries)
            {
                if (entry.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var entryStream = entry.Open();
                int bytesRead;
                while ((bytesRead = entryStream.Read(HashBuffer, 0, HashBuffer.Length)) > 0)
                    md5.TransformBlock(HashBuffer, 0, bytesRead, HashBuffer, 0);
            }
            md5.TransformFinalBlock([], 0, 0);
            return md5.Hash == null ? addon.HandshakeHash : BitConverter.ToString(md5.Hash).ComputeConstantHash();
        }
        catch
        {
            return addon.HandshakeHash;
        }
    }
}