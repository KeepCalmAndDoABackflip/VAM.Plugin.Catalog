
using UnityEngine;

namespace juniperD.Utils
{
	public class Helpers
	{
		public static Texture2D LoadImageFromFile(string filePath)
		{
			return TextureLoader.LoadTexture(filePath);
		}
	}
}
