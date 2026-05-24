using System.Reflection.Emit;

namespace DHMO.Patches;

public static class IgnoreBubble
{
    public static Func<GamePlayer, bool>? IsIgnore { get; set; }
}

[HarmonyPatch(typeof(Bubblegun.BubblegunBubble), nameof(Bubblegun.BubblegunBubble.OnUpdate))]
public static class BubblegunBubbleKillPatch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        var matcher = new CodeMatcher(instructions, generator);

        var getLocalPlayerMethod = AccessTools.PropertyGetter(typeof(GamePlayer), nameof(GamePlayer.LocalPlayer));
        var canKillMethod = AccessTools.Method(typeof(GamePlayer), nameof(GamePlayer.CanKill), [typeof(GamePlayer)]);

        matcher.MatchForward(false, new CodeMatch(OpCodes.Call, getLocalPlayerMethod), new CodeMatch(OpCodes.Stloc_S));

        if (!matcher.IsValid)
        {
            matcher.Start();
            matcher.MatchForward(false, new CodeMatch(OpCodes.Call, getLocalPlayerMethod), new CodeMatch(OpCodes.Stloc));
        }

        if (!matcher.IsValid)
        {
            return instructions;
        }

        var storeInstruction = matcher.InstructionAt(1);
        var localVarIndex = storeInstruction.operand;
        if (localVarIndex is LocalVariableInfo localVar)
            localVarIndex = localVar.LocalIndex;
        else if (localVarIndex is byte b)
            localVarIndex = (int)b;
        else if (localVarIndex is int i)
            localVarIndex = i;
        else
        {
            return instructions;
        }

        matcher.Start();
        matcher.MatchForward(true, new CodeMatch(OpCodes.Callvirt, canKillMethod));

        if (!matcher.IsValid)
        {
            return instructions;
        }

        var isIgnoreField = AccessTools.Field(typeof(IgnoreBubble), nameof(IgnoreBubble.IsIgnore));
        var funcInvoke = AccessTools.Method(typeof(Func<GamePlayer, bool>), "Invoke");

        CodeInstruction loadLocal;
        if ((int)localVarIndex <= 255)
            loadLocal = new CodeInstruction(OpCodes.Ldloc_S, (byte)(int)localVarIndex);
        else
            loadLocal = new CodeInstruction(OpCodes.Ldloc, (int)localVarIndex);

        matcher.Advance(1);

        var labelSkip = generator.DefineLabel();

        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Ldsfld, isIgnoreField),
            new CodeInstruction(OpCodes.Dup), new CodeInstruction(OpCodes.Brfalse_S, labelSkip),
            loadLocal,
            new CodeInstruction(OpCodes.Callvirt, funcInvoke),
            new CodeInstruction(OpCodes.Or),
            new CodeInstruction(OpCodes.Br_S, labelSkip));

        matcher.InstructionAt(matcher.Pos - 1).labels.Add(labelSkip);

        return matcher.InstructionEnumeration();
    }
}