using Comfort.Common;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Donuts.Bots;
using EFT;
using HarmonyLib;
using JetBrains.Annotations;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Donuts.Patches;

/// <summary>
/// Patch <see cref="LocalGame.vmethod_1"/>'s compiler-generated MoveNext method to await Donut's raid manager initialization.
/// <br/><br/>
/// Method signature:
/// <br/>
/// <c>public override async Task vmethod_1(BotControllerSettings controllerSettings, ISpawnSystem spawnSystem)</c>
/// </summary>
[UsedImplicitly]
internal class EnableRaidManagerPatch : ModulePatch
{
	// protected override MethodBase GetTargetMethod() =>
	// 	AccessTools.Method(typeof(BotsController), nameof(BotsController.AddActivePLayer));
	
	protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(LocalGame.Struct480), "MoveNext");

	[PatchTranspiler]
	private static IEnumerable<CodeInstruction> PatchTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
	{
		var codes = new List<CodeInstruction>(instructions);
		InitializeRaidManager(codes, generator);
		return codes;
	}

	private static void InitializeRaidManager(List<CodeInstruction> codes, ILGenerator generator)
	{
		MethodInfo insertAfterMethod = AccessTools.Method(typeof(BotsController), nameof(BotsController.AddActivePLayer));
		
		MethodInfo enableRaidManagerMethod = AccessTools.Method(typeof(DonutsRaidManager), nameof(DonutsRaidManager.Enable));
		MethodInfo initRaidManagerMethod = AccessTools.Method(typeof(DonutsRaidManager), nameof(DonutsRaidManager.Initialize));
		MethodInfo taskAwaiterMethod = AccessTools.Method(typeof(Task), nameof(Task.GetAwaiter));
		MethodInfo taskIsCompletedMethod = AccessTools.PropertyGetter(typeof(Task), nameof(Task.IsCompleted));
		FieldInfo taskStateMachineIndexField = AccessTools.Field(typeof(LocalGame.Struct480), "int_0");
		FieldInfo taskAwaiterField = AccessTools.Field(typeof(LocalGame.Struct480), "taskAwaiter_0");
		FieldInfo asyncTaskMethodBuilderField = AccessTools.Field(typeof(LocalGame.Struct480), "asyncTaskMethodBuilder_0");
		MethodInfo awaitUnsafeOnCompletedMethod = AccessTools.Method(typeof(AsyncTaskMethodBuilder),
			nameof(AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted),
			generics: [typeof(TaskAwaiter), typeof(LocalGame.Struct480)]);
		MethodInfo taskAwaiterGetResultMethod =
			AccessTools.Method(typeof(TaskAwaiter), nameof(TaskAwaiter.GetResult));
		
		var switchCaseFound = false;
		Label newSwitchCaseOperandLabel = generator.DefineLabel();
		Label taskCompletedLabel = generator.DefineLabel();
		
		var thisObjectInstruction = new CodeInstruction(OpCodes.Ldarg_0);
		object localVariableIndex7Operand = codes.First(ci => ci.opcode == OpCodes.Stloc_S).operand;
		CodeInstruction taskCompletedInstruction = new CodeInstruction(OpCodes.Ldloca_S, localVariableIndex7Operand)
			.WithLabels(taskCompletedLabel);
		CodeInstruction switchCaseTargetInstruction = new CodeInstruction(OpCodes.Ldarg_0)
			.WithLabels(newSwitchCaseOperandLabel);
		
		for (var i = 0; i < codes.Count; i++)
		{
			CodeInstruction code = codes[i];
			
			if (!switchCaseFound && code.opcode == OpCodes.Switch)
			{
				// Add new switch case
				var originalSwitchLabels = (Label[])code.operand;
				Label[] newSwitchLabels = originalSwitchLabels.Concat([newSwitchCaseOperandLabel]).ToArray();
				code.operand = newSwitchLabels;
				
				switchCaseFound = true;
				continue;
			}
			
			if (switchCaseFound && code.opcode == OpCodes.Callvirt && (MethodInfo)code.operand == insertAfterMethod)
			{
				// Insert new instructions to await raid manager initialization task
				var instructions = new List<CodeInstruction>
				{
					new(OpCodes.Call, enableRaidManagerMethod),
					new(OpCodes.Call, initRaidManagerMethod),
					new(OpCodes.Callvirt, taskAwaiterMethod),
					new(OpCodes.Stloc_S, localVariableIndex7Operand),
					new(OpCodes.Ldloca_S, localVariableIndex7Operand),
					new(OpCodes.Call, taskIsCompletedMethod),
					new(OpCodes.Brtrue, taskCompletedLabel),
					thisObjectInstruction,
					new(OpCodes.Ldc_I4_3),
					new(OpCodes.Ldc_I4_3),
					new(OpCodes.Stloc_0),
					new(OpCodes.Stfld, taskStateMachineIndexField),
					thisObjectInstruction,
					new(OpCodes.Ldloc_S, localVariableIndex7Operand),
					new(OpCodes.Stfld, taskAwaiterField),
					thisObjectInstruction,
					new(OpCodes.Ldflda, asyncTaskMethodBuilderField),
					new(OpCodes.Ldloca_S, localVariableIndex7Operand),
					thisObjectInstruction,
					new(OpCodes.Call, awaitUnsafeOnCompletedMethod),
					new(OpCodes.Leave, codes.First(ci => ci.opcode == OpCodes.Leave).operand),
					switchCaseTargetInstruction,
					new(OpCodes.Ldfld, taskAwaiterField),
					new(OpCodes.Stloc_S, localVariableIndex7Operand),
					thisObjectInstruction,
					new(OpCodes.Ldflda, taskAwaiterField),
					new(OpCodes.Initobj, typeof(TaskAwaiter)),
					thisObjectInstruction,
					new(OpCodes.Ldc_I4_M1),
					new(OpCodes.Ldc_I4_M1),
					new(OpCodes.Stloc_0),
					new(OpCodes.Stfld, taskStateMachineIndexField),
					taskCompletedInstruction,
					new(OpCodes.Call, taskAwaiterGetResultMethod)
				};
				
				codes.InsertRange(i + 1, instructions);
				break;
			}
		}
	}
}