namespace juniperD.Models
{
	public class VersionMessage
	{
		// Example: if {Operator: GreaterThan} {Version: 1.02} then print {Message}
		// Serialized...
		public string If { get; set; }
		public string Version { get; set; }
		public string LongMessage { get; set; }
		public string ShortMessage { get;set;}
		public string Then { get; set; }
		// Non-Serialized...
	}
}

