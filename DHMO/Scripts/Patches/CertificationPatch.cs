using System.Reflection.Emit;

namespace DHMO.Patches;

public enum UnmatchedReason
{
    Extra,
    Missing,
    HashMismatched,
}

public readonly record struct AddonInfo
{
    public static IEnumerable<AddonInfo> AllAddonInfos => allAddonInfos;
    private static List<AddonInfo> allAddonInfos = [];
    private static StringBuilder Text { get; } = new();
    
    public string Name { get; init; }
    public string Id { get; init; }
    public string Version { get; init; }
    public string Author { get; init; }
    public int Hash { get; init; }

    public AddonInfo(NebulaAddon addon)
    {
        this.Name = addon.AddonName;
        this.Id = addon.Id;
        this.Version = addon.Version;
        this.Author = addon.Author;
        this.Hash = addon.HandshakeHash;
    }

    public AddonInfo(string name, string id, string version, string author, int hash)
    {
        this.Name = name;
        this.Id = id;
        this.Version = version;
        this.Author = author;
        this.Hash = hash;
    }

    public static bool Compare(IEnumerable<AddonInfo> addonInfos, out IEnumerable<(IEnumerable<AddonInfo>, UnmatchedReason)> results)
    {
        var local = AllAddonInfos.ToDictionary(a => a.Id);
        var client = addonInfos.ToDictionary(a => a.Id);
        var unmatches = new List<(IEnumerable<AddonInfo>, UnmatchedReason)>();

        foreach (var remotePair in client)
        {
            if (!local.TryGetValue(remotePair.Key, out var localAddon))
                unmatches.Add(([remotePair.Value], UnmatchedReason.Extra));
            else if (localAddon.Hash != remotePair.Value.Hash)
                unmatches.Add(([localAddon, remotePair.Value], UnmatchedReason.HashMismatched));
        }

        unmatches.AddRange(from localPair in local where !client.ContainsKey(localPair.Key) select ((IEnumerable<AddonInfo>, UnmatchedReason))([localPair.Value], UnmatchedReason.Missing));

        results = unmatches;
        return unmatches.Count > 0;
    }

    public static string ResultToString(IEnumerable<(IEnumerable<AddonInfo> addons, UnmatchedReason reason)> results)
    {
        Text.Clear();
        foreach (var (addons, reason) in results)
        {
            Text.AppendLine($"{Language.Translate($"certification.unmatchedAddon.{reason.ToString().HeadLower()}")}".Bold());
            foreach (var addon in addons)
                Text.AppendLine($"- {addon.Name} ({addon.Id})".Sized(80).Bold().Color(VColor.Red));
        }

        return Text.ToString();
    }

    static AddonInfo()
    {
        new RemoteProcessArgument<AddonInfo>((writer, param) =>
        {
            writer.Write(param.Name);
            writer.Write(param.Id);
            writer.Write(param.Version);
            writer.Write(param.Author);
            writer.Write(param.Hash);
        }, (reader) => new(
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadInt32()
        ));

        foreach (var addon in NebulaAddon.AllAddons)
        {
            if (addon.NeedHandshake) allAddonInfos.Add(new AddonInfo(addon));
        }
    }
}

[HarmonyPatch]
[NebulaRPCHolder]
public static class CertificationPatch
{
    private static RemoteProcess<(byte playerId, int epoch, int build, AddonInfo[] addonInfos)> RpcHandshake = new(
    "HandshakeMod", (message, calledByMe) => 
    {
        var player = Helpers.GetPlayer(message.playerId);
        if (player?.ModGameObject(false).TryGetComponent<UncertifiedPlayer>(out var certification) ?? false)
        {
            if (message.epoch != NebulaPlugin.PluginEpoch)
                certification.RejectMod(UncertifiedReason.UnmatchedEpoch);
            else if (message.build != NebulaPlugin.PluginBuildNum)
                certification.RejectMod(UncertifiedReason.UnmatchedBuild);
            else if (AddonInfo.Compare(message.addonInfos, out var result))
                certification.RejectMod(UncertifiedReason.UnmatchedAddon, result);
            else
                certification.Certify();
        }
    }, false);

    private static RemoteProcess<(PlayerControl player, PlayerControl host, string text)> RpcUpdateStatus = new(
        "UpdateCertificationStatus", (message, calledByMe) =>
        {
            if (!message.player.AsBoolFast(out var player) || !message.host.AsBoolFast(out var host)) return;

            UpdateButton(host, player);
            UpdateButton(player, host);
            return;

            void UpdateButton(PlayerControl owner, PlayerControl target)
            {
                if (!owner.AmOwner)
                    return;

                if (!target.ModGameObject(false).TryGetComponent<UncertifiedPlayer>(out var certification))
                    return;

                var button = certification.myShower.GetComponent<PassiveButton>();
                button.OnMouseOver.RemoveAllListeners();
                button.OnMouseOver.AddListener(() =>
                {
                    NebulaManager.Instance.SetHelpWidget(button, new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr)
                    {
                        Alignment = IMetaWidgetOld.AlignmentOption.Left,
                        RawText = $"{Language.Translate($"{UncertifiedPlayer.ReasonToTranslationKey(certification.State)}{(AmongUsLLImpl.AmongUsClientInstance.AmHost ? ".detail" : ".client")}")}<br>{message.text}"
                    });
                });
            }
        }, false);

    public static void SendHandshake((byte playerId, int epoch, int build) tuple)
    {
        var parameters = (tuple.playerId, tuple.epoch, tuple.build, AddonInfo.AllAddonInfos.ToArray());
        RpcHandshake.Invoke(parameters);
    }

    [HarmonyPatch(typeof(Certification), nameof(Certification.Handshake))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        FieldInfo? rpcHandshakeField = typeof(Certification).GetField("RpcHandshake", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo? customMethod = typeof(CertificationPatch).GetMethod(nameof(CertificationPatch.SendHandshake), BindingFlags.Public | BindingFlags.Static);

        if (rpcHandshakeField == null || customMethod == null)
            return instructions;

        ConstructorInfo? tuple = typeof(ValueTuple<byte, int, int>).GetConstructor([typeof(byte), typeof(int), typeof(int)]);
        if (tuple == null)
            return instructions;

        bool patched = false;

        for (int i = 0; i < codes.Count && !patched; i++)
        {
            if (codes[i].opcode != OpCodes.Ldsfld ||
                codes[i].operand is not FieldInfo fi ||
                fi != rpcHandshakeField)
            {
                continue;
            }

            int invokeIdx = -1;
            for (int j = i + 1; j < codes.Count; j++)
            {
                if ((codes[j].opcode == OpCodes.Call || codes[j].opcode == OpCodes.Callvirt) &&
                    codes[j].operand is MethodInfo invokeMi &&
                    invokeMi.Name == "Invoke")
                {
                    invokeIdx = j;
                    break;
                }
            }

            if (invokeIdx == -1)
                continue;

            var paramInstructions = new List<CodeInstruction>();
            for (int k = i + 1; k < invokeIdx; k++)
            {
                paramInstructions.Add(new CodeInstruction(codes[k]));
            }

            if (paramInstructions.Count >= 5)
            {
                int newobjIdx = paramInstructions.FindLastIndex(ci => ci.opcode == OpCodes.Newobj);
                if (newobjIdx >= 4)
                {
                    paramInstructions.RemoveAt(newobjIdx - 1);
                    newobjIdx--;
                    paramInstructions[newobjIdx] = new CodeInstruction(OpCodes.Newobj, tuple);
                }
            }

            if (codes[i].labels.Count > 0 && paramInstructions.Count > 0)
            {
                foreach (var label in codes[i].labels)
                    paramInstructions[0].labels.Add(label);
            }

            var replacement = new List<CodeInstruction>();
            replacement.AddRange(paramInstructions);
            replacement.Add(new CodeInstruction(OpCodes.Call, customMethod));

            int removeCount = invokeIdx - i + 1;
            codes.RemoveRange(i, removeCount);
            codes.InsertRange(i, replacement);

            patched = true;
        }

        return codes.AsEnumerable();
    }

    public static void RejectMod(this UncertifiedPlayer uncertified, UncertifiedReason reason, IEnumerable<(IEnumerable<AddonInfo>, UnmatchedReason)>? results = null)
    {
        uncertified.Reject(reason);

        if (!AmongUsLLImpl.AmongUsClientInstance.AmHost || !uncertified.MyControl.AsBoolFast(out var player) || player.AmHost()) return;
        using (NebulaAPI.CreateRPCSection("SendCertificationMessage"))
        {
            if (results != null)
                RpcUpdateStatus.Invoke((player, AmongUsLLImpl.LocalPlayer, AddonInfo.ResultToString(results)));

            APICompat.RpcAddLobbyNotification.Invoke((player.Data.PlayerId.ToString(), Language.Translate("certification.notification").Replace("%REASON%", Language.Translate(UncertifiedPlayer.ReasonToTranslationKey(reason))).Replace("%PLAYER%", player.Data.PlayerName), AmongUsLLImpl.HudManagerInstance.Notifier.disconnectColor, 1, false));
        }
    }
}