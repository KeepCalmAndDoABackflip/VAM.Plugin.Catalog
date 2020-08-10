namespace juniperD.Models
{
	public class DynamicMutation
	{
		// Serialized...
		public string DAZDynamicItemName { get; set; }
		public bool Active { get; set; } = true;
		

		// Non-Serialized...
		public UIDynamicToggle UiToggle;
		public DAZDynamicItem DAZDynamicItem;
		public UIDynamicButton UiToggleInfoBox { get; set; }
	}

}
