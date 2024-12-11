using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Donuts.Utils;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DisablePatchAttribute : Attribute;

public static class ModulePatchManager
{
	private static readonly Dictionary<Type, ModulePatch> _patches = [];

	static ModulePatchManager()
	{
		IEnumerable<Type> patchTypes = Assembly.GetExecutingAssembly().GetTypes();
		foreach (Type patch in patchTypes)
		{
			if (patch.BaseType == typeof(ModulePatch) &&
				patch.GetCustomAttribute(typeof(DisablePatchAttribute)) == null)
			{
				_patches[patch] = (ModulePatch)Activator.CreateInstance(patch);
			}
		}
	}
	
	public static void EnablePatches()
	{
		foreach (ModulePatch patch in _patches.Values)
			patch.Enable();
	}

	public static void DisablePatches()
	{
		foreach (ModulePatch patch in _patches.Values)
			patch.Disable();
	}
}