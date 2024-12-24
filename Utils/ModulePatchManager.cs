using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Donuts.Utils;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DisablePatchAttribute : Attribute;

public static class ModulePatchManager
{
	private static readonly List<ModulePatch> _patches = [];

	static ModulePatchManager()
	{
		IEnumerable<Type> currentAssemblyTypes = Assembly.GetExecutingAssembly().GetTypes();
		foreach (Type type in currentAssemblyTypes)
		{
			if (type.BaseType == typeof(ModulePatch) &&
				type.GetCustomAttribute(typeof(DisablePatchAttribute)) == null)
			{
				_patches.Add((ModulePatch)Activator.CreateInstance(type));
			}
		}
	}
	
	public static void EnablePatches()
	{
		foreach (ModulePatch patch in _patches)
			patch.Enable();
	}

	public static void DisablePatches()
	{
		foreach (ModulePatch patch in _patches)
			patch.Disable();
	}

	public static void EnablePatch<T>() where T : ModulePatch
	{
		foreach (ModulePatch patch in _patches)
		{
			if (_patches.GetType() == typeof(T))
			{
				patch.Enable();
				return;
			}
		}
	}

	public static void DisablePatch<T>() where T : ModulePatch
	{
		foreach (ModulePatch patch in _patches)
		{
			if (_patches.GetType() == typeof(T))
			{
				patch.Disable();
				return;
			}
		}
	}
}