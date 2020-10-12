namespace juniperD.Models
{
	public class StoredAction
	{
		public static string ENUM_INITIATOR_FRAME_IN = "on enter frame";
		public static string ENUM_INITIATOR_FRAME_OUT = "on exit frame";

		public bool Active { get; set; } = true;
		public string AtomName { get;set;}
		public string StorableId { get; set; }
		public string ActionName { get;set; }
		public string ActionValue { get; set; }
		public string InitiatorEnum { get; set; }
		public UIDynamicToggle UiToggle { get; set; }
	}
}