using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;

namespace DHMO.Behaviour;

public abstract class AbstractPlayerMenuMinigame : Minigame
{
    protected const int ItemsPerPage = 15, ColumnsPerPage = 3;
    protected static readonly Image? NextButton = NebulaAPI.AddonAsset?.GetResource("NextButton.png")?.AsImage();
    protected static readonly Image? NextButtonActive = NebulaAPI.AddonAsset?.GetResource("NextButtonActive.png")?.AsImage();

    protected UiElement? backButton;
    protected UiElement? defaultButtonSelected;
    internal ShapeshifterPanel panelPrefab = null!;
    protected int currentPage;
    protected float xOffset = 1.95f, xStart = -0.8f, yOffset = -0.65f, yStart = 2.15f;
    protected List<ShapeshifterPanel> AllPanels { get; private set; } = [];

    public AbstractPlayerMenuMinigame(nint ptr) : base(ptr) { }
    public AbstractPlayerMenuMinigame() : base(ClassInjector.DerivedConstructorPointer<AbstractPlayerMenuMinigame>()) => ClassInjector.DerivedConstructorBody(this);

    public static TMenu Create<TMenu>() where TMenu : AbstractPlayerMenuMinigame
    {
        var originalMenu = AmongUsUtil.GetRolePrefab<ShapeshifterRole>()!.ShapeshifterMenu;
        var newMenu = Instantiate(originalMenu);
        var customMenu = newMenu.ModGameObject(false).AddComponent<TMenu>();

        var customMenuObj = customMenu.ModGameObject(false);
        var menuTransform = customMenuObj.GetUnityTransform();

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

        var backBtn = MetaScreen.InstantiateCloseButton(menuTransform, new VVector3(-4.79f, 2.48f, 0f));
        backBtn.ModGameObject(false).LocalScale = new VVector3(0.75f, 0.75f, 1f);
        backBtn.OnClick.AddListener(customMenu.CloseInternal);

        newMenu.DestroyImmediate();
        menuTransform.SetParent(Camera.main.transform, false);
        customMenuObj.LocalPosition = new VVector3(0f, 0f, -60f);

        var phoneUI = menuTransform.TryDig("PhoneUI");
        if (phoneUI.AsBoolFast(out var phone))
        {
            var bodyMat = GamePlayer.LocalPlayer?.VanillaCosmetics.currentBodySprite.BodySprite.material;
            phone.GetChild(0)?.GetComponent<SpriteRenderer>()?.SetMaterial(bodyMat);
            phone.GetChild(1)?.GetComponent<SpriteRenderer>()?.SetMaterial(bodyMat);
        }

        return customMenu;
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
    protected List<UiElement> ShowPage()
    {
        foreach (var panel in AllPanels) panel.ModGameObject(false).SetActive(false);
        var totalPages = GetTotalPages(AllPanels.Count);
        currentPage = Mathn.Clamp(currentPage, 0, totalPages - 1);
        var pageEntries = AllPanels.Skip(currentPage * ItemsPerPage).Take(ItemsPerPage).ToList();
        var uiElements = new List<UiElement>(pageEntries.Count);

        for (int i = 0; i < pageEntries.Count; i++)
        {
            var panel = pageEntries[i];
            var panelObj = panel.ModGameObject(false);

            panelObj.LocalPosition = new VVector3(xStart + i % ColumnsPerPage * xOffset, yStart + i / ColumnsPerPage * yOffset, -1f);
            panelObj.SetActive(true);
            uiElements.Add(panel.Button);
        }
        return uiElements;
    }

    [HideFromIl2Cpp]
    public void Begin(Action<GamePlayer>? onClick = null, Predicate<GamePlayer>? predicate = null, bool withOwner = false)
    {
        this.BeginInternal(null!);

        var players = GamePlayer.AllPlayers.Where(p => predicate == null || predicate(p)).OrderByDescending(p => p.IsAlive).ThenBy(p => p.PlayerId).ToList();
        AllPanels = [];
        currentPage = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.AmOwner && !withOwner) continue;

            var panel = GameObject.Instantiate(panelPrefab, transform);
            var panelObj = panel.ModGameObject(false);

            panelObj.LocalPosition = new VVector3(0f, 0f, -1f);
            panel.SetPlayer(i, player.VanillaPlayer.Data, (Action)(() => onClick?.Invoke(player)));
            panel.NameText.color = VColor.White.ToUnityColor();
            
            var namePlate = panelObj.GetUnityTransform().FindChild("Nameplate");
            namePlate.FindChild("Highlight")?.Find("ShapeshifterIcon").gameObject.SetActive(false);

            OnCreatePanel(player, panel, onClick);
            AllPanels.Add(panel);
        }
        
        ShowPageButtons();
        AfterCreated();
        
        RefreshControllerOverlay(ShowPage());
    }

    protected virtual void OnCreatePanel(GamePlayer player, ShapeshifterPanel panel, Action<GamePlayer>? onClick = null)
    {
    }

    protected virtual void AfterCreated()
    {
    }
    
    protected void ShowPageButtons()
    {
        if (GetTotalPages(AllPanels.Count) > 1)
        {
            var nextButton = CreateArrowButton(this.transform, new VVector3(1.85f, -2.185f, -60f), "RightArrowButton", false);
            nextButton.OnClick.AddListener(this.NextPage);
            var prevButton = CreateArrowButton(this.transform, new VVector3(-1.85f, -2.185f, -60f), "LeftArrowButton", true);
            prevButton.OnClick.AddListener(this.PreviousPage);
        }

        static PassiveButton CreateArrowButton(Transform parent, VVector3 position, string name, bool flipX)
        {
            var button = MetaScreen.InstantiateCloseButton(parent, position);
            button.transform.localScale = new VVector3(0.65f, 0.65f, 1f);
            button.name = name;

            var sprite = button.GetComponent<SpriteRenderer>();
            sprite.sprite = NextButton?.GetSprite();
            sprite.flipX = flipX;

            var passive = button.GetComponent<PassiveButton>();
            passive.OnMouseOver.AddListener(() => sprite.sprite = NextButtonActive?.GetSprite());
            passive.OnMouseOut.AddListener(() => sprite.sprite = NextButton?.GetSprite());
            return passive;
        }
    }

    public void OnDisable() => ControllerManager.Instance.CloseOverlayMenu(name);
    public override void Close() => this.CloseInternal();
}