namespace DHMO.Patches;

public static class AddonHandshakeHashPatch
{
    static AddonHandshakeHashPatch()
    {
        foreach (var addon in NebulaAddon.AllAddons)
            addon.HandshakeHash = CalculateAddonHash(addon);
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