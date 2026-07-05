namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class LobbyRule : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public LobbyRule() => ModSingleton<LobbyRule>.Instance = this;
    static LobbyRule() => DIManager.Instance.RegisterModule(() => new LobbyRule());

    private static MetaScreen? lastWindow;
    public static bool IsShown => lastWindow != null;

    protected override void OnInjected(Game container) => this.Register(container);

    public static RemoteProcess<PlayerControl> RpcRequestRules = new("RequestRules", (requester, _) =>
    {
        if (!AmongUsLLImpl.AmongUsClientInstance.AmHost) return;
        RpcShowRules?.Invoke((AmongUsLLImpl.LocalPlayer, requester, GetLobbyRulesText()));
    });

    public static RemoteProcess<(PlayerControl host, PlayerControl target, string rulesText)> RpcShowRules = new("ShowRules", (message, _) =>
    {
        if (!message.host.AmHost() || AmongUsLLImpl.LocalPlayer.PlayerId != message.target.PlayerId) return;
        ModSingleton<LobbyRule>.Instance.OpenClientRuleScreen(message.rulesText);
    });

    void OnUpdate(UpdateEvent ev)
    {
        if (!PreloadManager.FinishedPreload || !((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.U))
            || AmongUsLLImpl.AmongUsClientInstance.GameState == InnerNet.InnerNetClient.GameStates.NotJoined || IsShown) return;

        if (AmongUsLLImpl.LocalPlayer.AmHost()) OpenHostRuleScreen();
        else RpcRequestRules.Invoke(PlayerControl.LocalPlayer);
    }

    private static string GetLobbyRulesPath()
    {
        var dir = new DirectoryInfo(Application.dataPath).Parent?.FullName ?? Application.dataPath;
        return string.IsNullOrEmpty(dir) ? string.Empty : Path.Combine(dir, "LobbyRules.txt");
    }

    private (MetaScreen window, string hostName, GUIWidget titleWidget) CreateRuleWindow()
    {
        var window = MetaScreen.GenerateWindow(new VVector2(7.5f, 4.6f), AmongUsLLImpl.HudManagerInstance.transform, new VVector3(0f, 0f, -200f), true, false, true, BackgroundSetting.Modern);
        lastWindow = window;
        var host = AmongUsLLImpl.AmongUsClientInstance.GetHost().Character;
        var hostName = $"<b>{Language.Translate("lobby.host")}</b>: <b>{host.name.Color(MyContainer.GetColor(host.PlayerId).MainColor)}</b>";

        var titleWidget = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentTitle, Language.Translate("lobby.host.rule")),
            NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentStandard, hostName));

        return (window, hostName, titleWidget);
    }

    internal MetaScreen? OpenClientRuleScreen(string? rule)
    {
        if (string.IsNullOrEmpty(rule)) { DebugScreen.Push(Language.Translate("lobby.rule.error"), 1f); return null; }

        var (window, _, titleWidget) = CreateRuleWindow();

        var ruleWidget = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, titleWidget,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentStandard, $"<size=125%>{rule}</size>"),
            NebulaAPI.GUI.VerticalMargin(0.15f));

        window.SetWidget(ruleWidget, new VVector2(0.5f, 1f), out _);
        return window;
    }

    internal MetaScreen? OpenHostRuleScreen()
    {
        var (window, _, titleWidget) = CreateRuleWindow();
        var inputField = new GUITextField(GUIAlignment.Center, new Virial.Compat.Size(7f, 3.25f))
        {
            IsSharpField = false,
            MaxLines = 16,
            FontSize = 1.4f,
            DefaultText = GetLobbyRulesText(true) ?? "",
            HintText = Language.Translate("ui.lobby.rule").Color(VColor.Gray)
        };
        var confirmButton = new GUIModernButton(GUIAlignment.Center, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.dialog.confirmation"))
        {
            OnClick = _ =>
            {
                var field = inputField.Artifact.FirstOrDefault();
                var text = field?.Text ?? "";
                if (string.IsNullOrEmpty(text))
                {
                    field?.SetHint(Language.Translate("ui.lobby.error").Color(VColor.Red.RGBMultiplied(0.7f)).Bold());
                    return;
                }
                WriteLobbyRulesText(text);
                window?.CloseScreen();
            }
        };
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, titleWidget, inputField, confirmButton), new VVector2(0.5f, 1f), out _);
        return window;
    }

    public static string GetLobbyRulesText(bool isHost = false)
    {
        var path = GetLobbyRulesPath();
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return string.Empty;
        try
        {
            var content = System.IO.File.ReadAllText(path);
            return isHost ? content : content.Replace("\r", "\n");
        }
        catch { return string.Empty; }
    }

    public static void WriteLobbyRulesText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var path = GetLobbyRulesPath();

        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
        try { System.IO.File.WriteAllText(path, text); } catch { }
    }
}