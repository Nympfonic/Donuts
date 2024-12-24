using EFT;

namespace Donuts.Bots.SpawnCheckProcessor;

public class BotVicinitySpawnCheckProcessor : SpawnCheckProcessorBase
{
	public override void Process(SpawnCheckData data)
	{
		if (!DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsBool.Value)
		{
			data.Success = false;
			return;
		}
		
		float triggerDistance = GetMinDistanceFromOtherBots(data.MapLocation);
		float triggerSqrMagnitude = triggerDistance * triggerDistance;

		foreach (Player player in data.AlivePlayers)
		{
			if (!player.IsAI)
			{
				continue;
			}

			float actualSqrMagnitude = (((IPlayer)player).Position - data.Position).sqrMagnitude;
			if (actualSqrMagnitude <= triggerSqrMagnitude)
			{
				data.Success = false;
				return;
			}
		}
		
		data.Success = true;
		base.Process(data);
	}
	
	private static float GetMinDistanceFromOtherBots(string mapLocation) =>
		mapLocation switch
		{
			"bigmap" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsCustoms.Value,
			"factory4_day" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsFactory.Value,
			"factory4_night" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsStreets.Value,
			"sandbox" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsGroundZero.Value,
			"sandbox_high" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsShoreline.Value,
			"woods" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsWoods.Value,
			"laboratory" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsInterchange.Value,
			_ => 0f,
		};
}