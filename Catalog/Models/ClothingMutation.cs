using System;
using UnityEngine;

namespace juniperD.Models
{
	public class ClothingMutation
	{
		// Serialized...
		public string DAZClothingItemName { get; set; }
		public bool Active { get; set; } = true;

		// Non-Serialized...
		public UIDynamicToggle UiToggle { get; set; }
		public DAZClothingItem DAZClothingItem { get; set; }
		public UiCatalogSubItem DynamicCheckbox { get; set; }

	}

}
