using System;
using UnityEngine;

namespace juniperD.Models
{
	public class ClothingMutation: BaseMutation
	{
		// Serialized...
		//public string Id { get; set; } //...DAZClothingItemName
		//public bool Active { get; set; } = true;

		// Non-Serialized...
		public UIDynamicToggle UiToggle { get; set; }
		public DAZClothingItem DAZClothingItem { get; set; }
		//public EntrySubItem DynamicCheckbox { get; set; }

	}

}
