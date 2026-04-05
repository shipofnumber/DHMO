namespace DHMO.Patches;

/*[HarmonyPatch]
public static class KeyAssignmentPatch
{
    internal static Func<KeyCode> GetModKeyCodeGetter(string translationKey, KeyCode defaultKey)
    {
        KeyAssignment assignment = new(translationKey, defaultKey);
        return () => assignment.KeyInput;
    }
    static KeyAssignmentPatch()
    {
        passBomb = new VirtualInput(GetModKeyCodeGetter("passAction", KeyCode.Space));
    }
    public static readonly VirtualInput passBomb;

    [HarmonyPatch(typeof(NebulaInput), nameof(NebulaInput.GetInput))]
    [HarmonyPrefix]
    public static bool GetInputPatch(VirtualKeyInput type, ref VirtualInput __result)
    {
        if (type == (VirtualKeyInput)120)
        {
            __result = passBomb;
            return false;
        }
        return true;
    }
}*/