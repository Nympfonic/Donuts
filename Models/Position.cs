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

	public Position() {}

	public Position(Vector3 position)
	{
		x = position.x;
		y = position.y;
		z = position.z;
	}

	public static explicit operator Position(Vector3 p) => new(p);
	public static implicit operator Vector3(Position p) => new(p.x, p.y, p.z);
}