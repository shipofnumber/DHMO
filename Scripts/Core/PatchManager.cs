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

using InnerNet;
using Virial.Runtime;

namespace DHMO.Core;

// Made by Plana, later maintained and updated by Water.
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public class PatchManager
{
    internal static Harmony? harmony = new("DHMO");
    public static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor;
        NebulaGameEnd.RegisterWinCondTip(Raven.Instance.RavenTeamWin, () => ((ISpawnable)Raven.MyRole).IsSpawnable, "raven", null);
    }

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
}

[NebulaRPCHolder]
public class DHMOGameManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static DHMOGameManager() => DIManager.Instance.RegisterModule(() => new DHMOGameManager());
    public DHMOGameManager() => ModSingleton<DHMOGameManager>.Instance = this;

    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);

    private const float EffectDuration = 1.42857146f;
    private static UnityEngine.Vector3 EffectOffset = new(0f, 0f, -0.1f);

    void OnRoleChanged(PlayerRoleSetEvent ev)
    {
        var game = NebulaGameManager.Instance;
        var local = GamePlayer.LocalPlayer;

        if (game == null || local == null || (local != ev.Player && !game.CanSeeAllInfo)) return;

        NebulaManager.Instance.ScheduleDelayAction(() => AnimationEffects.CoPlayRoleNameEffect(ev.Player).StartOnScene());
    }

    void OnPlayerJoin(PlayerJoinEvent ev)
    {
        if (TutorialManager.InstanceExists) return;
        AmongUsClient.Instance.StartCoroutine(CoSendRulesToPlayer(ev.ClientData).WrapToIl2Cpp());
    }

    static IEnumerator CoSendRulesToPlayer(ClientData clientData)
    {
        while (AmongUsClient.Instance == null || PlayerControl.LocalPlayer?.Data == null)
            yield return null;

        yield return new WaitForSecondsRealtime(1f);

        if (!PlayerControl.LocalPlayer.AmHost()) yield break;

        var joiningPlayer = clientData.Character;
        if (joiningPlayer == null) yield break;

        yield return new WaitForSecondsRealtime(0.5f);

        var rulesText = PatchManager.GetLobbyRulesText();
        if (string.IsNullOrWhiteSpace(rulesText)) yield break;

        ChatCommandPatch.RpcSendLobbyRules.Invoke((PlayerControl.LocalPlayer, joiningPlayer, rulesText));
    }
}