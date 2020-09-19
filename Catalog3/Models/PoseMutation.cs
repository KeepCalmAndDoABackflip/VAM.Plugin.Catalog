using System.Collections.Generic;
using UnityEngine;

namespace juniperD.Models
{
	public class PoseMutation: BaseMutation
	{
		// Serialized...
		//public string Id { get; set; }
		//public bool Active { get; set; } = true;
		public List<ActiveController> Points = new List<ActiveController>();

		public Quaternion Rotation { get; set; }
		public Vector3 Position { get; set; }
		public string PositionState { get; internal set; }
		public string RotationState { get; internal set; }

		// Non-Serialized...
		public string Label;
		public UIDynamicToggle UiToggle {get;set; }
		internal AnimatedItem AnimatedItem { get; set; }
	}

}
