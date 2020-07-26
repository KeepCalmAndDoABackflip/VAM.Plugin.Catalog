namespace CataloggerPlugin.Models
{
	public class MorphMutation
	{
		// Serialized...
		public string Name { get; set; }
		public float PreviousValue { get; set; }
		public float Value { get; set; }
		public bool Active { get; set; } = true;

		// Non-Serialized...
		public UIDynamicToggle UiToggle;
		public DAZMorph MorphItem;
	}

}
