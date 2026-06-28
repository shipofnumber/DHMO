namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public static class CertifiedPatch
{
    public static string text = string.Empty;
    public static int AddonHash
    {
        get
        {
            int val = 0;
            foreach (var addon in NebulaAddon.allOrderedAddons)
                if (addon.NeedHandshake) val ^= CalculateAddonHash(addon);
            return val;
        }
    }
    public static List<AddonInfo> AddonsList { get; set; } = [];

    private static RemoteProcess<(byte playerId, int epoch, int build, int addonHash, AddonInfo[] addons)> RpcHandshake = new(
        "DHMOHandshake", (message, _) =>
        {
            var player = Helpers.GetPlayer(message.playerId);
            if (!(player?.gameObject.TryGetComponent<UncertifiedPlayer>(out var certification) ?? false)) return;

            if (message.epoch != NebulaPlugin.PluginEpoch)
                certification.Reject(UncertifiedReason.UnmatchedEpoch);
            else if (message.build != NebulaPlugin.PluginBuildNum)
                certification.Reject(UncertifiedReason.UnmatchedBuild);
            else if (message.addonHash != AddonHash)
                certification.StartCoroutine(CoCheckAddon(certification, message.addons));
            else
                certification.Certify();
        }, false);

    static IEnumerator CoCheckAddon(UncertifiedPlayer certification, AddonInfo[] addons)
    {
        yield return certification;
        HashSet<string> paramIdSet = [.. addons.Select(a => a.Id)];
        List<AddonInfo> redundant = [], missing = [], unmatched = [];

        foreach (var key in AddonsList)
            if (!paramIdSet.Contains(key.Id)) redundant.Add(key);

        for (int i = 0; i < addons.Length; i++)
        {
            var id = addons[i].Id;
            if (!AddonsList.Exists(a => a.Id == id)) { missing.Add(addons[i]); continue; }
            if (!AddonsList.Exists(a => a.Hash == addons[i].Hash)) unmatched.Add(addons[i]);
        }

        if (redundant.Count > 0 || missing.Count > 0 || unmatched.Count > 0)
            UnmatchedAddonInfo(redundant, missing, unmatched);
    }

    [HarmonyPatch(typeof(Certification), nameof(Certification.Handshake))]
    [HarmonyPrefix]
    public static bool HandshakePrefix()
    {
        byte id = AmongUsLLImpl.LocalPlayer.PlayerId;
        RpcHandshake.Invoke((id, NebulaPlugin.PluginEpoch, NebulaPlugin.PluginBuildNum, AddonHash, AddonsList.ToArray()));
        Certification.RpcShareAchievement.Invoke((id, NebulaAchievementManager.MyTitleData));
        ModSingleton<ShowUp>.Instance?.ShareLocalAfk();
        Nebula.Modules.Cosmetics.DynamicPalette.RpcShareMyColor();
        NebulaAchievementManager.SendLastClearedAchievements();

        if (AmongUsLLImpl.AmongUsClientInstance.AmHost)
        {
            ModSingleton<ShowUp>.Instance?.ShareSocialSettingsAsHost();
            ConfigurationValues.ShareAll();
        }
        return false;
    }

    [HarmonyPatch(typeof(UncertifiedPlayer), nameof(UncertifiedPlayer.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(UncertifiedPlayer __instance)
    {
        __instance.myShower.TryGetComponent<PassiveButton>(out var button);
        button.OnMouseOver.RemoveAllListeners();
        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button,
            $"{Language.Translate($"{UncertifiedPlayer.ReasonToTranslationKey(__instance.State)}{(AmongUsClient.Instance.AmHost ? ".detail" : ".client")}")}<br>{text}"));
    }

    internal static string UnmatchedAddonInfo(params List<AddonInfo>[] addonLists)
    {
        var groups = new (List<AddonInfo> List, string Host, string Client)[]
        {
            (addonLists[0], "certification.unmatchedAddon.missing", "certification.unmatchedAddon.redundant"),
            (addonLists[1], "certification.unmatchedAddon.redundant", "certification.unmatchedAddon.missing"),
            (addonLists[2], "certification.unmatchedAddon.unmatched", "certification.unmatchedAddon.unmatched")
        };

        bool host = AmongUsClient.Instance.AmHost;
        StringBuilder sb = new();
        foreach (var g in groups.Where(x => x.List.Count > 0))
        {
            sb.AppendLine($"<b>{Language.Translate(host ? g.Host : g.Client)}:</b>");
            g.List.ForEach(a => sb.AppendLine($"- <color=red><b>{a.Name}({a.Version})</b></color>"));
        }
        return sb.ToString();
    }

    internal static int CalculateAddonHash(NebulaAddon addon)
    {
        try
        {
            using var md5 = MD5.Create();
            byte[] buffer = new byte[4096];

            foreach (var entry in addon.Archive.Entries)
            {
                if (entry.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) continue;
                using var entryStream = entry.Open();

                int bytesRead;
                while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }
            md5.TransformFinalBlock([], 0, 0);
            if (md5.Hash == null) return addon.HandshakeHash;

            return BitConverter.ToString(md5.Hash).ComputeConstantHash();
        }
        catch { return addon.HandshakeHash; }
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
            Hash = CalculateAddonHash(addon);
        }

        public AddonInfo(string name, string id, string version, int hash)
        {
            Name = name;
            Id = id;
            Version = version;
            Hash = hash;
        }

        static AddonInfo() => new RemoteProcessArgument<AddonInfo>((write, val) => val?.Serializer(write),
            reader => new(reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadInt32()));

        public void Serializer(Virial.Utilities.MessageWriter write)
        {
            write.Write(Name);
            write.Write(Id);
            write.Write(Version);
            write.Write(Hash);
        }
    }
}