using Comfort.Common;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;

namespace Donuts.FikaBridge;

public static class FikaBridge
{
	private static LineOfSightController _lineOfSightController;
	private static PlayerLineOfSightChecker _playerLineOfSightChecker;
	
	public static void Initialize()
	{
		if (FikaBackendUtils.IsServer)
		{
			_lineOfSightController = new LineOfSightController(Singleton<FikaServer>.Instance);
			Singleton<LineOfSightController>.Create(_lineOfSightController);
		}
		else
		{
			_playerLineOfSightChecker = new PlayerLineOfSightChecker(Singleton<FikaClient>.Instance);
			Singleton<PlayerLineOfSightChecker>.Create(_playerLineOfSightChecker);
		}
	}
}