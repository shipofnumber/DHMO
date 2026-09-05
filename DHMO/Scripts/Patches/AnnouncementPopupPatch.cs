using System.Text.RegularExpressions;
using AmongUs.Data.Player;
using Assets.InnerNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Virial.Runtime;

namespace DHMO.Patches;

public class AddonNewsAsset
{
    [JsonSerializableField]
    public string iconPath = "";

    [JsonSerializableField] 
    public float defaultPixsPerUnit = 100f;
}

public class AddonNews
{
    public int Id => 200000 + AddonNewsHistory.AllAddonNews.IndexOf(this); 

    [JsonSerializableField]
    public string translationKey = "";
    [JsonSerializableField]
    public string date = "";

    [JsonSerializableField]
    public bool debug = false;
    
    public Image? icon = null;

    private static Regex RoleRegex = Nebula.Patches.ModNewsHistory.RoleRegex;
    private static Regex OptionRegex = Nebula.Patches.ModNewsHistory.OptionRegex;

    public Announcement ToAnnouncement()
    {
        string detailText = Language.Translate($"announcement.{translationKey}.detail");

        foreach (Match match in RoleRegex.Matches(detailText))
        {
            var split = match.Value.Split(':', '(', ')');
            detailText = FormatRoleString(match, detailText, split[1], split[2]);
        }
        foreach (Match match in OptionRegex.Matches(detailText))
        {
            var split = match.Value.Split('(', ')');
            var translated = Language.Find(split[1]) ?? split[3];
            detailText = detailText.Replace(match.Value, translated);
        }

        var result = new Announcement
        {
            Number = Id,
            Title = Language.Translate($"announcement.{translationKey}.title"),
            SubTitle = Language.Translate($"announcement.{translationKey}.subTitle"),
            ShortTitle = Language.Translate($"announcement.{translationKey}.shortTitle"),
            Text = detailText,
            Language = Language.GetCurrentLanguageId(),
            Date = date,
            Id = "AddonNews"
        };
        return result;
    }
    
    private static string FormatRoleString(Match match, string str, string key, string defaultString)
    {
        str = Nebula.Roles.Roles.AllAssignables()
            .Where(role => role.LocalizedName.Equals(key, StringComparison.CurrentCultureIgnoreCase))
            .Aggregate(str, (current, role) => current.Replace(match.Value, role.DisplayColoredName));
        str = str.Replace(match.Value, defaultString);
        return str;
    }
}

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public static class AddonNewsLoader
{
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading News");

        AddonNewsHistory.AllAddonNews.Clear();
        
        foreach (var addon in NebulaAddon.AllAddons)
        {
            var newsStream = NebulaAPI.GetAddon(addon.Id)?.GetResource("AnnouncementNews/AnnouncementNews.json")?.AsStream();
            if (newsStream == null)
            {
                continue;
            }
            
            using var newsReader = new StreamReader(newsStream, Encoding.GetEncoding("utf-8"));

            var news = JsonStructure.Deserialize<List<AddonNews>>(newsReader.ReadToEnd()) ?? [];

            var assetStream = NebulaAPI.GetAddon(addon.Id)?.GetResource("AnnouncementNews/NewsAsset.json")?.AsStream();
                
            using var assetReader = new StreamReader(assetStream ?? Stream.Null, Encoding.GetEncoding("utf-8"));
            var asset = JsonStructure.Deserialize<AddonNewsAsset>(assetReader.ReadToEnd());

            if (asset != null)
            {
                news.Do(n =>
                {
                    n.icon = NebulaAPI.GetAddon(addon.Id)?.GetResource(asset.iconPath)?.AsImage(asset.defaultPixsPerUnit);
                });
                DLog.Log($"{addon.Id} Loads The News Asset.");
            }
            
            DLog.Log($"{addon.Id} Loads News!");
            AddonNewsHistory.AllAddonNews.AddRange(news);
        }
    }
}

[HarmonyPatch]
public static class AddonNewsHistory
{
    public readonly static List<AddonNews> AllAddonNews = [];
    
    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    [HarmonyPriority(-10)]
    public static bool SetAddonAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {        
        List<Announcement> temp =
        [
            .. aRange,
            .. AllAddonNews.Where(m => !m.debug).Select(m => m.ToAnnouncement())
        ];
        temp.Sort((a1, a2) => string.Compare(a2.Date, a1.Date));
        aRange = temp.ToArray();
        
        return true;
    }

    [HarmonyPatch(typeof(AnnouncementPanel), nameof(AnnouncementPanel.SetUp)), HarmonyPostfix, HarmonyPriority(-10)]
    public static void SetUpPanel(AnnouncementPanel __instance, [HarmonyArgument(0)] Announcement announcement)
    {
        if (announcement.Number < 200000) return;

        var label = __instance.transform.FindChild("ModLabel");
        if (label == null) return;

        label.name = "AddonNewsLabel";
        label.localPosition = new VVector3(-0.7f, 0.03f, 0.5f);
        var renderer = label.GetComponent<SpriteRenderer>();
        renderer.sprite = AllAddonNews.FirstOrDefault(an => an.Id == announcement.Number)?.icon?.GetSprite();
    }
}