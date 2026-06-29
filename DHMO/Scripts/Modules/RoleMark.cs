using AmongUs.GameOptions;
using DHMO.Roles.Abilities;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;

namespace DHMO.Modules;

static public class RoleMarkWindow
{
    readonly static TextAttributeOld ButtonAttribute = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(1.05f, 0.3f), Alignment = TMPro.TextAlignmentOptions.Center, FontMaterial = VanillaAsset.StandardMaskedFontMaterial }.EditFontSize(2f, 1f, 2f);
    readonly static TextAttributeOld TabAttribute = new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial };

    static public MetaScreen OpenRoleSelectWindowUsingTabs(GamePlayer player, IEnumerable<DefinedAssignable>? assignables, (string? tab, Predicate<DefinedAssignable>? predicate)[] tabs, string underText, Action<DefinedAssignable, SpriteRenderer> onSelected)
    {
        var window = MetaScreen.GenerateWindow(new(7.6f, 4.2f), AmongUsLLImpl.HudManagerInstance.transform, new VVector3(0, 0, -200f), true, false);
        MetaWidgetOld widget = new(), inner = new();

        if (assignables == null)
        {
            HashSet<DefinedAssignable> assignableSet = [];
            foreach (var r in Nebula.Roles.Roles.AllRoles) assignableSet.Add(r);
            foreach (var m in Nebula.Roles.Roles.AllModifiers) assignableSet.Add(m);
            assignables = assignableSet;
        }

        bool isFirst = true;
        foreach (var tab in tabs)
        {
            var ary = assignables.Where(a => tab.predicate?.Invoke(a) ?? true).OrderBy(a => a switch
            {
                DefinedRole r => (int)r.Category * 1000 + r.InternalName.GetHashCode(),
                DefinedModifier m => m.InternalName.GetHashCode(),
                _ => 0
            }).ToArray();

            if (!isFirst) inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));
            isFirst = false;
            if (tab.tab != null) inner.Append(new MetaWidgetOld.Text(TabAttribute) { MyText = new RawTextComponent(tab.tab), Alignment = IMetaWidgetOld.AlignmentOption.Center });

            inner.Append(ary, a => new CombinedWidgetOld(new MetaWidgetOld.HorizonalMargin(0.1f), new MetaWidgetOld.Button(null, ButtonAttribute)
            {
                RawText = a.DisplayColoredName,
                TextHorizonotalExtraMargin = 0.15f,
                PostBuilder = (button, renderer, text) =>
                {
                    var dic = RoleMarkAbility.MarkRoleDic;
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    renderer.color = dic != null && dic[player.PlayerId].Contains(a) ? VColor.White : new(0.14f, 0.14f, 0.14f);
                    button.transform.localPosition += new UVector3(0.05f, 0f, 0f);
                    text.transform.localPosition += new UVector3(0.072f, 0f, 0f);

                    var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", button.transform, new(-0.65f, 0f, -0.1f));
                    icon.sprite = a.GetRoleIcon()?.GetSprite();
                    icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    icon.material = RoleIcon.GetRoleIconMaterial(a, 0.8f);
                    icon.transform.localScale = new(0.253f, 0.253f, 1f);
                    icon.SetBothOrder(15);

                    button.OnMouseOut.RemoveAllListeners();
                    button.OnMouseOut.AddListener(() => renderer.color = dic != null && dic[player.PlayerId].Contains(a) ? VColor.White : new(0.14f, 0.14f, 0.14f));
                    button.OnClick.AddListener(() => onSelected.Invoke(a, renderer));
                }
            }), 4, -1, 0, 0.59f);
        }

        MetaWidgetOld.ScrollView scroller = new(new(6.9f, 3.8f), inner, true) { Alignment = IMetaWidgetOld.AlignmentOption.Center };
        widget.Append(scroller);
        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { MyText = new RawTextComponent(underText), Alignment = IMetaWidgetOld.AlignmentOption.Center });
        window.SetWidget(widget);

        window.StartCoroutine(CoCloseOnResult(window).WrapToIl2Cpp());
        return window;
    }

    static IEnumerator CoCloseOnResult(MetaScreen window)
    {
        if (MeetingHud.Instance.AsBoolFast(out var hud))
            while (hud.state != MeetingHud.VoteStates.Results) yield return null;
        else
            while (!MeetingHud.Instance.AsBoolFast()) yield return null;
        window.CloseScreen();
    }
}

public class RoleMarkMenu : Minigame
{
    private const int ItemsPerPage = 15, ColumnsPerPage = 3;
    private static readonly Image? NextButton = NebulaAPI.AddonAsset?.GetResource("NextButton.png")?.AsImage();
    private static readonly Image? NextButtonActive = NebulaAPI.AddonAsset?.GetResource("NextButtonActive.png")?.AsImage();

    internal UiElement? backButton;
    private UiElement? defaultButtonSelected;
    internal ShapeshifterPanel? panelPrefab;
    private int currentPage;
    private float xOffset = 1.95f, xStart = -0.8f, yOffset = -0.65f, yStart = 2.15f;
    private List<ShapeshifterPanel> AllPanels { get; set; } = [];

    static RoleMarkMenu() => ClassInjector.RegisterTypeInIl2Cpp<RoleMarkMenu>();
    public RoleMarkMenu(System.IntPtr ptr) : base(ptr) { }
    public RoleMarkMenu() : base(ClassInjector.DerivedConstructorPointer<RoleMarkMenu>()) => ClassInjector.DerivedConstructorBody(this);

    public void OnDisable() => ControllerManager.Instance.CloseOverlayMenu(name);
    public override void Close() => this.CloseInternal();

    public static void Open(Action<GamePlayer> onClick, Action<TextMeshPro, GamePlayer> textUpdate) => Create().Begin(onClick, textUpdate);

    public static RoleMarkMenu Create()
    {
        var originalMenu = RoleManager.Instance.GetRole(RoleTypes.Shapeshifter).TryCast<ShapeshifterRole>()!.ShapeshifterMenu;
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
        customMenu.CloseSound = newMenu.CloseSound;
        customMenu.logger = newMenu.logger;
        customMenu.OpenSound = newMenu.OpenSound;

        var backBtn = MetaScreen.InstantiateCloseButton(customMenu.transform, new VVector3(-4.79f, 2.48f, 0f));
        backBtn.transform.localScale = new VVector3(0.75f, 0.75f, 1f);
        backBtn.OnClick.AddListener(customMenu.CloseInternal);

        newMenu.DestroyImmediate();
        customMenu.transform.SetParent(Camera.main.transform, false);
        customMenu.transform.localPosition = new VVector3(0f, 0f, -60f);

        var nextButton = CreateArrowButton(customMenu, new VVector3(1.85f, -2.185f, -60f), "RightArrowButton", false);
        nextButton.OnClick.AddListener(customMenu.NextPage);
        var prevButton = CreateArrowButton(customMenu, new VVector3(-1.85f, -2.185f, -60f), "LeftArrowButton", true);
        prevButton.OnClick.AddListener(customMenu.PreviousPage);

        var phoneUI = customMenu.transform.TryDig("PhoneUI");
        if (phoneUI != null)
        {
            var bodyMat = GamePlayer.LocalPlayer?.VanillaCosmetics.currentBodySprite.BodySprite.material;
            phoneUI.GetChild(0)?.GetComponent<SpriteRenderer>()?.SetMaterial(bodyMat);
            phoneUI.GetChild(1)?.GetComponent<SpriteRenderer>()?.SetMaterial(bodyMat);
        }

        return customMenu;
    }

    private static PassiveButton CreateArrowButton(RoleMarkMenu parent, VVector3 position, string name, bool flipX)
    {
        var button = MetaScreen.InstantiateCloseButton(parent.transform, position);
        button.transform.localScale = new VVector3(0.65f, 0.65f, 1f);
        button.name = name;
        var sprite = button.GetComponent<SpriteRenderer>();
        sprite.sprite = NextButton?.GetSprite();
        sprite.flipX = flipX;
        var passive = button.GetComponent<PassiveButton>();
        passive.RemoveAllListeners();
        passive.OnMouseOver.AddListener(() => sprite.sprite = NextButtonActive?.GetSprite());
        passive.OnMouseOut.AddListener(() => sprite.sprite = NextButton?.GetSprite());
        return passive;
    }

    [HideFromIl2Cpp]
    private static int GetTotalPages(int itemCount) => Mathn.Max(1, Mathn.CeilToInt(itemCount / (float)ItemsPerPage));
    internal void RefreshControllerOverlay(List<UiElement> list) => ControllerManager.Instance.OpenOverlayMenu(name, backButton, defaultButtonSelected, list.ToIl2CppList());

    [HideFromIl2Cpp]
    private void NextPage()
    {
        currentPage = (currentPage + 1) % GetTotalPages(AllPanels.Count);
        RefreshControllerOverlay(ShowPage());
    }

    [HideFromIl2Cpp]
    private void PreviousPage()
    {
        var total = GetTotalPages(AllPanels.Count);
        currentPage = (currentPage - 1 + total) % total;
        RefreshControllerOverlay(ShowPage());
    }

    [HideFromIl2Cpp]
    public List<UiElement> ShowPage()
    {
        foreach (var panel in AllPanels) panel.gameObject.SetActive(false);
        var totalPages = GetTotalPages(AllPanels.Count);
        currentPage = Mathn.Clamp(currentPage, 0, totalPages - 1);
        var pageEntries = AllPanels.Skip(currentPage * ItemsPerPage).Take(ItemsPerPage).ToList();
        var uiElements = new List<UiElement>(pageEntries.Count);

        for (int i = 0; i < pageEntries.Count; i++)
        {
            var panel = pageEntries[i];
            panel.transform.localPosition = new VVector3(xStart + i % ColumnsPerPage * xOffset, yStart + i / ColumnsPerPage * yOffset, -1f);
            panel.gameObject.SetActive(true);
            uiElements.Add(panel.Button);
        }
        return uiElements;
    }

    [HideFromIl2Cpp]
    public void Begin(Action<GamePlayer> onClick, Action<TextMeshPro, GamePlayer> textUpdate)
    {
        this.BeginInternal(null!);

        var players = GamePlayer.AllPlayers.OrderByDescending(p => p.IsAlive).ThenBy(p => p.PlayerId).ToList();

        AllPanels = [];
        currentPage = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var panel = Instantiate(panelPrefab, transform);
            panel?.transform.localPosition = new VVector3(xStart + i % ColumnsPerPage * xOffset, yStart + i / ColumnsPerPage * yOffset, -1f);
            panel?.SetPlayer(i, player.VanillaPlayer.Data, (Action)(() => onClick?.Invoke(player)));
            panel?.NameText.color = player == GamePlayer.LocalPlayer ? GamePlayer.LocalPlayer.Role.Role.Color : VColor.White;
            panel?.ColorBlindName.transform.localPosition = new VVector3(-0.9616f, -0.1666f, -0.1f);

            TextMeshPro? nameText = panel?.NameText;
            TextMeshPro? roleText = GameObject.Instantiate(nameText, nameText?.transform);
            roleText?.name = "RoleMarkText";
            roleText?.text = "";
            roleText?.transform.localPosition = new VVector3(0f, -0.1611f, 0f);
            roleText?.transform.localScale = new VVector3(0.6333f, 0.6333f);
            roleText?.rectTransform.sizeDelta += new UVector2(0.35f, 0f);
            roleText?.UseRoleIcon();

            ScriptBehaviour? script = panel?.gameObject.AddComponent<ScriptBehaviour>();
            if (roleText != null) script?.UpdateHandler += () => textUpdate.Invoke(roleText, player);

            var namePlate = panel?.gameObject.transform.FindChild("Nameplate");
            var button = namePlate?.GetComponent<PassiveButton>();

            button?.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, RoleMarkAbility.GetModifierString(player)));
            button?.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            namePlate?.FindChild("Highlight")?.Find("ShapeshifterIcon").gameObject.SetActive(false);

            if (panel != null) AllPanels.Add(panel);
        }

        this.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());
        RefreshControllerOverlay(ShowPage());
    }

    private IEnumerator CoCloseOnResult()
    {
        if (MeetingHud.Instance.AsBoolFast(out var hud))
            while (hud.state != MeetingHud.VoteStates.Results) yield return null;
        else
            while (!MeetingHud.Instance.AsBoolFast()) yield return null;
        this.Close();
    }
}