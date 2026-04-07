using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;

namespace LumaPlayerLimit.Patches
{
    internal static class SteamLobbyController
    {
        public static void Patch(Harmony harmony)
        {
            var steamLobbyControllerType = AccessTools.TypeByName("SteamLobbyController");
            if (steamLobbyControllerType == null)
            {
                Plugin.Log.LogWarning("SteamLobbyController type not found; skipping lobby controller patches.");
                return;
            }

            Core.PatchNamedMethod(
                harmony,
                steamLobbyControllerType,
                "CreateLobby",
                transpiler: AccessTools.Method(typeof(SteamLobbyController), nameof(CreateLobby_Transpiler)));

            Core.PatchNamedMethod(
                harmony,
                steamLobbyControllerType,
                "HostUpdatePlayerCount",
                prefix: AccessTools.Method(typeof(SteamLobbyController), nameof(HostUpdatePlayerCount_Prefix)));

            Core.PatchNamedMethod(
                harmony,
                steamLobbyControllerType,
                "OnReceiveLobbyData",
                transpiler: AccessTools.Method(typeof(SteamLobbyController), nameof(OnReceiveLobbyData_Transpiler)));
        }

        private static bool HostUpdatePlayerCount_Prefix(object __instance, int playerCount)
        {
            try
            {
                var hasCreatedLobby = Reflect.GetFieldBool(__instance, "HasCreatedLobby", false);
                var isLobbyOwner = Reflect.GetFieldBool(__instance, "isLobbyOwner", false);

                if (!hasCreatedLobby || !isLobbyOwner)
                {
                    return false;
                }

                var lobbyId = Reflect.GetFieldValue(__instance, "lobbyID");
                if (lobbyId == null)
                {
                    Plugin.Log.LogWarning("SteamLobbyController.HostUpdatePlayerCount: lobbyID was null.");
                    return false;
                }

                var stateAllowsJoining = Reflect.GetFieldBool(__instance, "State", false);
                var isFull = playerCount >= Plugin.GetConfiguredMaxPlayers();

                if (stateAllowsJoining)
                {
                    Reflect.InvokeStaticIfExists("Steamworks.SteamMatchmaking", "SetLobbyJoinable", lobbyId, !isFull);
                }

                Reflect.InvokeStaticIfExists("Steamworks.SteamMatchmaking", "SetLobbyData", lobbyId, "numPlayers", playerCount.ToString());
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(string.Format("SteamLobbyController_HostUpdatePlayerCount_Prefix failed: {0}", ex));
                return true;
            }
        }

        private static readonly Dictionary<string, string> KnownSignatures = new Dictionary<string, string>
        {
            { "SteamLobbyController_CreateLobby", "Ldloc Ldloc Ldc_I4 Callvirt Ldc_I4 Call Callvirt Callvirt" },
            { "SteamLobbyController_OnReceiveLobbyData", "Ldarg Ldc_I4 Clt Brfalse Ldloc Call Ldc_I4 Cgt Brtrue" },
        };

        private static string GetTranspilerKey(MethodBase method)
        {
            if (method == null) return null;
            var declaringType = method.DeclaringType?.Name ?? "";
            return $"{declaringType}_{method.Name}";
        }

        private static string ComputeCurrentSignature(IEnumerable<CodeInstruction> instructions, int sampleSize = 20)
        {
            return TranspilerHelpers.ComputeILSignature(instructions.ToList(), sampleSize);
        }

        private static bool CheckSignatureAndWarn(string key, IEnumerable<CodeInstruction> instructions, float threshold = 0.7f)
        {
            if (!KnownSignatures.TryGetValue(key, out var knownSig))
            {
                Plugin.Log.LogInfo($"No known signature stored for {key}; storing baseline from this run.");
                KnownSignatures[key] = ComputeCurrentSignature(instructions);
                return true;
            }

            var currentSig = ComputeCurrentSignature(instructions);
            var similarity = TranspilerHelpers.SignatureSimilarity(knownSig, currentSig);
            if (similarity < threshold)
            {
                Plugin.Log.LogWarning($"IL signature for {key} differs from known pattern (similarity={similarity:P0}). Game may have updated. Attempting patch anyway...");
                return true;
            }

            return true;
        }

        private static void UpdateStoredSignature(string key, IEnumerable<CodeInstruction> instructions)
        {
            KnownSignatures[key] = ComputeCurrentSignature(instructions);
        }

        private static bool ValidateReplacement(IList<CodeInstruction> codes, int position, int expectedStackBefore)
        {
            if (!TranspilerHelpers.ValidateStackTransition(codes, position, expectedStackBefore))
            {
                Plugin.Log.LogError($"Stack depth mismatch at position {position} after replacement. Skipping transpiler.");
                return false;
            }
            return true;
        }

        private static IEnumerable<CodeInstruction> CreateLobby_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var codes = instructions.ToList();
            var key = GetTranspilerKey(method);
            CheckSignatureAndWarnVerbose(key, codes);

            var getter = AccessTools.Method(typeof(Plugin), nameof(Plugin.GetConfiguredMaxPlayers));
            var positions = TranspilerHelpers.FindLoadIntPositions(codes, 4);

            Plugin.Log.LogInfo($"CreateLobby transpiler: found {positions.Count} ldc.i4 4 positions.");

            if (positions.Count == 0)
            {
                Plugin.Log.LogWarning("CreateLobby transpiler: no ldc.i4 4 instructions found in method.");
                return codes;
            }

            var replacementCount = 0;
            var strategyUsed = "none";
            var usedPositions = new HashSet<int>();

            for (var idx = 0; idx < positions.Count; idx++)
            {
                var i = positions[idx];
                if (usedPositions.Contains(i)) continue;

                for (var j = i + 1; j < Math.Min(i + 4, codes.Count); j++)
                {
                    if (TranspilerHelpers.CallsMethodNamed(codes[j], "CreateLobby") ||
                        TranspilerHelpers.CallsMethodNamed(codes[j], "SetLobbyData"))
                    {
                        codes[i] = TranspilerHelpers.CloneAsCall(codes[i], getter);
                        usedPositions.Add(i);
                        replacementCount++;
                        strategyUsed = "A";
                        Plugin.Log.LogInfo($"  [{i}] Replaced: ldc.i4 4 followed by {codes[j].operand} at +{j - i}");
                        break;
                    }
                    if (TranspilerHelpers.IsLoadInt(codes[j], 4))
                    {
                        break;
                    }
                }
            }

            if (replacementCount == 0)
            {
                for (var idx = 0; idx < positions.Count; idx++)
                {
                    var i = positions[idx];
                    if (usedPositions.Contains(i)) continue;

                    for (var j = i + 1; j < Math.Min(i + 6, codes.Count); j++)
                    {
                        var op = codes[j].opcode;
                        if (op == OpCodes.Clt || op == OpCodes.Cgt || op == OpCodes.Ceq ||
                            op == OpCodes.Brtrue || op == OpCodes.Brfalse || op == OpCodes.Ret)
                        {
                            codes[i] = TranspilerHelpers.CloneAsCall(codes[i], getter);
                            usedPositions.Add(i);
                            replacementCount++;
                            strategyUsed = "B";
                            Plugin.Log.LogInfo($"  [{i}] Replaced with fallback B (comparison at +{j - i}). Stack effect preserved.");
                            break;
                        }
                    }
                    if (replacementCount > 0) break;
                }
            }

            if (replacementCount == 0)
            {
                Plugin.Log.LogError($"CreateLobby transpiler: found {positions.Count} constant '4' but no pattern matched. Game may have updated.");
            }
            else
            {
                var firstReplacementPos = FindFirstReplacementPosition(codes, getter);
                Plugin.Log.LogInfo($"CreateLobby transpiler: replaced {replacementCount} lobby-cap constant(s) using strategy {strategyUsed}. First at pos {firstReplacementPos}");
                UpdateStoredSignature(key, codes);
            }

            return codes;
        }

        private static int TryReplaceInRange(IList<CodeInstruction> codes, List<int> positions, MethodInfo getter, int maxAhead, bool requireDirectNext, Func<int, bool> matchFunc)
        {
            var replaced = 0;
            var usedPositions = new HashSet<int>();

            for (var idx = 0; idx < positions.Count; idx++)
            {
                var i = positions[idx];
                if (usedPositions.Contains(i)) continue;

                var limit = Math.Min(i + maxAhead + 1, codes.Count);
                for (var j = i + 1; j < limit; j++)
                {
                    if (requireDirectNext && j != i + 1) break;
                    if (TranspilerHelpers.IsLoadInt(codes[j], 4)) break;

                    var found = matchFunc(j);
                    if (!found) continue;
                    if (j > i + 1 && codes[j - 1].opcode == OpCodes.Conv_I1)
                    {
                        var beforeDepth = TranspilerHelpers.ComputeStackDepthAt(codes, i);
                        codes[i] = TranspilerHelpers.CloneAsCall(codes[i], getter);
                        if (!ValidateReplacement(codes, i, beforeDepth)) return replaced;
                        usedPositions.Add(i);
                        replaced++;
                        break;
                    }
                    else
                    {
                        var beforeDepth = TranspilerHelpers.ComputeStackDepthAt(codes, i);
                        codes[i] = TranspilerHelpers.CloneAsCall(codes[i], getter);
                        if (!ValidateReplacement(codes, i, beforeDepth)) return replaced;
                        usedPositions.Add(i);
                        replaced++;
                        break;
                    }
                }
            }

            return replaced;
        }

        private static int TryReplaceFirstOccurrence(IList<CodeInstruction> codes, List<int> positions, MethodInfo getter, int searchAhead)
        {
            for (var idx = 0; idx < positions.Count; idx++)
            {
                var i = positions[idx];
                var limit = Math.Min(i + searchAhead, codes.Count);
                for (var j = i; j < limit; j++)
                {
                    var op = codes[j].opcode;
                    if (op == OpCodes.Clt || op == OpCodes.Cgt || op == OpCodes.Ceq ||
                        op == OpCodes.Brtrue || op == OpCodes.Brfalse || op == OpCodes.Ret)
                    {
                        var beforeDepth = TranspilerHelpers.ComputeStackDepthAt(codes, i);
                        codes[i] = TranspilerHelpers.CloneAsCall(codes[i], getter);
                        if (!ValidateReplacement(codes, i, beforeDepth)) return 0;
                        return 1;
                    }
                }
            }
            return 0;
        }

        private static int FindFirstReplacementPosition(IList<CodeInstruction> codes, MethodInfo getter)
        {
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && Equals(codes[i].operand, getter))
                {
                    return i;
                }
            }
            return -1;
        }

        private static readonly HashSet<OpCode> ComparisonOps = new HashSet<OpCode>
        {
            OpCodes.Clt, OpCodes.Cgt, OpCodes.Ceq, OpCodes.Clt_Un, OpCodes.Cgt_Un
        };

        private static readonly HashSet<OpCode> BranchOps = new HashSet<OpCode>
        {
            OpCodes.Br, OpCodes.Brfalse, OpCodes.Brtrue,
            OpCodes.Beq, OpCodes.Bne_Un,
            OpCodes.Bge, OpCodes.Bgt, OpCodes.Ble, OpCodes.Blt, OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un
        };

        private static bool IsComparisonWithin(IList<CodeInstruction> codes, int start, int maxAhead, out int foundAt)
        {
            foundAt = -1;
            for (var j = start + 1; j < Math.Min(start + maxAhead + 1, codes.Count); j++)
            {
                var op = codes[j].opcode;
                if (ComparisonOps.Contains(op))
                {
                    foundAt = j;
                    return true;
                }
                if (BranchOps.Contains(op) || op == OpCodes.Ret)
                {
                    break;
                }
                if (op == OpCodes.Pop || op == OpCodes.Stloc || op == OpCodes.Stloc_0 ||
                    op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3 ||
                    op == OpCodes.Stloc_S || op == OpCodes.Starg || op == OpCodes.Starg_S)
                {
                    break;
                }
            }
            return false;
        }

        private static bool IsArithmeticOp(OpCode op)
        {
            return op == OpCodes.Add || op == OpCodes.Sub || op == OpCodes.Mul ||
                   op == OpCodes.Div || op == OpCodes.Rem || op == OpCodes.And ||
                   op == OpCodes.Or || op == OpCodes.Xor;
        }

        private static bool WouldReplacementBreakExpression(IList<CodeInstruction> codes, int pos)
        {
            for (var j = pos + 1; j < Math.Min(pos + 5, codes.Count); j++)
            {
                var op = codes[j].opcode;
                if (ComparisonOps.Contains(op) || BranchOps.Contains(op) || op == OpCodes.Ret)
                {
                    return false;
                }
                if (IsArithmeticOp(op))
                {
                    return true;
                }
                if (op == OpCodes.Pop || op == OpCodes.Stloc || op == OpCodes.Starg)
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> OnReceiveLobbyData_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var codes = instructions.ToList();
            var key = GetTranspilerKey(method);
            var signatureChanged = CheckSignatureAndWarnVerbose(key, codes);
            var getter = AccessTools.Method(typeof(Plugin), nameof(Plugin.GetConfiguredMaxPlayers));
            var positions = TranspilerHelpers.FindLoadIntPositions(codes, 4);

            Plugin.Log.LogInfo($"OnReceiveLobbyData transpiler: found {positions.Count} ldc.i4 4 positions. Signature changed: {signatureChanged}");

            if (positions.Count == 0)
            {
                Plugin.Log.LogWarning("OnReceiveLobbyData transpiler: no ldc.i4 4 instructions found in method.");
                return codes;
            }

            var replaced = 0;
            var strategyUsed = "none";
            var usedPositions = new HashSet<int>();

            for (var idx = 0; idx < positions.Count; idx++)
            {
                var i = positions[idx];
                if (usedPositions.Contains(i)) continue;

                if (WouldReplacementBreakExpression(codes, i))
                {
                    Plugin.Log.LogInfo($"  [{i}] Skipping: ldc.i4 4 appears to be part of an arithmetic expression.");
                    continue;
                }

                int cmpAt;
                if (IsComparisonWithin(codes, i, 3, out cmpAt))
                {
                    codes[i] = TranspilerHelpers.CloneAsCall(codes[i], getter);
                    usedPositions.Add(i);
                    replaced++;
                    strategyUsed = "A";
                    Plugin.Log.LogInfo($"  [{i}] Replaced with strategy A (comparison at +{cmpAt - i}). Stack effect preserved: both ldc.i4 4 and GetConfiguredMaxPlayers push 1.");
                }
            }

            if (replaced == 0)
            {
                Plugin.Log.LogWarning($"OnReceiveLobbyData transpiler: found {positions.Count} ldc.i4 4 but none matched comparison pattern within 4 instructions. Game may have updated.");
                Plugin.Log.LogInfo("  Falling back to first-occurrence strategy...");
                var firstResult = TryReplaceFirstOccurrence(codes, positions, getter, 6);
                if (firstResult > 0)
                {
                    replaced = firstResult;
                    strategyUsed = "B";
                }
            }

            if (replaced == 0)
            {
                Plugin.Log.LogError($"OnReceiveLobbyData transpiler: found {positions.Count} constant '4' but no pattern matched (tried A, B). Game may have updated.");
            }
            else
            {
                var firstReplacementPos = FindFirstReplacementPosition(codes, getter);
                Plugin.Log.LogInfo($"OnReceiveLobbyData transpiler: replaced {replaced} lobby-cap constant(s) using strategy {strategyUsed}. First replacement at position {firstReplacementPos}.");
                UpdateStoredSignature(key, codes);
            }

            return codes;
        }

        private static bool CheckSignatureAndWarnVerbose(string key, IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();
            if (!KnownSignatures.TryGetValue(key, out var knownSig))
            {
                Plugin.Log.LogInfo($"No known signature stored for {key}; storing baseline from this run.");
                var newSig = ComputeCurrentSignature(codeList, 30);
                KnownSignatures[key] = newSig;
                Plugin.Log.LogInfo($"  Stored baseline: {newSig}");
                return false;
            }

            var currentSig = ComputeCurrentSignature(codeList, 30);
            var similarity = TranspilerHelpers.SignatureSimilarity(knownSig, currentSig);

            Plugin.Log.LogInfo($"Signature check for {key}:");
            Plugin.Log.LogInfo($"  Known:  {knownSig}");
            Plugin.Log.LogInfo($"  Current: {currentSig}");
            Plugin.Log.LogInfo($"  Similarity: {similarity:P0}");

            if (similarity < 0.7f)
            {
                Plugin.Log.LogWarning($"IL signature for {key} differs from known pattern (similarity={similarity:P0}). Game may have updated. Attempting patch anyway...");
                return true;
            }

            return similarity < 1.0f;
        }
    }
}
