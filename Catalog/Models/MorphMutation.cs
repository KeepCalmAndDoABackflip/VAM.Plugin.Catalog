using UnityEngine;

namespace juniperD.Models
{
	public class MorphMutation: BaseMutation
	{
		// Serialized...
		//public string Id { get; set; }
		//public bool Active { get; set; } = true;
		public float PreviousValue { get; set; }
		public float Value { get; set; }
		

		// Non-Serialized...
		public UIDynamicToggle UiToggle {get;set; }
		public string Name { get; internal set; }
		//public DAZMorph MorphItem {get;set; }
		//public EntrySubItem DynamicCheckbox { get; set; }
	}

}
