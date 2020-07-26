namespace CataloggerPlugin.Models
{
	public class HairMutation
	{
		// Serialized...
		public string DAZHairGroupName { get; set; }
		public bool Active { get; set; } = true;

		// Non-Serialized...
		public UIDynamicToggle UiToggle;
		public DAZHairGroup DAZHairGroup;
	}

}
