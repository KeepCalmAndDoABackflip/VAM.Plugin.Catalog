using SimpleJSON;
using System.Collections.Generic;

namespace juniperD.Models
{

	public class Mutation
	{
		// Serialized...
		public bool IsActive { get; set; } = false;
		
		public string ImageExternalPath { get; set; }	//...Deprecated	
		public string ScenePathToOpen { get; set; }
		public List<MorphMutation> FaceGenMorphSet { get; set; } = new List<MorphMutation>();
		public List<ClothingMutation> ClothingItems { get; set; } = new List<ClothingMutation>();
		public List<HairMutation> HairItems { get; set; } = new List<HairMutation>();
		public List<DynamicMutation> DynamicItems { get; set; } = new List<DynamicMutation>();
		public List<MorphMutation> ActiveMorphs { get; set; } = new List<MorphMutation>();
		public List<StoredAtom> StoredAtoms { get; set; } = new List<StoredAtom>();
		public List<JSONClass> Storables { get;set; } = new List<JSONClass>();
		//public string Img_RGB24_W1000H1000_64bEncoded { get; set; }
		//public string ImageData { get; set; }
		
		public string AtomType { get; set; } //...Deprecated
		public string AtomName { get; set; } //...Deprecated

		// Non-Serialized...


		public Mutation()
		{
		}

	}

}
