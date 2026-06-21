namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
[NebulaRPCHolder]
public class LobbyRule : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public LobbyRule() => ModSingleton<LobbyRule>.Instance = this;
    static LobbyRule() => DIManager.Instance.RegisterModule(() => new LobbyRule());
    protected override void OnInjected(Game container) => this.Register(container);

    public static RemoteProcess<PlayerControl> RpcRequestRules = new("RequestRules", (requester, _) =>
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var rule = GetLobbyRulesText();
        RpcShowRules?.Invoke((PlayerControl.LocalPlayer, requester, rule));
    });

    public static RemoteProcess<(PlayerControl host, PlayerControl target, string rulesText)> RpcShowRules = new("ShowRules", (message, _) =>
    {
        if (!message.host.AmHost()) return;
        if (PlayerControl.LocalPlayer.PlayerId != message.target.PlayerId) return;
        OpenClientRuleScreen(HudManager.Instance.transform, message.rulesText);
    });

    private static MetaScreen? lastWindow;

    void OnUpdate(UpdateEvent _)
    {
        if (PreloadManager.FinishedPreload && ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.U)) && AmongUsClient.Instance != null && AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.NotJoined && !IsShown)
        {
            if (PlayerControl.LocalPlayer.AmHost())
                OpenHostRuleScreen(HudManager.Instance.transform);
            else
                RpcRequestRules.Invoke(PlayerControl.LocalPlayer);
        }
    }

    public static bool IsShown => lastWindow != null;

    static MetaScreen? OpenClientRuleScreen(Transform transform, string? rule)
    {
        if (string.IsNullOrEmpty(rule))
        {
            DebugScreen.Push(Language.Translate("lobby.rule.error"), 1f);
            return null;
        }
        var window = MetaScreen.GenerateWindow(new Vector2(7.5f, 4.6f), transform, new Vector3(0f, 0f, -200f), true, false, true, BackgroundSetting.Modern);
        lastWindow = window;

        var host = AmongUsClient.Instance.GetHost().Character;
        var hostName = $"<b>{Language.Translate("lobby.host")}</b>: <b>{host.name.Color(ModSingleton<DHMOGameManager>.Instance.MyContainer.GetColor(host.PlayerId).MainColor.ToUnityColor())}</b>";
        var titleWidget = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentTitle, Language.Translate("lobby.host.rule")), NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentStandard, hostName));

        var ruleWidget = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, titleWidget, NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentStandard, $"<size=125%>{rule}</size>"), NebulaAPI.GUI.VerticalMargin(0.15f));

        window.SetWidget(ruleWidget, new Vector2(0.5f, 1f), out _);
        return window;
    }

    static MetaScreen? OpenHostRuleScreen(Transform transform)
    {
        var window = MetaScreen.GenerateWindow(new Vector2(7.5f, 4.6f), transform, new Vector3(0f, 0f, -200f), true, false, true, BackgroundSetting.Modern);
        lastWindow = window;

        var host = AmongUsClient.Instance.GetHost().Character;
        var hostName = $"<b>{Language.Translate("lobby.host")}</b>: <b>{host.name.Color(ModSingleton<DHMOGameManager>.Instance.MyContainer.GetColor(host.PlayerId).MainColor.ToUnityColor())}</b>";
        var titleWidget = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentTitle, Language.Translate("lobby.host.rule")), NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentStandard, hostName));

        var inputField = new GUITextField(GUIAlignment.Center, new Virial.Compat.Size(7f, 3.25f))
        {
            IsSharpField = false,
            MaxLines = 16,
            FontSize = 1.4f,
            DefaultText = GetLobbyRulesText(true) ?? "",
            HintText = Language.Translate("ui.lobby.rule").Color(Color.gray)
        };

        var confirmButton = new GUIModernButton(GUIAlignment.Center, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.dialog.confirmation"))
        {
            OnClick = _ =>
            {
                var field = inputField.Artifact.FirstOrDefault();
                var text = field?.Text ?? "";

                if (string.IsNullOrEmpty(text))
                {
                    field?.SetHint(Language.Translate("ui.lobby.error").Color(Color.red.RGBMultiplied(0.7f)).Bold());
                    return;
                }

                WriteLobbyRulesText(text);
                window?.CloseScreen();
            }
        };

        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, titleWidget, inputField, confirmButton), new Vector2(0.5f, 1f), out _);

        return window;
    }

    public static string GetLobbyRulesText(bool isHost = false)
    {
        var dir = new DirectoryInfo(Application.dataPath).Parent?.FullName ?? Application.dataPath;
        if (string.IsNullOrEmpty(dir)) return string.Empty;
        var path = Path.Combine(dir, "LobbyRules.txt");
        if (!System.IO.File.Exists(path)) return string.Empty;
        try
        {
            string fileContent = System.IO.File.ReadAllText(path);
            if (!isHost)
            {
                return fileContent.Replace("\r", "\n");
            }
            else
                return fileContent;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void WriteLobbyRulesText(string text)
    {
        if (text == null || text == string.Empty) return;
        var dir = new DirectoryInfo(Application.dataPath).Parent?.FullName ?? Application.dataPath;
        if (string.IsNullOrEmpty(dir)) return;
        var path = Path.Combine(dir, "LobbyRules.txt");
        if (!System.IO.File.Exists(path)) return;
        try
        {
            System.IO.File.WriteAllText(path, text);
        }
        catch
        {
            return;
        }
    }
}