using UnityEngine;

namespace juniperD.Models
{
	public class HairMutation: BaseMutation
	{
		// Serialized...
		//public string Id { get; set; }
		//public bool Active { get; set; } = true;
		

		// Non-Serialized...
		public UIDynamicToggle UiToggle { get; set; }
		public DAZHairGroup DAZHairGroup { get; set; }
		//public EntrySubItem DynamicCheckbox { get; set; }
	}

}
