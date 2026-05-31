namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public class CertifiedPatch
{
    public static string text = string.Empty;
    public static Dictionary<string, int> addonDictionary = [];

    private static readonly RemoteProcess<(PlayerControl player, int epoch, int build, string[] ids, int[] hashes)> RpcHandshake = new(
        "DHMOHandshake", (message, _) =>
        {
            if (message.player.gameObject.TryGetComponent<UncertifiedPlayer>(out var certification))
            {
                if (message.epoch != NebulaPlugin.PluginEpoch)
                    certification.Reject(UncertifiedReason.UnmatchedEpoch);
                else if (message.build != NebulaPlugin.PluginBuildNum)
                    certification.Reject(UncertifiedReason.UnmatchedBuild);
                else
                {
                    HashSet<string> paramIdSet = [.. message.ids];
                    List<string> redundant = new(addonDictionary.Count);

                    foreach (var key in addonDictionary.Keys)
                    {
                        if (!paramIdSet.Contains(key))
                            redundant.Add(key);
                    }

                    List<string> missing = new(message.ids.Length);
                    List<string> unmatched = new(message.hashes.Length);
                    for (int i = 0; i < message.ids.Length; i++)
                    {
                        var id = message.ids[i];
                        if (!addonDictionary.TryGetValue(id, out int clientHash))
                        {
                            missing.Add(id);
                            continue;
                        }
                        if (clientHash != message.hashes[i])
                            unmatched.Add(id);
                    }
                    if (redundant.Count > 0 || missing.Count > 0 || unmatched.Count > 0)
                    {
                        certification.Reject(UncertifiedReason.UnmatchedAddon);
                        AddonInfo(redundant, missing, unmatched);
                    }
                    else
                        certification.Certify();
                }
            }
        }, false);

    [HarmonyPatch(typeof(Certification), nameof(Certification.Handshake))]
    [HarmonyPrefix]
    public static bool HandshakePrefix()
    {
        var ids = addonDictionary.Keys.ToArray();
        var hashes = addonDictionary.Values.ToArray();

        RpcHandshake.Invoke((PlayerControl.LocalPlayer, NebulaPlugin.PluginEpoch, NebulaPlugin.PluginBuildNum, ids, hashes));
        Certification.RpcShareAchievement.Invoke((PlayerControl.LocalPlayer.PlayerId, NebulaAchievementManager.MyTitleData));
        ModSingleton<ShowUp>.Instance?.ShareLocalAfk();
        Nebula.Modules.Cosmetics.DynamicPalette.RpcShareMyColor();
        NebulaAchievementManager.SendLastClearedAchievements();
        if (AmongUsClient.Instance.AmHost)
        {
            ModSingleton<ShowUp>.Instance?.ShareSocialSettingsAsHost();
            ConfigurationValues.ShareAll();
        }
        return false;
    }

    [HarmonyPatch(typeof(UncertifiedPlayer), nameof(UncertifiedPlayer.Start))]
    [HarmonyPostfix]
    public static void StartPrefix(UncertifiedPlayer __instance)
    {
        __instance.myShower.TryGetComponent<PassiveButton>(out var button);
        button.OnMouseOver.RemoveAllListeners();
        button.OnMouseOver.AddListener(() =>
        {
            NebulaManager.Instance.SetHelpWidget(button, new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr)
            {
                Alignment = IMetaWidgetOld.AlignmentOption.Left,
                RawText = $"{Language.Translate($"{UncertifiedPlayer.ReasonToTranslationKey(__instance.State)}{(AmongUsClient.Instance.AmHost ? ".detail" : ".client")}")}<br>{text}"
            });
        });
    }

    internal static string AddonInfo(List<string> redundant, List<string> missing, List<string> unmatched)
    {
        StringBuilder builder = new();
        if (redundant.Count > 0)
        {
            builder.AppendLine($"<b>{Language.Translate(AmongUsClient.Instance.AmHost ? "certification.unmatchedAddon.missing" : "certification.unmatchedAddon.redundant")}:</b>");
            foreach (var id in redundant)
                builder.AppendLine($"- <color=red><b>{id}</b></color>");
        }
        if (missing.Count > 0)
        {
            builder.AppendLine($"<b>{Language.Translate(AmongUsClient.Instance.AmHost ? "certification.unmatchedAddon.redundant" : "certification.unmatchedAddon.missing")}:</b>");
            foreach (var id in missing)
                builder.AppendLine($"- <color=red><b>{id}</b></color>");
        }
        if (unmatched.Count > 0)
        {
            builder.AppendLine($"<b>{Language.Translate("certification.unmatchedAddon.unmatched")}:</b>");
            foreach (var id in unmatched)
                builder.AppendLine($"- <color=red><b>{id}</b></color>");
        }
        return text = builder.ToString();
    }

    internal static string UnmatchedDetail(UncertifiedReason reason) => reason == UncertifiedReason.UnmatchedAddon ? "<br>" + text : string.Empty;
    internal static int AddonHash(NebulaAddon addon)
    {
        if (addonDictionary.TryGetValue(addon.Id, out int cachedHash))
            return cachedHash;

        try
        {
            using var md5 = MD5.Create();
            byte[] buffer = new byte[4096];

            foreach (var entry in addon.Archive.Entries)
            {
                if (entry.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var entryStream = entry.Open();
                int bytesRead;
                while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }

            md5.TransformFinalBlock([], 0, 0);

            if (md5.Hash == null)
                return addon.HandshakeHash;

            int hash = BitConverter.ToString(md5.Hash).ComputeConstantHash();
            addonDictionary[addon.Id] = hash;
            return hash;
        }
        catch
        {
            return addon.HandshakeHash;
        }
    }
}