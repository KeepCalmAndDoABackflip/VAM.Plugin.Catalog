
using UnityEngine;

namespace juniperD.Models
{
	public class ImageInfo
	{
		// Serialized...
		public TextureFormat Format { get; set;}
		public float Height { get; set; }
		public float Width { get; set; }		
		public bool IsDymeCompressed { get; set; }
		public bool IsB64Encoded { get; set; }
		public string ExternalPath { get; set; }

		// Non-Serialized...
		public Texture2D Texture { get; set; }
	}
}
