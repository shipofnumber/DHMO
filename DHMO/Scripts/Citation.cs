namespace DHMO;

public static class DHMOCitations
{
    public static Citation GGD { get; private set; } = new("GooseGooseDuck", null, new ColorTextComponent(VColor.White, new RawTextComponent("Goose Goose Duck")), "https://gaggle.fun/goose-goose-duck");
    public static Citation TownOfUsMira { get; private set; } = new("TownOfUsMira", NebulaAPI.AddonAsset.GetResource("Citation/TownOfUsMira.png")?.AsImage(70f), new ColorTextComponent(VColor.White, new RawTextComponent("TownOfUsMira")), "https://github.com/AU-Avengers/TOU-Mira");
    public static Citation DHMO { get; private set; } = new("DHMO", NebulaAPI.AddonAsset.GetResource("DHMO.png")?.AsImage(50f), new ColorTextComponent(VColor.White, new RawTextComponent("DHMO")), "https://github.com/shipofnumber/DHMO");
}