using System;

namespace CataloggerPlugin.Models
{
	public class ClothingMutation
	{
		// Serialized...
		public string DAZClothingItemName { get; set; }
		public bool Active { get; set; } = true;

		// Non-Serialized...
		public UIDynamicToggle UiToggle;
		public DAZClothingItem DAZClothingItem { get; set; }

	}

}
