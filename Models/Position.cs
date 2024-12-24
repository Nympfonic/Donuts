using Newtonsoft.Json;
using UnityEngine;

namespace Donuts.Models;

[JsonObject]
public class Position
{
	private Vector3 _internalVector = Vector3.positiveInfinity;

	[JsonProperty("x")]
	public float x { get; set; }

	[JsonProperty("y")]
	public float y { get; set; }

	[JsonProperty("z")]
	public float z { get; set; }

	public static implicit operator Vector3(Position p)
	{
		p._internalVector.Set(p.x, p.y, p.z);
		return p._internalVector;
	}
}