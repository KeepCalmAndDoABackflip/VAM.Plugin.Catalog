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

		public Vector3 Rotation { get; set; }
		public Vector3 Position { get; set; }
		public string PositionState { get; internal set; }
		public string RotationState { get; internal set; }

		// Non-Serialized...
		public UIDynamicToggle UiToggle {get;set; }
		//public DAZMorph MorphItem {get;set; }
		//public EntrySubItem DynamicCheckbox { get; set; }
	}

}
