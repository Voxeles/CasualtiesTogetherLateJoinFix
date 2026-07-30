using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherLateJoinFix;

internal static class Patches
{
    [HarmonyPatch(typeof(ClientMain))]
    internal static class ClientMainPatches
    {
        [HarmonyPatch(nameof(ClientMain.ClientReceiver__SingleCharacterPositionsSync))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SingleCharacterPositionsSyncReceiverPatch(
            IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(true,
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldloca_S),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(NetBody), nameof(NetBody.TryGetNetBodyFromId))),
                    new CodeMatch(OpCodes.Brfalse))
                .ThrowIfInvalid("SingleCharacterPositionsSyncReceiverPatch: Could not find a match!");
            var label = codeMatcher.Operand;
            codeMatcher
                .Advance(codeMatcher.Remaining - 1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
                .Insert(
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Call, 
                        AccessTools.Method(typeof(Plugin), nameof(Plugin.HandleMissingPlayerForNetBody))))
                .Labels.Add((Label)label);
            codeMatcher.Advance(3).Labels = [];
            Plugin.Logger.LogInfo("Transpiled method ClientReceiver__SingleCharacterPositionsSync...");
            return codeMatcher.InstructionEnumeration();
        }
    }
}