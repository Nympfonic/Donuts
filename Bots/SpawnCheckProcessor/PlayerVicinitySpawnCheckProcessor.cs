using EFT;

namespace Donuts.Bots.SpawnCheckProcessor;

public class PlayerVicinitySpawnCheckProcessor : SpawnCheckProcessorBase
{
	public override void Process(SpawnCheckData data)
	{
		if (!DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value)
		{
			data.Success = true;
			base.Process(data);
			return;
		}
		
		float triggerDistance = GetMinDistanceFromPlayer(data.mapLocation);
		float triggerSqrMagnitude = triggerDistance * triggerDistance;

		foreach (Player player in data.alivePlayers)
		{
			if (player.IsAI)
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
	
	private static float GetMinDistanceFromPlayer(string mapLocation) =>
		mapLocation switch
		{
			"bigmap" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerCustoms.Value,
			"factory4_day" or "factory4_night" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerFactory.Value,
			"tarkovstreets" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerStreets.Value,
			"sandbox" or "sandbox_high" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerGroundZero.Value,
			"rezervbase" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerReserve.Value,
			"lighthouse" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerLighthouse.Value,
			"shoreline" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerShoreline.Value,
			"woods" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerWoods.Value,
			"laboratory" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerLaboratory.Value,
			"interchange" => DefaultPluginVars.globalMinSpawnDistanceFromPlayerInterchange.Value,
			_ => 50f,
		};
}