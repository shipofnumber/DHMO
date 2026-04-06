global using BepInEx.Unity.IL2CPP.Utils;
global using BepInEx.Unity.IL2CPP.Utils.Collections;
global using DHMO.Core;
global using DHMO.Patches;
global using DHMO.Roles;
global using HarmonyLib;
global using Hazel;
global using Il2CppSystem.Threading.Tasks;
global using Nebula;
global using Nebula.Behavior;
global using Nebula.Configuration;
global using Nebula.Extensions;
global using Nebula.Game;
global using Nebula.Game.Statistics;
global using Nebula.Modules;
global using Nebula.Modules.GUIWidget;
global using Nebula.Modules.ScriptComponents;
global using Nebula.Player;
global using Nebula.Roles;
global using Nebula.Roles.Abilities;
global using Nebula.Roles.Crewmate;
global using Nebula.Roles.Impostor;
global using Nebula.Roles.Modifier;
global using Nebula.Roles.Neutral;
global using Nebula.Utilities;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Security.Cryptography;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using TMPro;
global using UnityEngine;
global using Virial;
global using Virial.Assignable;
global using Virial.Attributes;
global using Virial.Compat;
global using Virial.Components;
global using Virial.Configuration;
global using Virial.DI;
global using Virial.Events.Game;
global using Virial.Events.Game.Meeting;
global using Virial.Events.Player;
global using Virial.Events.Role;
global using Virial.Game;
global using Virial.Media;
global using Virial.Text;
global using Virial.Utilities;
global using GamePlayer = Virial.Game.Player;
using Cpp2IL.Core.Utils;
using Virial.Runtime;
using static Il2CppSystem.Net.Http.Headers.Parser;
using static UnityEngine.GraphicsBuffer;
using Color = UnityEngine.Color;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace DHMO.Core;

// Made by Plana, later maintained and updated by Water.
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class PatchManager
{
    internal static Harmony? harmony = new("DHMO");
    static PatchManager()
    {
        harmony?.PatchAll();
    }

    internal static string GetLobbyRulesText()
    {
        var dir = new DirectoryInfo(Application.dataPath).Parent?.FullName ?? Application.dataPath;
        if (string.IsNullOrEmpty(dir)) return string.Empty;
        var path = Path.Combine(dir, "LobbyRules.txt");
        if (!System.IO.File.Exists(path)) return string.Empty;
        try
        {
            return System.IO.File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static void WriteLobbyRulesText(string text)
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

[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    private static MetaScreen? lastWindow;

    public static RemoteProcess<PlayerControl> RpcRequestRules = new("RequestRules", (requester, _) =>
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var rule = PatchManager.GetLobbyRulesText();
        RpcShowRules?.Invoke((PlayerControl.LocalPlayer, requester, rule));
    });
    public static RemoteProcess<(PlayerControl host, PlayerControl target, string rulesText)> RpcShowRules = new("ShowRules", (message, _) =>
    {
        if (!message.host.AmHost()) return;
        if (PlayerControl.LocalPlayer.PlayerId != message.target.PlayerId) return;
        OpenClientRuleScreen(HudManager.Instance.transform, message.rulesText);
    });

    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;

    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;
        NebulaGameEnd.RegisterWinCondTip(Raven.Instance.RavenTeamWin, () => ((ISpawnable)Raven.MyRole).IsSpawnable, "raven", null);
    }

    protected override void OnInjected(Game container) => this.Register(container);

    void OnRoleChanged(PlayerRoleSetEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game == null || local == null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player).StartOnScene());
    }

    void OnUpdate(UpdateEvent _)
    {
        if (PreloadManager.FinishedPreload && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.L) && AmongUsClient.Instance != null && AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.NotJoined)
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
        if (lastWindow != null) return null;

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
        if (lastWindow != null) return null;

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
            DefaultText = PatchManager.GetLobbyRulesText() ?? "",
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

                PatchManager.WriteLobbyRulesText(text);
                window?.CloseScreen();
            }
        };

        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, titleWidget, inputField, confirmButton), new Vector2(0.5f, 1f), out _);

        return window;
    }

    void OnPlayerJoin(PlayerJoinEvent ev)
    {
        if (TutorialManager.InstanceExists || ev.ClientData.Character.AmHost()) return;
        AmongUsClient.Instance.StartCoroutine(CoOpenRuleScreen().WrapToIl2Cpp());
    }

    static IEnumerator CoOpenRuleScreen()
    {
        const float maxWaitTotal = 15f;
        float elapsedTime = 0f;

        while (elapsedTime < maxWaitTotal)
        {
            var client = AmongUsClient.Instance;
            var localPlayer = PlayerControl.LocalPlayer;
            bool isCoreReady = client != null && client && localPlayer != null && localPlayer.Data != null;

            if (isCoreReady) break;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (elapsedTime >= maxWaitTotal) yield break;

        elapsedTime = 0f;
        UncertifiedPlayer? localCertification = null;
        while (elapsedTime < maxWaitTotal)
        {
            localCertification = PlayerControl.LocalPlayer?.gameObject.GetComponent<UncertifiedPlayer>();

            bool isCertified = localCertification == null || localCertification.State != UncertifiedReason.Waiting && localCertification.State != UncertifiedReason.Uncertified;

            if (isCertified) break;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);

        if (PlayerControl.LocalPlayer != null) RpcRequestRules.Invoke(PlayerControl.LocalPlayer);
    }
}