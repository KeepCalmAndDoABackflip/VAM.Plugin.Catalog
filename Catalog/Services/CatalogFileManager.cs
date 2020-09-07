using juniperD.Contracts;
using juniperD.Models;
using juniperD.Services.CatalogSerializers;
using System.Collections.Generic;

namespace juniperD.Services
{
	public class CatalogFileManager
	{

		public static void SaveCatalog(Catalog catalog, string filePath)
		{
			string catalogData = SerializerService_3_0_2.SaveCatalog(catalog);
			// Save the data to file...
			SuperController.singleton.SaveStringIntoFile(filePath, catalogData);
		}

		public static Catalog LoadCatalog(string filePath)
		{
			Catalog catalog = null;
			string fileContents = SuperController.singleton.ReadFileIntoString(filePath);
			var catalogVersion = GetCatalogVersionFromFile(fileContents);
			SuperController.LogMessage("Catalog version: " + catalogVersion);
			switch (catalogVersion)
			{
				case "3.0.0":
					catalog = SerializerService_3_0_0.LoadCatalog(fileContents);
					break;
				case "3.0.1":
					catalog = SerializerService_3_0_1.LoadCatalog(fileContents);
					break;
				case "3.0.2":
					catalog = SerializerService_3_0_2.LoadCatalog(fileContents);
					break;
			}
			
			// Upgrade catalog...
			if (catalog.PluginVersion == "3.0.0") catalog = GetV3_0_1FromV3_0_0(catalog);
			if (catalog.PluginVersion == "3.0.1") catalog = GetV3_0_2FromV3_0_1(catalog);

			return catalog;
		}

		private static Catalog GetV3_0_1FromV3_0_0(Catalog catalog)
		{
			foreach (var entry in catalog.Entries)
			{
				if (entry.CatalogMode != CatalogModeEnum.CATALOG_MODE_SESSION) continue;
				var mutation = entry.Mutation;
				var storedAtom = new StoredAtom()
				{
					Active = true,
					AtomName = mutation.AtomName,
					AtomType = mutation.AtomType,
					Storables = mutation.Storables
				};
				mutation.StoredAtoms = new List<StoredAtom>() { storedAtom };
			}
			catalog.PluginVersion = "3.0.1";
			return catalog;
		}

		private static Catalog GetV3_0_2FromV3_0_1(Catalog catalog)
		{
			foreach (var entry in catalog.Entries)
			{
				entry.ImageInfo = new ImageInfo();
				entry.ImageInfo.Width = 1000;
				entry.ImageInfo.Height = 1000;
				entry.ImageInfo.IsB64Encoded = true;
				entry.ImageInfo.IsDymeCompressed = true;
				entry.ImageInfo.Texture = entry.ImageAsTexture;
				entry.ImageInfo.Format  = UnityEngine.TextureFormat.DXT1;
				entry.ImageInfo.ExternalPath = entry.Mutation.ImageExternalPath;
				catalog.PluginVersion = "3.0.2";
			}
			return catalog;
		}

		private static string GetCatalogVersionFromFile(string fileContents)
		{
			var openTag = "\"PluginVersion\":\"";
			var closeTag = "\"";
			int startIndex = fileContents.IndexOf(openTag);
			if (startIndex == -1) return "0.0.0";
			startIndex = startIndex + openTag.Length;
			int endIndex = fileContents.IndexOf(closeTag, startIndex);
			if (endIndex == -1) return "0.0.0";
			var versionTextLength = endIndex - startIndex;
			var version = fileContents.Substring(startIndex, versionTextLength);
			return version;
		}
	}
}
