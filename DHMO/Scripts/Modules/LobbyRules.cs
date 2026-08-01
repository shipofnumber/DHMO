using Virial.Runtime;

namespace DHMO.Modules;

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public class LobbyRules
{
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Building Lobby Rules Module");

        NebulaManager.commands.Add(new NebulaManager.MetaCommand("help.command.showRule",
                     () => NebulaAPI.CurrentGame != null && !LobbyRules.IsShown,
                     () =>
                     {
                         if (!AmongUsLLImpl.TryGetAmongUsClientInstance(out var auClient)) return;

                         var localPlayerId = AmongUsLLImpl.LocalPlayer.PlayerId;
                         if (!auClient.AmHost || auClient.GameState == InnerNet.InnerNetClient.GameStates.Started)
                         {
                             LobbyRules.RpcRequestRules.Invoke(localPlayerId);
                             return;
                         }

                         string[] files = Directory.GetFiles(LobbyRules.directory, "*.txt", SearchOption.AllDirectories);
                         string? firstFile = files.FirstOrDefault();

                         if (files.Length > 1)
                             LobbyRules.CreateSelectFileScreen();
                         else
                         {
                             LobbyRules.currentPath = firstFile;
                             LobbyRules.selectedPath = firstFile;
                             LobbyRules.OpenHostRuleScreen();
                         }
                     })
        { DefaultKeyInput = new(KeyCode.F6), });
    }

    static LobbyRules()
    {
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var files = Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories);

        if (!System.IO.File.Exists(setting))
            System.IO.File.WriteAllText(setting, "");

        if (files.Length == 0)
        {
            if (AmongUs.Data.DataManager.Settings.Language.CurrentLanguage != SupportedLangs.SChinese) return;

            var stream = NebulaAPI.AddonAsset.GetResource("LobbyRule.txt")?.AsStream()!;
            using var reader = new StreamReader(stream, Encoding.GetEncoding("utf-8"));
            string text = reader.ReadToEnd();

            string ruleFile = Path.Combine(directory, "LobbyRule.txt");
            selectedPath = ruleFile;

            System.IO.File.WriteAllText(setting, ruleFile);
            System.IO.File.WriteAllText(ruleFile, text);
        }
        else
        {
            var path = System.IO.File.ReadAllText(setting);
            if (System.IO.File.Exists(path))
                selectedPath = path;
            else
                selectedPath = files.FirstOrDefault();
        }
    }

    internal static string setting = Path.Combine(Path.Combine(BepInEx.Paths.GameRootPath, "LobbyRules"), "setting.dat");
    internal static string directory = Path.Combine(BepInEx.Paths.GameRootPath, "LobbyRules");
    private static MetaScreen? lastWindow;
    internal static bool IsShown => lastWindow.AsBoolFast();
    internal static string? currentPath = "";
    internal static string? selectedPath = "";

    public static RemoteProcess<byte> RpcRequestRules = new("RequestRules", (requester, _) =>
    {
        if (!AmongUsLLImpl.AmongUsClientInstance.AmHost) return;
        RpcShowRules?.Invoke((AmongUsLLImpl.LocalPlayer.OwnerId, requester, GetLobbyRulesText(selectedPath), Path.GetFileNameWithoutExtension(selectedPath)));
    });

    public static RemoteProcess<(int host, byte target, string rulesText, string title)> RpcShowRules = new("ShowRules", (message, _) =>
    {
        if (AmongUsLLImpl.AmongUsClientInstance.HostId != message.host || AmongUsLLImpl.LocalPlayer.PlayerId != message.target) return;
        LobbyRules.OpenClientRuleScreen(message.rulesText, message.title);
    });

    static RemoteProcess<(string key, string name)> RpcSetLobbyRulesMessage = new("SetLobbyRulesMessage", (message, _) =>
    {
        var notifier = AmongUsLLImpl.HudManagerInstance.Notifier;
        var key = message.key.Sum(c => c);
        var text = $"<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">{Language.Translate("ui.lobbyRules.selectFileSuccessfully").Replace("%FILE%", message.name.Color(VColor.CrewmateColor))}</font>";

        if (notifier.lastMessageKey == key && notifier.activeMessages.Count > 0)
            notifier.activeMessages[^1].UpdateMessage(text);
        else
        {
            notifier.lastMessageKey = key;
            LobbyNotificationMessage newMessage = UnityEngine.Object.Instantiate(notifier.notificationMessageOrigin, VVector3.Zero, Quaternion.identity, notifier.transform);
            newMessage.transform.localPosition = new VVector3(0f, 0f, -2f);
            var action = () => notifier.OnMessageDestroy(newMessage);
            newMessage.SetUp(text, notifier.settingsChangeSprite, notifier.settingsChangeColor, action);
            notifier.ShiftMessages();
            notifier.AddMessageToQueue(newMessage);
        }
        AmongUsLLImpl.SoundManagerInstance.PlaySoundImmediate(notifier.settingsChangeSound, false, 1f, 1f);
    });

    private static (MetaScreen window, GUIWidget titleWidget) CreateRuleWindow(string? title)
    {
        var window = MetaScreen.GenerateWindow(new VVector2(7.5f, 4.6f), AmongUsLLImpl.HudManagerInstance.transform, new VVector3(0f, 0f, -200f), true, false, true, BackgroundSetting.Modern);
        lastWindow = window;
        var host = AmongUsLLImpl.AmongUsClientInstance.GetHost().Character;
        var hostName = $"<b>{Language.Translate("ui.lobbyRules.host")}</b>: <b>{host.name}</b>";

        var titleWidget = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentTitle, title ?? Language.Translate("ui.lobbyRules.noTitle")),
            NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentBold, hostName));

        return (window, titleWidget);
    }

    internal static MetaScreen? OpenClientRuleScreen(string? rule, string? title)
    {
        if (string.IsNullOrEmpty(rule)) { DebugScreen.Push(Language.Translate("ui.lobbyRules.noTextError"), 1f); return null; }

        var (window, titleWidget) = CreateRuleWindow(title);

        var scrollView = new GUIScrollView(GUIAlignment.Center, new(7.4f, 4.1f - 0.9f), NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, titleWidget, NebulaAPI.GUI.VerticalMargin(0.15f),
            NebulaAPI.GUI.RawText(GUIAlignment.Center, AttributeAsset.DocumentStandard, $"<size=130%>{rule}</size>")));
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, scrollView), new VVector2(0.5f, 1f), out _);
        return window;
    }

    internal static MetaScreen? OpenHostRuleScreen()
    {
        var (window, titleWidget) = CreateRuleWindow(Path.GetFileNameWithoutExtension(currentPath));

        var button = new GUIModernButton(Virial.Media.GUIAlignment.TopLeft, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.lobbyRules.return"))
        {
            OnClick = _ =>
            {
                window.CloseScreen();
                CreateSelectFileScreen();
            }
        };

        var inputField = new GUITextField(GUIAlignment.Center, new Virial.Compat.Size(6.5f, 7f))
        {
            IsSharpField = false,
            MaxLines = 25,
            FontSize = 1.7f,
            DefaultText = GetLobbyRulesText(currentPath, true) ?? "",
            HintText = Language.Translate("ui.lobbyRules.rule").Color(VColor.Gray)
        };
        var confirmButton = new GUIModernButton(GUIAlignment.TopRight, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.lobbyRules.confirmation"))
        {
            OnClick = _ =>
            {
                var field = inputField.Artifact.FirstOrDefault();
                var text = field?.Text ?? "";
                if (string.IsNullOrEmpty(text))
                {
                    field?.SetHint(Language.Translate("ui.lobbyRules.fieldNoText").Color(VColor.Red.RGBMultiplied(0.7f)).Bold());
                    return;
                }
                WriteLobbyRulesText(currentPath, text);
                window?.CloseScreen();
                CreateSelectFileScreen();
            }
        };

        var scrollView = new GUIScrollView(GUIAlignment.Center, new(7.4f, 4.1f), NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Top, button, confirmButton), titleWidget, inputField));
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, scrollView), new VVector2(0.5f, 1f), out _);
        return window;
    }

    internal static MetaScreen CreateSelectFileScreen()
    {
        var window = MetaScreen.GenerateWindow(new(7.5f, 4.6f), AmongUsLLImpl.HudManagerInstance.transform, new(0f, 0f, -200f), true, false, background: BackgroundSetting.Modern);
        lastWindow = window;

        List<Virial.Media.GUIWidget> widgets = [];
        string[] txtPath = Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories);

        foreach (var path in txtPath)
        {
            var name = Path.GetFileNameWithoutExtension(path);

            widgets.Add(NebulaAPI.GUI.HorizontalHolder(Virial.Media.GUIAlignment.Center,
        new NoSGUIFramed(GUIAlignment.Left,NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, NebulaAPI.GUI.HorizontalMargin(5.75f), NebulaAPI.GUI.RawText(GUIAlignment.Left, AttributeAsset.DocumentBold, name)),
        new(0.1f, 0.1f), new(0.3f, 0.3f, 0.3f, 0.2f))
        {
            OnClicked = () =>
            {
                currentPath = path;
                window.CloseScreen();
                OpenHostRuleScreen();
            }
        }
        , new GUIModernButton(Virial.Media.GUIAlignment.Right, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.lobbyRules.confirmation"))
        {
            OnClick = clickable =>
            {
                selectedPath = path;
                WriteLobbyRulesText(setting, path);
                window.CloseScreen();
                RpcSetLobbyRulesMessage.Invoke(("SendLobbyRulesMessage", name));
            },
        }));
        }

        var scrollView = new GUIScrollView(GUIAlignment.Center, new(7.4f, 4.1f - 0.9f), NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, widgets));
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, scrollView), new VVector2(0.5f, 1f), out _);
        return window;
    }

    public static string GetLobbyRulesText(string? path, bool isHost = false)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return string.Empty;

        var content = System.IO.File.ReadAllText(path);
        return isHost ? content : content.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static void WriteLobbyRulesText(string? path, string text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(path)) return;
        System.IO.File.WriteAllText(path, text);
    }
}