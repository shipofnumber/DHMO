using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Injection;

namespace DHMO.Modules;

static public class RoleMarkWindow
{
    readonly static TextAttributeOld ButtonAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.05f, 0.3f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(2f, 1f, 2f);
    readonly static TextAttributeOld TabAttribute = new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
    static public MetaScreen OpenRoleSelectWindow(IEnumerable<DefinedRole>? roles, Predicate<DefinedRole>? predicate, bool impRolesArrangeAtFirst, string underText, Action<DefinedRole> onSelected)
        => OpenRoleSelectWindowUsingTabs(roles, [(null, predicate)], impRolesArrangeAtFirst, underText, onSelected);

    static public MetaScreen OpenRoleSelectWindowUsingTabs(IEnumerable<DefinedRole>? roles, (string? tab, Predicate<DefinedRole>? predicate)[] tabs, bool impRolesArrangeAtFirst, string underText, Action<DefinedRole> onSelected)
    {
        var window = MetaScreen.GenerateWindow(new(7.6f, 4.2f), HudManager.Instance.transform, new Vector3(0, 0, -200f), true, false);

        MetaWidgetOld widget = new();

        MetaWidgetOld inner = new();

        if (roles == null)
        {
            HashSet<DefinedRole> roleSet = [];
            foreach (var r in Nebula.Roles.Roles.AllRoles)
            {
                roleSet.Add(r); 
            }
            roles = roleSet;
        }

        int CategoryToInt(RoleCategory roleCategory) => roleCategory switch
        {
            RoleCategory.ImpostorRole => impRolesArrangeAtFirst ? 0 : 1,
            RoleCategory.CrewmateRole => impRolesArrangeAtFirst ? 1 : 0,
            _ => 2
        };

        bool isFirst = true;
        foreach (var tab in tabs)
        {
            var ary = roles.Where(r => tab.predicate?.Invoke(r) ?? true).ToArray();
            ary.Sort((r1, r2) =>
            {
                if (r1.Category == r2.Category) return r1.InternalName.CompareTo(r2.InternalName);
                return CategoryToInt(r1.Category).CompareTo(CategoryToInt(r2.Category));
            });

            if (isFirst) isFirst = false;
            else inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));

            if (tab.tab != null) inner.Append(new MetaWidgetOld.Text(TabAttribute) { MyText = new RawTextComponent(tab.tab), Alignment = IMetaWidgetOld.AlignmentOption.Center });
            inner.Append(ary, r => new CombinedWidgetOld(new MetaWidgetOld.HorizonalMargin(0.1f), new MetaWidgetOld.Button(() => onSelected.Invoke(r), ButtonAttribute)
            {
                RawText = r.DisplayColoredName,
                TextHorizonotalExtraMargin = 0.15f,
                PostBuilder = (button, renderer, text) =>
                {
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    button.transform.localPosition += new Vector3(0.05f, 0f, 0f);
                    text.transform.localPosition += new Vector3(0.072f, 0f, 0f);

                    var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", button.transform, new(-0.65f, 0f, -0.1f));
                    icon.sprite = r.GetRoleIcon()?.GetSprite();
                    icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    icon.material = RoleIcon.GetRoleIconMaterial(r, 0.8f);
                    icon.transform.localScale = new(0.253f, 0.253f, 1f);
                    icon.SetBothOrder(15);
                }
            }), 4, -1, 0, 0.59f);
        }

        MetaWidgetOld.ScrollView scroller = new(new(6.9f, 3.8f), inner, true) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
        widget.Append(scroller);

        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { MyText = new RawTextComponent(underText), Alignment = IMetaWidgetOld.AlignmentOption.Center });

        window.SetWidget(widget);

        IEnumerator CoCloseOnResult()
        {
            if (MeetingHud.Instance)
            {
                while (MeetingHud.Instance.state != MeetingHud.VoteStates.Results) yield return null;
            }
            else
            {
                while (!MeetingHud.Instance) yield return null;
            }
            window.CloseScreen();
        }

        window.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());


        return window;
    }
}

public sealed class RoleMarkMenu : Minigame
{
    internal UiElement? backButton;
    private int currentPage;
    private UiElement? defaultButtonSelected;
    internal ShapeshifterPanel? panelPrefab;
    private List<MenuEntry> allEntries = [];
    public List<ShapeshifterPanel>? potentialVictims;

    private const int ItemsPerPage = 15;

    private float xOffset = 1.95f;
    private float xStart = -0.8f;
    private float yOffset = -0.65f;
    private float yStart = 2.15f;

    public static readonly Image? NextButton = NebulaAPI.AddonAsset?.GetResource("NextButton.png")?.AsImage();
    public static readonly Image? NextButtonActive = NebulaAPI.AddonAsset?.GetResource("NextButtonActive.png")?.AsImage();

    static RoleMarkMenu() => ClassInjector.RegisterTypeInIl2Cpp<RoleMarkMenu>();
    public RoleMarkMenu(System.IntPtr ptr) : base(ptr) { }
    public RoleMarkMenu() : base(ClassInjector.DerivedConstructorPointer<RoleMarkMenu>()) => ClassInjector.DerivedConstructorBody(this);

    public void OnDisable() => ControllerManager.Instance.CloseOverlayMenu(name);

    public static void Open(Func<PlayerControl, bool>? playerMatch, Action<PlayerControl?>? onClick, Action<TextMeshPro, PlayerControl?>? updateRoleText)
    {
        var menu = RoleMarkMenu.Create();
        menu.Begin(playerMatch, onClick, updateRoleText);
    }

    public static RoleMarkMenu Create()
    {
        var shapeShifterRole = RoleManager.Instance.GetRole(RoleTypes.Shapeshifter);
        var originalMenu = shapeShifterRole.TryCast<ShapeshifterRole>()!.ShapeshifterMenu;
        var newMenu = Instantiate(originalMenu);
        var customMenu = newMenu.gameObject.AddComponent<RoleMarkMenu>();

        customMenu.panelPrefab = newMenu.PanelPrefab;
        customMenu.xStart = newMenu.XStart;
        customMenu.yStart = newMenu.YStart;
        customMenu.xOffset = newMenu.XOffset;
        customMenu.yOffset = newMenu.YOffset;
        customMenu.defaultButtonSelected = newMenu.DefaultButtonSelected;
        customMenu.name = "RoleMarkMenu";
        customMenu.backButton = newMenu.BackButton;
        customMenu.backButton?.gameObject.SetActive(false);

        var backBtn = MetaScreen.InstantiateCloseButton(customMenu.transform, new Vector3(-4.79f, 2.48f, 0f)); 
        backBtn.transform.localScale = new Vector3(0.75f, 0.75f, 1f); 
        backBtn.OnClick.AddListener(customMenu.Close);

        customMenu.CloseSound = newMenu.CloseSound;
        customMenu.logger = newMenu.logger;
        customMenu.OpenSound = newMenu.OpenSound;

        newMenu.DestroyImmediate();

        customMenu.transform.SetParent(Camera.main.transform, false);
        customMenu.transform.localPosition = new Vector3(0f, 0f, -60f);

        var nextButton = Instantiate(backBtn, customMenu.transform).gameObject;
        nextButton.transform.localPosition = new Vector3(1.85f, -2.185f, -60f);
        nextButton.transform.localScale = new Vector3(0.65f, 0.65f, 1);
        nextButton.name = "RightArrowButton";
        var sprite = nextButton.GetComponent<SpriteRenderer>();
        sprite.sprite = NextButton?.GetSprite();

        var nextPassive = nextButton.GetComponent<PassiveButton>();
        nextPassive.RemoveAllListeners();
        nextPassive.OnClick.AddListener(() => customMenu.NextPage());
        nextPassive.OnMouseOver.AddListener(() => sprite.sprite = NextButtonActive?.GetSprite());
        nextPassive.OnMouseOut.AddListener(() => sprite.sprite = NextButton?.GetSprite());

        var prevButton = Instantiate(nextButton, customMenu.transform).gameObject;
        prevButton.transform.localPosition = new Vector3(-1.85f, -2.185f, -60f);
        prevButton.name = "LeftArrowButton";
        prevButton.GetComponent<SpriteRenderer>().flipX = true;

        var prevPassive = prevButton.GetComponent<PassiveButton>();
        prevPassive.RemoveAllListeners();
        prevPassive.OnClick.AddListener(() => customMenu.PreviousPage());
        prevPassive.OnMouseOver.AddListener(() => prevButton.GetComponent<SpriteRenderer>().sprite = NextButtonActive?.GetSprite());
        prevPassive.OnMouseOut.AddListener(() => prevButton.GetComponent<SpriteRenderer>().sprite = NextButton?.GetSprite());

        var phoneUI = customMenu.transform.TryDig("PhoneUI");
        if (phoneUI != null)
        {
            var bodyMat = PlayerControl.LocalPlayer.cosmetics.currentBodySprite.BodySprite.material;
            phoneUI.GetChild(0)?.GetComponent<SpriteRenderer>()?.SetMaterial(bodyMat);
            phoneUI.GetChild(1)?.GetComponent<SpriteRenderer>()?.SetMaterial(bodyMat);
        }
        return customMenu;
    }

    private sealed class MenuEntry(ShapeshifterPanel panel)
    {
        public ShapeshifterPanel Panel { get; } = panel;
    }

    private static int GetTotalPages(int itemCount) => Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)ItemsPerPage));

    internal void RefreshControllerOverlay(List<UiElement> list) => ControllerManager.Instance.OpenOverlayMenu(name, backButton, defaultButtonSelected, list.ToIl2CppList());

    private void NextPage()
    {
        var pages = GetTotalPages(allEntries.Count);
        currentPage = (currentPage + 1) % pages;
        RefreshControllerOverlay(ShowPage());
    }

    private void PreviousPage()
    {
        var pages = GetTotalPages(allEntries.Count);
        currentPage = (currentPage - 1 + pages) % pages;
        RefreshControllerOverlay(ShowPage());
    }

    public List<UiElement> ShowPage()
    {
        foreach (var entry in allEntries)
            entry.Panel.gameObject.SetActive(false);

        var totalPages = GetTotalPages(allEntries.Count);
        currentPage = Mathn.Clamp(currentPage, 0, totalPages - 1);

        var pageEntries = allEntries.Skip(currentPage * ItemsPerPage).Take(ItemsPerPage).ToList();
        var uiElements = new List<UiElement>();

        for (int i = 0; i < pageEntries.Count; i++)
        {
            var entry = pageEntries[i];
            int col = i % 3;
            int row = i / 3 % 5;
            entry.Panel.transform.localPosition = new Vector3(xStart + col * xOffset, yStart + row * yOffset, -1f);
            entry.Panel.gameObject.SetActive(true);
            uiElements.Add(entry.Panel.Button);
        }
        return uiElements;
    }

    public void Begin(Func<PlayerControl, bool>? playerMatch, Action<PlayerControl?>? onClick, Action<TextMeshPro, PlayerControl?>? updateRoleText)
    {
        this.BeginInternal(null!);

        List<PlayerControl> players = playerMatch == null ? [.. PlayerControl.AllPlayerControls.GetFastEnumerator()] : [.. PlayerControl.AllPlayerControls.GetFastEnumerator().Where(playerMatch)];

        potentialVictims = [];
        allEntries = [];
        currentPage = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            int col = i % 3;
            int row = i / 3;

            var panel = Instantiate(panelPrefab, transform);
            panel?.transform.localPosition = new Vector3(xStart + col * xOffset, yStart + row * yOffset, -1f);
            panel?.SetPlayer(i, player.Data, (Action)(() => onClick?.Invoke(player)));
            panel?.NameText.color = player == PlayerControl.LocalPlayer ? GamePlayer.LocalPlayer!.Role.Role.UnityColor : Color.white;
            panel?.ColorBlindName.transform.localPosition = new UnityEngine.Vector3(-0.9616f ,-0.1666f ,-0.1f);

            var roleText = GameObject.Instantiate(panel?.NameText, panel?.NameText.transform);
            roleText?.name = "RoleMarkText";
            roleText?.transform.localPosition = new Vector3(0f, -0.1611f, 0f);
            roleText?.transform.localScale = new Vector3(0.6333f, 0.6333f);
            roleText?.rectTransform.sizeDelta += new Vector2(0.35f, 0f);
            roleText?.UseRoleIcon();
            var script = roleText?.gameObject.AddComponent<ScriptBehaviour>();
            if (roleText != null)
                script?.UpdateHandler += () => updateRoleText?.Invoke(roleText, player);

            var highlight = panel?.gameObject.transform.TryDig("Nameplate", "Highlight");
            highlight?.Find("ShapeshifterIcon").gameObject.SetActive(false);

            if (panel != null)
            {
                potentialVictims.Add(panel);
                allEntries.Add(new MenuEntry(panel));
            }
        }

        IEnumerator CoCloseOnResult()
        {
            if (MeetingHud.Instance)
                while (MeetingHud.Instance.state != MeetingHud.VoteStates.Results) yield return null;
            else
                while (!MeetingHud.Instance) yield return null;

            this.Close();
        }

        this.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());

        ControllerManager.Instance.OpenOverlayMenu(name, backButton, defaultButtonSelected, ShowPage().ToIl2CppList());
    }

    public override void Close() => this.CloseInternal();
}