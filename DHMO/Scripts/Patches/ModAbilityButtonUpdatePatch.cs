namespace DHMO.Patches;

[HarmonyPatch(typeof(ModAbilityButtonImpl), nameof(ModAbilityButtonImpl.OnHudUpdate))]
public static class ModAbilityButtonUpdatePatch
{
    public static bool Prefix(ModAbilityButtonImpl __instance)
    {
        if (GamePlayer.LocalPlayer != null && GamePlayer.LocalPlayer.TryGetAbility<Overclocker.Ability>(out var ability) && (ability.killButton == __instance || ability.outTimeButton == __instance))
        {
            __instance.UpdateVisibility();

            __instance.OnUpdate?.Invoke(__instance);

            if (__instance.EffectActive && (__instance.EffectTimer == null || !__instance.EffectTimer.IsProgressing)) __instance.InactivateEffect();

            __instance.VanillaButton.SetCooldownFill(__instance.CurrentTimer?.Percentage ?? 0f);

            string timerText = __instance.CurrentTimer?.TimerText ?? "";
            __instance.cooldownTextObserver.Set(timerText);
            __instance.cooldownTextColorObserver.Set(__instance.EffectActive);

            if ((__instance.keyCode?.KeyDownInGame ?? false) || (__instance.canUseByMouseClick && __instance.CheckMouseClick() && !AmongUsUtil.UsingMouseMovement)) __instance.DoClick();
            if (__instance.subKeyCode?.KeyDownInGame ?? false) __instance.DoSubClick();

            return false;
        }

        return true;
    }
}