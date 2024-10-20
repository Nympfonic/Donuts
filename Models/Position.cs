using Newtonsoft.Json;
using UnityEngine;

namespace Donuts.Models
{
	public class Position
	{
		[JsonIgnore]
		private Vector3 internalVector = Vector3.positiveInfinity;

		public float x { get; set; }
		public float y { get; set; }
		public float z { get; set; }

		public Vector3 ToVector3()
		{
			if (internalVector == Vector3.positiveInfinity)
			{
				internalVector = new Vector3(x, y, z);
			}
			return internalVector;
		}
	}
}
