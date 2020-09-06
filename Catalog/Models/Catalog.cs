using juniperD.Contracts;
using System.Collections.Generic;

namespace juniperD.Models
{
	public class Catalog
	{
		// Serialized...
		public string PluginVersion { get; set;} = Global.HOST_PLUGIN_SYMANTIC_VERSION;
		public List<VersionMessage> VersionMessages { get; set; } = new List<VersionMessage>();
		public VersionMessage ActiveVersionMessage { get; set; }
		public string DeapVersion { get; set; }
		public List<CatalogEntry> Entries { get; set;} = new List<CatalogEntry>();

		public bool CaptureHair { get;set; }  = true;
		public bool CaptureClothes { get; set; } = true;
		public bool CaptureMorphs { get; set; } = true;
		public bool CapturePose { get; set; } = true;

		// Non-Serialized...
	}
}

