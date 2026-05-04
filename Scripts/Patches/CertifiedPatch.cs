using Nebula.Modules.Cosmetics;

namespace DHMO.Patches;

[NebulaRPCHolder]
[HarmonyPatch]
public class CertifiedPatch
{
    public static string Text = string.Empty;
    private static readonly byte[] HashBuffer = new byte[4096];

    private static RemoteProcess<(byte playerId, int epoch, int build, string vanilla, string[] ids, int[] hashes)> RpcHandshake = new(
        "DHMOHandshake", (message, _) =>
        {
            var player = Helpers.GetPlayer(message.playerId);
            if (player != null && player.gameObject.TryGetComponent(out UncertifiedPlayer certification))
            {
                if (!message.vanilla.Equals(ReferenceDataManager.Instance.Refdata.userFacingVersion))
                    certification.Reject(UncertifiedReason.UnmatchedVanilla);
                else if (message.epoch != NebulaPlugin.PluginEpoch)
                    certification.Reject(UncertifiedReason.UnmatchedEpoch);
                else if (message.build != NebulaPlugin.PluginBuildNum)
                    certification.Reject(UncertifiedReason.UnmatchedBuild);
                else
                    certification.StartCoroutine(CoCertifiedAddons(certification, message.ids, message.hashes).WrapToIl2Cpp());
            }
        }, false);

    private static IEnumerator CoCertifiedAddons(UncertifiedPlayer certification, string[] Ids, int[] Hashes)
    {
        yield return null;
        var dict = NebulaAddon.AllAddons.Where(a => a.NeedHandshake).ToDictionary(a => a.Id, a => a.HandshakeHash);
        List<string> redundant = [];
        HashSet<string> paramIdSet = [.. Ids];
        redundant.AddRange(dict.Keys.Except(paramIdSet));

        List<string> missing = [];
        List<string> unmatched = [];
        for (int i = 0; i < Ids.Length; i++)
        {
            var id = Ids[i];
            if (!dict.TryGetValue(id, out int clientHash))
            {
                missing.Add(id);
                continue;
            }
            if (clientHash != Hashes[i])
                unmatched.Add(id);
        }

        if (redundant.Any() || missing.Any() || unmatched.Any())
        {
            certification.Reject(UncertifiedReason.UnmatchedAddon);
            AddonInfo(redundant, missing, unmatched);
        }
        else
            certification.Certify();
    }

    [HarmonyPatch(typeof(NebulaAddon), nameof(NebulaAddon.HandshakeHash), MethodType.Getter)]
    [HarmonyPostfix]
    public static void HashPostfix(NebulaAddon __instance, ref int __result) => __result = AddonHash(__instance);

    [HarmonyPatch(typeof(Certification), nameof(Certification.Handshake))]
    [HarmonyPrefix]
    public static bool HandshakePrefix()
    {
        Text = string.Empty;
        List<NebulaAddon> handshakeAddons = [.. NebulaAddon.AllAddons.Where(a => a.NeedHandshake)];
        string[] ids = [.. handshakeAddons.Select(a => a.Id)];
        int[] hashes = [.. handshakeAddons.Select(a => a.HandshakeHash)];

        RpcHandshake.Invoke((PlayerControl.LocalPlayer.PlayerId, NebulaPlugin.PluginEpoch, NebulaPlugin.PluginBuildNum, ReferenceDataManager.Instance.Refdata.userFacingVersion, ids, hashes));
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
        new MetaWidgetOld.Text(TextAttributeOld.BoldAttr)
        {
            TranslationKey = UncertifiedPlayer.ReasonToTranslationKey(UncertifiedReason.Uncertified),
            PostBuilder = (text) => __instance.myText = text
        }.Generate(__instance.myShower, Vector2.zero, out _);
        __instance.myText.color = Color.red.RGBMultiplied(0.92f);
        __instance.myText.gameObject.layer = LayerExpansion.GetPlayersLayer();

        var button = __instance.myShower.SetUpButton(false);
        var collider = __instance.myShower.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.6f, 0.2f);
        button.OnMouseOver.AddListener(() =>
        {
            NebulaManager.Instance.SetHelpWidget(button, new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, RawText = Language.Translate(UncertifiedPlayer.ReasonToTranslationKey(__instance.State) + (AmongUsClient.Instance.AmHost ? ".detail" : ".client")) + UnmatchedDetail(__instance.State) });
        });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        __instance.OnStateChanged();

        IEnumerator CoWaitAndUpdate()
        {
            yield return new WaitForSeconds(0.8f);

            for (int i = 0; i < 20; i++)
            {
                if (__instance.State != UncertifiedReason.Waiting) break;
                yield return new WaitForSeconds(0.5f);
            }

            if (__instance.State == UncertifiedReason.Waiting) __instance.Reject(UncertifiedReason.Uncertified);
        }
        __instance.StartCoroutine(CoWaitAndUpdate().WrapToIl2Cpp());
        return false;
    }

    internal static string AddonInfo(List<string> redundant, List<string> missing, List<string> unmatched)
    {
        StringBuilder builder = new();
        if (redundant.Any())
        {
            builder.AppendLine($"<b>{Language.Translate(AmongUsClient.Instance.AmHost ? "certification.unmatchedAddon.missing" : "certification.unmatchedAddon.redundant")}:</b>");
            foreach (var id in redundant)
                builder.AppendLine($"- <color=red><b>{id}</b></color>");
        }
        if (missing.Any())
        {
            builder.AppendLine($"<b>{Language.Translate(AmongUsClient.Instance.AmHost ? "certification.unmatchedAddon.redundant" : "certification.unmatchedAddon.missing")}:</b>");
            foreach (var id in missing)
                builder.AppendLine($"- <color=red><b>{id}</b></color>");
        }
        if (unmatched.Any())
        {
            builder.AppendLine($"<b>{Language.Translate("certification.unmatchedAddon.unmatched")}:</b>");
            foreach (var id in unmatched)
                builder.AppendLine($"- <color=red><b>{id}</b></color>");
        }
        return Text = builder.ToString();
    }

    internal static string UnmatchedDetail(UncertifiedReason reason) => reason == UncertifiedReason.UnmatchedAddon ? "<br>" + Text : string.Empty;

    private static int AddonHash(NebulaAddon addon)
    {
        try
        {
            using MD5 md5 = MD5.Create();
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