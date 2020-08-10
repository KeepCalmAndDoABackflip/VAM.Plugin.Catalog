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
	}
}
