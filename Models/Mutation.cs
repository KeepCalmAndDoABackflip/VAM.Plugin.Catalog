using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CataloggerPlugin.Models
{

	public class Mutation
	{
		// Serialized...
		public bool IsActive { get; set; } = false;
		public string ImageExternalPath { get; set; }
		public string ScenePathToOpen { get; set; }
		public List<MorphMutation> FaceGenMorphSet { get; set; } = new List<MorphMutation>();
		public List<ClothingMutation> ClothingItems { get; set; } = new List<ClothingMutation>();
		public List<HairMutation> HairItems { get; set; } = new List<HairMutation>();
		public List<DynamicMutation> DynamicItems { get; set; } = new List<DynamicMutation>();
		public List<MorphMutation> ActiveMorphs { get; set; } = new List<MorphMutation>();
		public string Img_RGB24_W1000H1000_64bEncoded { get; set; }
		public string AssetName { get; set; }
		public string AssetUrl { get; set; }
		public string AtomType { get; set; }
		public string AtomName { get; set; }
		// Non-Serialized...


		public Mutation()
		{
		}

	}

}
