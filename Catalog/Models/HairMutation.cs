using UnityEngine;

namespace juniperD.Models
{
	public class HairMutation
	{
		// Serialized...
		public string DAZHairGroupName { get; set; }
		public bool Active { get; set; } = true;
		

		// Non-Serialized...
		public UIDynamicToggle UiToggle { get; set; }
		public DAZHairGroup DAZHairGroup { get; set; }
		public UiCatalogSubItem DynamicCheckbox { get; set; }
	}

}
