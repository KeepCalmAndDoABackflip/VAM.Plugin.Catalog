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
		public string PositionState { get; set; }
		public string RotationState { get; set; }

		public float StartAtTimeRatio { get; set; }
		public float EndAtTimeRatio { get; set; }

		// Non-Serialized...
		public string Label;
		public UIDynamicToggle UiToggle {get;set; }
		public AnimationElement AnimationItem { get; set; }
	}

}
