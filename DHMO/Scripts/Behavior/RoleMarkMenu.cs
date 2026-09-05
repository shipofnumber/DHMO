using DHMO.Roles.Abilities;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;

namespace DHMO.Behaviour;

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
            foreach (var r in NebulaAPI.Assignables.AllRoles) assignableSet.Add(r);
            foreach (var m in NebulaAPI.Assignables.AllModifiers) assignableSet.Add(m);
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
                    var dic = RoleMarkAbility.LocalMarkAbility.MarkRoleDic;

                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    renderer.color = dic[player.PlayerId].Contains(a) ? VColor.White.ToUnityColor() : new(0.14f, 0.14f, 0.14f);
                    button.ModGameObject(false).LocalPosition += new VVector3(0.05f, 0f, 0f);
                    text.ModGameObject(false).LocalPosition += new VVector3(0.072f, 0f, 0f);

                    var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", button.transform, new(-0.65f, 0f, -0.1f));
                    icon.sprite = a.GetRoleIcon()?.GetSprite();
                    icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    icon.material = RoleIcon.GetRoleIconMaterial(a, 0.8f);
                    icon.ModGameObject(false).LocalScale = new(0.253f, 0.253f, 1f);
                    icon.SetBothOrder(15);

                    button.OnMouseOut.RemoveAllListeners();
                    button.OnMouseOut.AddListener(() => renderer.color = dic[player.PlayerId].Contains(a) ? VColor.White.ToUnityColor() : new(0.14f, 0.14f, 0.14f));
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
            while (hud.CurrentState != MeetingHud.MeetingStates.Results) yield return null;
        else
            while (!hud.AsBoolFast()) yield return null;
        window.CloseScreen();
    }
}

public class RoleMarkMenu : AbstractPlayerMenuMinigame
{
    static RoleMarkMenu() => ClassInjector.RegisterTypeInIl2Cpp<RoleMarkMenu>();
    public RoleMarkMenu(nint ptr) : base(ptr) { }
    public RoleMarkMenu() : base(ClassInjector.DerivedConstructorPointer<RoleMarkMenu>()) => ClassInjector.DerivedConstructorBody(this);

    public static void Open(Action<GamePlayer> onClick) => Create<RoleMarkMenu>().Begin(onClick);

    protected override void OnCreatePanel(GamePlayer player, ShapeshifterPanel panel, Action<GamePlayer>? onClick = null)
    {
        TextMeshPro nameText = panel.NameText;
        TextMeshPro roleText = GameObject.Instantiate(nameText, nameText.transform);
        var textObj = roleText.ModGameObject(false);
        
        roleText.name = "RoleMarkText";
        roleText.text = "";

        textObj.LocalPosition = new VVector3(0f, -0.1611f, 0f);
        textObj.LocalScale = new VVector3(0.6333f, 0.6333f);

        roleText.rectTransform.sizeDelta += new UVector2(0.35f, 0f);
        roleText.UseRoleIcon();

        ScriptBehaviour script = panel.gameObject.AddComponent<ScriptBehaviour>();
        script.UpdateHandler += () =>
        {
            if (!RoleMarkAbility.LocalMarkAbility.MarkRoleDic.TryGetValue(player.PlayerId, out var assignableSet)) return;

            HashSet<DefinedRole> roles = [];
            foreach (var assignable in assignableSet)
            {
                if (assignable is DefinedRole role)
                    roles.Add(role);
            }

            roleText.text = string.Join(", ", roles.Select(r => r.GetRoleIconTag() + (roles.Count >= 2 ? " " + r.DisplayColoredShort : r.DisplayColoredName)));
        };
        
        var namePlate = panel.gameObject.transform.FindChild("Nameplate");
        var button = namePlate.GetComponent<PassiveButton>();

        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, RoleMarkAbility.LocalMarkAbility.GetModifierString(player.PlayerId)));
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
    }

    protected override void AfterCreated()
    {
        this.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());
    }

    private IEnumerator CoCloseOnResult()
    {
        if (MeetingHud.Instance.AsBoolFast(out var hud))
            while (hud.state != MeetingHud.MeetingStates.Results) yield return null;
        else
            while (!hud.AsBoolFast()) yield return null;
        this.Close();
    }
}