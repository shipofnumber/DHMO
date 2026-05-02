namespace DHMO;

public static class DHMOCitations
{
    public static Citation GGD { get; private set; } = new("GooseGooseDuck", null, new ColorTextComponent(Color.white, new RawTextComponent("Goose Goose Duck")), "https://gaggle.fun/goose-goose-duck");
    public static Citation TownOfUsMira { get; private set; } = new("TownOfUsMira", NebulaAPI.AddonAsset.GetResource("TownOfUsMira.png")?.AsImage(70f), new ColorTextComponent(Color.white, new RawTextComponent("TownOfUsMira")), "https://github.com/AU-Avengers/TOU-Mira");
}