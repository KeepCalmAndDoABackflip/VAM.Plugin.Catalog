using UnityEngine;

namespace juniperD.Models
{
	public class MorphMutation
	{
		// Serialized...
		public string Name { get; set; }
		public float PreviousValue { get; set; }
		public float Value { get; set; }
		public bool Active { get; set; } = true;

		// Non-Serialized...
		public UIDynamicToggle UiToggle {get;set; }
		public DAZMorph MorphItem {get;set; }
		public UiCatalogSubItem DynamicCheckbox { get; set; }
	}

}
