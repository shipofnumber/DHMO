namespace DHMO.Patches;

[HarmonyPatch(typeof(ModAbilityButtonImpl))]
public static class ModAbilityButtonUpdatePatch
{
    [HarmonyPatch(nameof(ModAbilityButtonImpl.OnHudUpdate))]
    [HarmonyPrefix]
    public static bool HudUpdatePrefix(ModAbilityButtonImpl __instance)
    {
        var localPlayer = GamePlayer.LocalPlayer;
        if (localPlayer == null) return true;

        if (!localPlayer.TryGetAbility<Overclocker.Ability>(out var ability) ||
            (ability.killButton != __instance && ability.overTimeButton != __instance)) return true;
        
        __instance.UpdateVisibility();

        __instance.OnUpdate?.Invoke(__instance);

        if (__instance is { EffectActive: true, EffectTimer: not { IsProgressing: true } }) __instance.InactivateEffect();

        __instance.VanillaButton.SetCooldownFill(__instance.CurrentTimer?.Percentage ?? 0f);

        string timerText = __instance.CurrentTimer?.TimerText ?? "";
        __instance.cooldownTextObserver.Set(timerText);
        __instance.cooldownTextColorObserver.Set(__instance.EffectActive);

        if ((__instance.keyCode?.KeyDownInGame ?? false) || (__instance.canUseByMouseClick && __instance.CheckMouseClick() && !AmongUsUtil.UsingMouseMovement)) __instance.DoClick();
        if (__instance.subKeyCode?.KeyDownInGame ?? false) __instance.DoSubClick();

        return false;

    }
}