global using BotZoneGroupData = GClass555;

/*
 * SpawnPointData's method signature:
 *	public SpawnPointData(Vector3 pos, int corePointId, bool isUsed)
 *	
 * SpawnPointData also has 3 fields:
 *	public Vector3 position
 *	public bool alreadyUsedToSpawn
 *	public int CorePointId
 */
global using SpawnPointData = GClass660;

// Class implements IGetProfileData
global using BotProfileRequestData = GClass663;

// Interface has method signature "CancellationToken GetCancelToken()"
global using ICancellable = GInterface22;