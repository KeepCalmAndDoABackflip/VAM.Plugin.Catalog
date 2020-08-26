using SimpleJSON;
using System.Collections.Generic;

namespace juniperD.Models
{
	public class StoredAtom
	{
		public bool Active { get; set;}
		public string AtomType { get; set; }
		public string AtomName { get; set; }
		public List<JSONClass> Storables { get; set; } = new List<JSONClass>();
		public JSONClass FullAtom { get; set; }

		// Not serialized...
		public List<JSONClass> StagedStorables { get; set; } = new List<JSONClass>();
		//public EntrySubItem EntrySubItemToggle { get;set;}
		public string SubstituteWithSceneAtom { get; set; }
	}
}
