using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace LumaPlayerLimit
{
    internal static class TranspilerHelpers
    {
        public static CodeInstruction CloneAsCall(CodeInstruction original, MethodInfo method)
        {
            var replacement = new CodeInstruction(OpCodes.Call, method);
            replacement.labels.AddRange(original.labels);
            replacement.blocks.AddRange(original.blocks);
            return replacement;
        }

        public static bool CallsMethodNamed(CodeInstruction instruction, string methodName)
        {
            if (instruction == null || instruction.operand == null)
            {
                return false;
            }

            var methodInfo = instruction.operand as MethodInfo;
            if (methodInfo != null)
            {
                return string.Equals(methodInfo.Name, methodName, StringComparison.Ordinal);
            }

            var methodBase = instruction.operand as MethodBase;
            if (methodBase != null)
            {
                return string.Equals(methodBase.Name, methodName, StringComparison.Ordinal);
            }

            return false;
        }

        public static bool IsLoadInt(CodeInstruction code, int value)
        {
            if (code == null)
            {
                return false;
            }

            var op = code.opcode;
            if (op == OpCodes.Ldc_I4_4) return value == 4;
            if (op == OpCodes.Ldc_I4_0) return value == 0;
            if (op == OpCodes.Ldc_I4_1) return value == 1;
            if (op == OpCodes.Ldc_I4_2) return value == 2;
            if (op == OpCodes.Ldc_I4_3) return value == 3;
            if (op == OpCodes.Ldc_I4_5) return value == 5;
            if (op == OpCodes.Ldc_I4_6) return value == 6;
            if (op == OpCodes.Ldc_I4_7) return value == 7;
            if (op == OpCodes.Ldc_I4_8) return value == 8;
            if (op == OpCodes.Ldc_I4_M1) return value == -1;
            if (op == OpCodes.Ldc_I4) return code.operand is int && (int)code.operand == value;
            if (op == OpCodes.Ldc_I4_S) return code.operand != null && Convert.ToInt32(code.operand) == value;
            return false;
        }

        public static bool IsIntConstant(CodeInstruction code, out int value)
        {
            value = 0;
            if (code == null)
            {
                return false;
            }

            var op = code.opcode;
            if (op == OpCodes.Ldc_I4_0) { value = 0; return true; }
            if (op == OpCodes.Ldc_I4_1) { value = 1; return true; }
            if (op == OpCodes.Ldc_I4_2) { value = 2; return true; }
            if (op == OpCodes.Ldc_I4_3) { value = 3; return true; }
            if (op == OpCodes.Ldc_I4_4) { value = 4; return true; }
            if (op == OpCodes.Ldc_I4_5) { value = 5; return true; }
            if (op == OpCodes.Ldc_I4_6) { value = 6; return true; }
            if (op == OpCodes.Ldc_I4_7) { value = 7; return true; }
            if (op == OpCodes.Ldc_I4_8) { value = 8; return true; }
            if (op == OpCodes.Ldc_I4_M1) { value = -1; return true; }
            if (op == OpCodes.Ldc_I4 && code.operand is int i) { value = i; return true; }
            if (op == OpCodes.Ldc_I4_S && code.operand != null) { value = Convert.ToInt32(code.operand); return true; }
            return false;
        }

        public static int CountLoadIntPattern(IList<CodeInstruction> codes, int value)
        {
            var count = 0;
            for (var i = 0; i < codes.Count; i++)
            {
                if (IsLoadInt(codes[i], value))
                {
                    count++;
                }
            }
            return count;
        }

        public static List<int> FindLoadIntPositions(IList<CodeInstruction> codes, int value)
        {
            var positions = new List<int>();
            for (var i = 0; i < codes.Count; i++)
            {
                if (IsLoadInt(codes[i], value))
                {
                    positions.Add(i);
                }
            }
            return positions;
        }

        public static bool ValidateStackTransition(IList<CodeInstruction> codes, int position, int stackDepthBefore)
        {
            var depth = stackDepthBefore;
            for (var i = position; i < codes.Count; i++)
            {
                var code = codes[i];
                var op = code.opcode;

                if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
                {
                    var methodInfo = code.operand as MethodInfo;
                    if (methodInfo != null)
                    {
                        depth -= methodInfo.GetParameters().Length;
                        if (methodInfo.ReturnType != typeof(void))
                        {
                            depth++;
                        }
                        continue;
                    }
                    var constructorInfo = code.operand as ConstructorInfo;
                    if (constructorInfo != null)
                    {
                        depth -= constructorInfo.GetParameters().Length;
                        depth++;
                        continue;
                    }
                    depth++;
                    continue;
                }
                else if (op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 ||
                         op == OpCodes.Ldarg_1 || op == OpCodes.Ldarg_2 ||
                         op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S)
                {
                    depth++;
                }
                else if (op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 ||
                         op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 ||
                         op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S)
                {
                    depth++;
                }
                else if (op == OpCodes.Stloc || op == OpCodes.Stloc_0 ||
                         op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 ||
                         op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S)
                {
                    depth--;
                }
                else if (op == OpCodes.Starg || op == OpCodes.Starg_S)
                {
                    depth--;
                }
                else if (op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 ||
                         op == OpCodes.Ldc_I4_2 || op == OpCodes.Ldc_I4_3 ||
                         op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5 ||
                         op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 ||
                         op == OpCodes.Ldc_I4_8 || op == OpCodes.Ldc_I4_M1 ||
                         op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S)
                {
                    depth++;
                }
                else if (op == OpCodes.Pop)
                {
                    depth--;
                }
                else if (op == OpCodes.Dup)
                {
                    depth++;
                }
                else if (op == OpCodes.Castclass || op == OpCodes.Isinst)
                {
                }
                else if (op == OpCodes.Conv_I1 || op == OpCodes.Conv_I2 ||
                         op == OpCodes.Conv_I4 || op == OpCodes.Conv_U1 ||
                         op == OpCodes.Conv_U2 || op == OpCodes.Conv_U4)
                {
                }
                else if (op == OpCodes.Clt || op == OpCodes.Cgt ||
                         op == OpCodes.Ceq || op == OpCodes.Clt_Un ||
                         op == OpCodes.Cgt_Un)
                {
                    depth -= 2;
                    depth++;
                }
                else if (op == OpCodes.Add || op == OpCodes.Sub ||
                         op == OpCodes.Mul || op == OpCodes.Div ||
                         op == OpCodes.Rem || op == OpCodes.And ||
                         op == OpCodes.Or || op == OpCodes.Xor)
                {
                    depth -= 2;
                    depth++;
                }
                else if (op == OpCodes.Neg || op == OpCodes.Not)
                {
                }
                else if (op == OpCodes.Ldflda || op == OpCodes.Ldfld ||
                         op == OpCodes.Stfld)
                {
                }
                else if (op == OpCodes.Stloc_S)
                {
                    depth--;
                }
                else if (op == OpCodes.Ret)
                {
                    return depth == stackDepthBefore;
                }
                else if (op == OpCodes.Br || op == OpCodes.Brfalse ||
                         op == OpCodes.Brtrue || op == OpCodes.Beq ||
                         op == OpCodes.Bne_Un || op == OpCodes.Bge ||
                         op == OpCodes.Bgt || op == OpCodes.Ble ||
                         op == OpCodes.Blt || op == OpCodes.Bge_Un ||
                         op == OpCodes.Bgt_Un || op == OpCodes.Ble_Un ||
                         op == OpCodes.Blt_Un)
                {
                    return depth == stackDepthBefore;
                }
            }
            return depth == stackDepthBefore;
        }

        public static bool ValidateStackTransitionVerbose(IList<CodeInstruction> codes, int position, int stackDepthBefore, System.Text.StringBuilder trace)
        {
            var depth = stackDepthBefore;
            for (var i = position; i < codes.Count; i++)
            {
                var code = codes[i];
                var op = code.opcode;

                if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
                {
                    var methodInfo = code.operand as MethodInfo;
                    if (methodInfo != null)
                    {
                        depth -= methodInfo.GetParameters().Length;
                        if (methodInfo.ReturnType != typeof(void))
                        {
                            depth++;
                        }
                        if (trace != null) trace.AppendLine($"  [{i}] {op} method={methodInfo.Name} -> stack={depth}");
                        continue;
                    }
                    var constructorInfo = code.operand as ConstructorInfo;
                    if (constructorInfo != null)
                    {
                        depth -= constructorInfo.GetParameters().Length;
                        depth++;
                        if (trace != null) trace.AppendLine($"  [{i}] {op} ctor -> stack={depth}");
                        continue;
                    }
                    if (trace != null) trace.AppendLine($"  [{i}] {op} unknown-call -> stack={depth} (untracked, assuming +1)");
                    depth++;
                    continue;
                }
                else if (op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 ||
                         op == OpCodes.Ldarg_1 || op == OpCodes.Ldarg_2 ||
                         op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S)
                {
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth}");
                }
                else if (op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 ||
                         op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 ||
                         op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S)
                {
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth}");
                }
                else if (op == OpCodes.Stloc || op == OpCodes.Stloc_0 ||
                         op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 ||
                         op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S)
                {
                    depth--;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Stloc] -> stack={depth}");
                }
                else if (op == OpCodes.Starg || op == OpCodes.Starg_S)
                {
                    depth--;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Starg] -> stack={depth}");
                }
                else if (op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 ||
                         op == OpCodes.Ldc_I4_2 || op == OpCodes.Ldc_I4_3 ||
                         op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5 ||
                         op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 ||
                         op == OpCodes.Ldc_I4_8 || op == OpCodes.Ldc_I4_M1 ||
                         op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S)
                {
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth}");
                }
                else if (op == OpCodes.Pop)
                {
                    depth--;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth}");
                }
                else if (op == OpCodes.Dup)
                {
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth}");
                }
                else if (op == OpCodes.Castclass || op == OpCodes.Isinst)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Castclass/Isinst] -> stack={depth}");
                }
                else if (op == OpCodes.Conv_I1 || op == OpCodes.Conv_I2 ||
                         op == OpCodes.Conv_I4 || op == OpCodes.Conv_U1 ||
                         op == OpCodes.Conv_U2 || op == OpCodes.Conv_U4)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Conv] -> stack={depth}");
                }
                else if (op == OpCodes.Clt || op == OpCodes.Cgt ||
                         op == OpCodes.Ceq || op == OpCodes.Clt_Un ||
                         op == OpCodes.Cgt_Un)
                {
                    depth -= 2;
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Comparison] -> stack={depth}");
                }
                else if (op == OpCodes.Add || op == OpCodes.Sub ||
                         op == OpCodes.Mul || op == OpCodes.Div ||
                         op == OpCodes.Rem || op == OpCodes.And ||
                         op == OpCodes.Or || op == OpCodes.Xor)
                {
                    depth -= 2;
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Arithmetic] -> stack={depth}");
                }
                else if (op == OpCodes.Neg || op == OpCodes.Not)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Unary] -> stack={depth}");
                }
                else if (op == OpCodes.Ldflda || op == OpCodes.Ldfld ||
                         op == OpCodes.Stfld)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Field] -> stack={depth}");
                }
                else if (op == OpCodes.Stloc_S)
                {
                    depth--;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Stloc_S] -> stack={depth}");
                }
                else if (op == OpCodes.Ret)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Ret] -> stack={depth} (method exit)");
                    return depth == stackDepthBefore;
                }
                else if (op == OpCodes.Br || op == OpCodes.Brfalse ||
                         op == OpCodes.Brtrue || op == OpCodes.Beq ||
                         op == OpCodes.Bne_Un || op == OpCodes.Bge ||
                         op == OpCodes.Bgt || op == OpCodes.Ble ||
                         op == OpCodes.Blt || op == OpCodes.Bge_Un ||
                         op == OpCodes.Bgt_Un || op == OpCodes.Ble_Un ||
                         op == OpCodes.Blt_Un)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} [Branch] -> stack={depth} (control flow redirected, stopping validation)");
                    return depth == stackDepthBefore;
                }
                else
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (untracked)");
                }
            }
            return depth == stackDepthBefore;
        }

        public static int CountOcurrences(IList<CodeInstruction> codes, OpCode opcode, object operand = null)
        {
            var count = 0;
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == opcode && (operand == null || Equals(codes[i].operand, operand)))
                {
                    count++;
                }
            }
            return count;
        }

        public static int ComputeStackDepthAt(IList<CodeInstruction> codes, int position)
        {
            var depth = 0;
            for (var i = 0; i < position; i++)
            {
                var code = codes[i];
                var op = code.opcode;

                if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
                {
                    var methodInfo = code.operand as MethodInfo;
                    if (methodInfo != null)
                    {
                        depth -= methodInfo.GetParameters().Length;
                        if (methodInfo.ReturnType != typeof(void))
                        {
                            depth++;
                        }
                    }
                    else
                    {
                        var constructorInfo = code.operand as ConstructorInfo;
                        if (constructorInfo != null)
                        {
                            depth -= constructorInfo.GetParameters().Length;
                            depth++;
                        }
                    }
                }
                else if (op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 ||
                         op == OpCodes.Ldarg_1 || op == OpCodes.Ldarg_2 ||
                         op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S ||
                         op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 ||
                         op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 ||
                         op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S ||
                         op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 ||
                         op == OpCodes.Ldc_I4_2 || op == OpCodes.Ldc_I4_3 ||
                         op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5 ||
                         op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 ||
                         op == OpCodes.Ldc_I4_8 || op == OpCodes.Ldc_I4_M1 ||
                         op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S ||
                         op == OpCodes.Ldstr || op == OpCodes.Ldnull ||
                         op == OpCodes.Ldftn)
                {
                    depth++;
                }
                else if (op == OpCodes.Stloc || op == OpCodes.Stloc_0 ||
                         op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 ||
                         op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S)
                {
                    depth--;
                }
                else if (op == OpCodes.Starg || op == OpCodes.Starg_S)
                {
                    depth--;
                }
                else if (op == OpCodes.Pop)
                {
                    depth--;
                }
                else if (op == OpCodes.Dup)
                {
                    depth++;
                }
                else if (op == OpCodes.Castclass || op == OpCodes.Isinst)
                {
                }
                else if (op == OpCodes.Conv_I1 || op == OpCodes.Conv_I2 ||
                         op == OpCodes.Conv_I4 || op == OpCodes.Conv_U1 ||
                         op == OpCodes.Conv_U2 || op == OpCodes.Conv_U4)
                {
                }
                else if (op == OpCodes.Clt || op == OpCodes.Cgt ||
                         op == OpCodes.Ceq || op == OpCodes.Clt_Un ||
                         op == OpCodes.Cgt_Un)
                {
                    depth -= 2;
                    depth++;
                }
                else if (op == OpCodes.Add || op == OpCodes.Sub ||
                         op == OpCodes.Mul || op == OpCodes.Div ||
                         op == OpCodes.Rem || op == OpCodes.And ||
                         op == OpCodes.Or || op == OpCodes.Xor)
                {
                    depth -= 2;
                    depth++;
                }
                else if (op == OpCodes.Neg || op == OpCodes.Not)
                {
                }
                else if (op == OpCodes.Ldflda || op == OpCodes.Ldfld ||
                         op == OpCodes.Stfld)
                {
                }
            }
            return depth;
        }

        public static int ComputeStackDepthAtVerbose(IList<CodeInstruction> codes, int position, System.Text.StringBuilder trace)
        {
            var depth = 0;
            for (var i = 0; i < position; i++)
            {
                var code = codes[i];
                var op = code.opcode;
                var delta = 0;

                if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
                {
                    var methodInfo = code.operand as MethodInfo;
                    if (methodInfo != null)
                    {
                        delta = -(methodInfo.GetParameters().Length);
                        if (methodInfo.ReturnType != typeof(void))
                        {
                            delta++;
                        }
                        depth += delta;
                        if (trace != null) trace.AppendLine($"  [{i}] {op} -> method={methodInfo.Name} stack={depth} (delta={delta})");
                        continue;
                    }
                    var constructorInfo = code.operand as ConstructorInfo;
                    if (constructorInfo != null)
                    {
                        delta = -(constructorInfo.GetParameters().Length) + 1;
                        depth += delta;
                        if (trace != null) trace.AppendLine($"  [{i}] {op} -> ctor stack={depth} (delta={delta})");
                        continue;
                    }
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> unknown-call stack={depth}");
                    continue;
                }
                else if (op == OpCodes.Ldarg || op == OpCodes.Ldarg_0 ||
                         op == OpCodes.Ldarg_1 || op == OpCodes.Ldarg_2 ||
                         op == OpCodes.Ldarg_3 || op == OpCodes.Ldarg_S ||
                         op == OpCodes.Ldloc || op == OpCodes.Ldloc_0 ||
                         op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 ||
                         op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S ||
                         op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 ||
                         op == OpCodes.Ldc_I4_2 || op == OpCodes.Ldc_I4_3 ||
                         op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5 ||
                         op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 ||
                         op == OpCodes.Ldc_I4_8 || op == OpCodes.Ldc_I4_M1 ||
                         op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S ||
                         op == OpCodes.Ldstr || op == OpCodes.Ldnull ||
                         op == OpCodes.Ldftn)
                {
                    depth++;
                    delta = 1;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=+1)");
                }
                else if (op == OpCodes.Stloc || op == OpCodes.Stloc_0 ||
                         op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 ||
                         op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S)
                {
                    depth--;
                    delta = -1;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=-1) [Stloc]");
                }
                else if (op == OpCodes.Starg || op == OpCodes.Starg_S)
                {
                    depth--;
                    delta = -1;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=-1) [Starg]");
                }
                else if (op == OpCodes.Pop)
                {
                    depth--;
                    delta = -1;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=-1)");
                }
                else if (op == OpCodes.Dup)
                {
                    depth++;
                    delta = 1;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=+1)");
                }
                else if (op == OpCodes.Castclass || op == OpCodes.Isinst)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=0) [Castclass/Isinst]");
                }
                else if (op == OpCodes.Conv_I1 || op == OpCodes.Conv_I2 ||
                         op == OpCodes.Conv_I4 || op == OpCodes.Conv_U1 ||
                         op == OpCodes.Conv_U2 || op == OpCodes.Conv_U4)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=0) [Conv]");
                }
                else if (op == OpCodes.Clt || op == OpCodes.Cgt ||
                         op == OpCodes.Ceq || op == OpCodes.Clt_Un ||
                         op == OpCodes.Cgt_Un)
                {
                    var prevDepth = depth;
                    depth -= 2;
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta={depth-prevDepth}) [Comparison]");
                }
                else if (op == OpCodes.Add || op == OpCodes.Sub ||
                         op == OpCodes.Mul || op == OpCodes.Div ||
                         op == OpCodes.Rem || op == OpCodes.And ||
                         op == OpCodes.Or || op == OpCodes.Xor)
                {
                    var prevDepth = depth;
                    depth -= 2;
                    depth++;
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta={depth-prevDepth}) [Arithmetic]");
                }
                else if (op == OpCodes.Neg || op == OpCodes.Not)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=0) [Unary]");
                }
                else if (op == OpCodes.Ldflda || op == OpCodes.Ldfld ||
                         op == OpCodes.Stfld)
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=0) [Field]");
                }
                else
                {
                    if (trace != null) trace.AppendLine($"  [{i}] {op} -> stack={depth} (delta=0) [untracked]");
                }
            }
            return depth;
        }

        public static string ComputeILSignature(IList<CodeInstruction> codes, int targetCount)
        {
            if (codes == null || codes.Count == 0)
            {
                return string.Empty;
            }

            var keyOps = codes
                .Take(Math.Min(codes.Count, targetCount))
                .Select(c => c.opcode.ToString())
                .ToArray();

            return string.Join(" ", keyOps);
        }

        public static float SignatureSimilarity(string sig1, string sig2)
        {
            if (string.IsNullOrEmpty(sig1) && string.IsNullOrEmpty(sig2))
            {
                return 1f;
            }

            if (string.IsNullOrEmpty(sig1) || string.IsNullOrEmpty(sig2))
            {
                return 0f;
            }

            var tokens1 = sig1.Split(' ');
            var tokens2 = sig2.Split(' ');
            var matches = tokens1.Intersect(tokens2).Count();
            return (float)matches / Math.Max(tokens1.Length, tokens2.Length);
        }
    }
}
