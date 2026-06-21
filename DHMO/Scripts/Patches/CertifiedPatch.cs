using Hazel;
using Il2CppSystem.Runtime.Remoting.Messaging;

namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public class CertifiedPatch
{
    public static string text = string.Empty;
    public static List<AddonInfo> addonsList = [];

    private static RemoteProcess<(PlayerControl player, int epoch, int build, AddonInfo[] addons)> RpcHandshake = new(
        "DHMOHandshake", (message, _) =>
        {
            if (message.player.gameObject.TryGetComponent<UncertifiedPlayer>(out var component))
            {
                if (message.epoch != NebulaPlugin.PluginEpoch)
                    component.Reject(UncertifiedReason.UnmatchedEpoch);
                else if (message.build != NebulaPlugin.PluginBuildNum)
                    component.Reject(UncertifiedReason.UnmatchedBuild);
                else
                {
                    component.StartCoroutine(CoCheckAddon(component, message.addons));
                }
            }
        }, false);

    static IEnumerator CoCheckAddon(UncertifiedPlayer component, AddonInfo[] addons)
    {
        yield return component;
        HashSet<string> paramIdSet = [.. addons.Select(a => a.Id)];
        List<AddonInfo> redundant = [];

        foreach (var key in addonsList)
        {
            var id = key.Id;
            if (!paramIdSet.Contains(id))
                redundant.Add(key);
        }

        List<AddonInfo> missing = [];
        List<AddonInfo> unmatched = [];
        for (int i = 0; i < addons.Length; i++)
        {
            var id = addons[i].Id;
            if (!addonsList.Exists(a => a.Id == id))
            {
                missing.Add(addons[i]);
                continue;
            }
            if (!addonsList.Exists(a => a.Hash == addons[i].Hash))
                unmatched.Add(addons[i]);
        }
        if (redundant.Count > 0 || missing.Count > 0 || unmatched.Count > 0)
        {
            component.Reject(UncertifiedReason.UnmatchedAddon);
            UnmatchedAddonInfo(redundant, missing, unmatched);
        }
        else
            component.Certify();
    }

    [HarmonyPatch(typeof(Certification), nameof(Certification.Handshake))]
    [HarmonyPrefix]
    public static bool HandshakePrefix()
    {
        RpcHandshake.Invoke((PlayerControl.LocalPlayer, NebulaPlugin.PluginEpoch, NebulaPlugin.PluginBuildNum, addonsList.ToArray()));
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

    internal static string UnmatchedAddonInfo(params List<AddonInfo>[] addonLists)
    {
        var redundant = addonLists[0];
        var missing = addonLists[1];
        var unmatched = addonLists[2];

        StringBuilder builder = new();
        if (redundant.Count > 0)
        {
            builder.AppendLine($"<b>{Language.Translate(AmongUsClient.Instance.AmHost ? "certification.unmatchedAddon.missing" : "certification.unmatchedAddon.redundant")}:</b>");
            foreach (var addon in redundant)
                builder.AppendLine($"- <color=red><b>{addon.Name}({addon.Version})</b></color>");
        }
        if (missing.Count > 0)
        {
            builder.AppendLine($"<b>{Language.Translate(AmongUsClient.Instance.AmHost ? "certification.unmatchedAddon.redundant" : "certification.unmatchedAddon.missing")}:</b>");
            foreach (var addon in missing)
                builder.AppendLine($"- <color=red><b>{addon.Name}({addon.Version})</b></color>");
        }
        if (unmatched.Count > 0)
        {
            builder.AppendLine($"<b>{Language.Translate("certification.unmatchedAddon.unmatched")}:</b>");
            foreach (var addon in unmatched)
                builder.AppendLine($"- <color=red><b>{addon.Name}({addon.Version})</b></color>");
        }
        return text = builder.ToString();
    }

    internal static string UnmatchedDetail(UncertifiedReason reason) => reason == UncertifiedReason.UnmatchedAddon ? "<br>" + text : string.Empty;
    internal static int AddonHash(NebulaAddon addon)
    {
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
            return hash;
        }
        catch
        {
            return addon.HandshakeHash;
        }
    }

    public class AddonInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public int Hash { get; set; }
        
        public AddonInfo(NebulaAddon addon)
        {
            Name = addon.AddonName;
            Id = addon.Id;
            Version = addon.Version;
            Hash = AddonHash(addon);
        }

        public AddonInfo(string name, string id, string version, int hash)
        {
            Name = name;
            Id = id;
            Version = version;
            Hash = hash;
        }

        static AddonInfo() => new RemoteProcessArgument<AddonInfo>((write, val) => val?.Serializer(write), reader => new(reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadInt32()));

        public void Serializer(MessageWriter write)
        {
            write.Write(Name);
            write.Write(Id);
            write.Write(Version);
            write.Write(Hash);
        }
    }
}