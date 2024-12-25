using Newtonsoft.Json;
using UnityEngine;

namespace Donuts.Models;

[JsonObject]
public class Position
{
	[JsonProperty("x")]
	public float x { get; set; }

	[JsonProperty("y")]
	public float y { get; set; }

	[JsonProperty("z")]
	public float z { get; set; }

	public static implicit operator Vector3(Position p)
	{
		return new Vector3(p.x, p.y, p.z);
	}
}