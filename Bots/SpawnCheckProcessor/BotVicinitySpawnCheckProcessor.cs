using EFT;

namespace Donuts.Bots.SpawnCheckProcessor;

public class BotVicinitySpawnCheckProcessor : SpawnCheckProcessorBase
{
	public override void Process(SpawnCheckData data)
	{
		if (!DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsBool.Value)
		{
			data.Success = true;
			base.Process(data);
			return;
		}
		
		float triggerDistance = GetMinDistanceFromOtherBots(data.mapLocation);
		float triggerSqrMagnitude = triggerDistance * triggerDistance;

		for (int i = data.alivePlayers.Count - 1; i >= 0; i--)
		{
			Player player = data.alivePlayers[i];
			if (player == null || !player.IsAI || !player.HealthController.IsAlive)
			{
				continue;
			}

			float actualSqrMagnitude = (((IPlayer)player).Position - data.position).sqrMagnitude;
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
			"factory4_day" or "factory4_night" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsStreets.Value,
			"sandbox" or "sandbox_high" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsShoreline.Value,
			"woods" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsWoods.Value,
			"laboratory" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsInterchange.Value,
			_ => 0f,
		};
}