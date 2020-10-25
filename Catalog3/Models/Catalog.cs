using juniperD.Contracts;
using System.Collections.Generic;
using System.Linq;

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
		public CatalogEntry CurrentAppliedEntry { get; set; }
		public CatalogEntry CurrentSelectedEntry { get; set; }

		public void SetAppliedEntry(CatalogEntry selectCatalogEntry)
		{
			CurrentSelectedEntry = selectCatalogEntry;
		}

		public void SetCurrentAppliedEntry(int index)
		{
			if (!Entries.Any()) return;
			CurrentAppliedEntry = Entries.ElementAt(index);
		}

		public void SetCurrentSelectedEntry(CatalogEntry selectCatalogEntry)
		{
			CurrentSelectedEntry = selectCatalogEntry;
		}

		public void SetCurrentSelectedEntry(int index)
		{
			if (!Entries.Any()) return;
			CurrentSelectedEntry = Entries.ElementAt(index);
		}

	}
}

