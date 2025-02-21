using Comfort.Common;
using Fika.Core.Networking;
using Unity.Collections;
using UnityEngine;

namespace Donuts.FikaBridge;

public class LineOfSightController(FikaServer fikaServer)
{
	private readonly NativeArray<RaycastCommand> _lineOfSightCommands;
}