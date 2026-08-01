namespace DHMO.Patches;

[HarmonyPatch(typeof(NebulaAddon))]
public static class AddonHandshakeHashPatch
{
    public static int AddonHandshakeHash { get; internal set; }

    static AddonHandshakeHashPatch()
    {
        int val = 0;
        foreach (var addon in NebulaAddon.AllAddons)
        {
            if (addon.NeedHandshake) val ^= CalculateAddonHash(addon);
        }

        AddonHandshakeHash = val;
    }

    [HarmonyPatch(nameof(NebulaAddon.AddonHandshakeHash), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix(ref int __result)
    {
        __result = AddonHandshakeHashPatch.AddonHandshakeHash;
        return false;
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
        catch
        {
            return addon.HandshakeHash;
        }
    }
}