using CataloggerPlugin.Models;
using Dyme.Compression;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CataloggerPlugin.Services
{
	class SerializerService
	{
		public static string HOST_PLUGIN_SYMANTIC_VERSION = "2.1.0";
		public static string CATALOG_DEAP_VERSION = "0.0.1";
		// Deap version 
		// Revision: 0.0.1
		//	- Added fields: 
		//		- ./DeapVersion, 
		//		- ./PluginVersion,
		//		- ./VersionMessages

		public static string DO_LOAD_ANYWAY = "loadAnyway";
		public static string DO_NOT_LOAD = "dontLoad";

		const string IMAGE_START_TAG = "#IMAGE_START#";
		const string IMAGE_END_TAG = "#IMAGE_END#";
		const string IMAGE_PLACEHOLDER = "#IMAGE_PLACEHOLDER{ImageIndex}#";

		const string HOST_IS_GREATER_THAN = "hostGreaterThan";
		const string HOST_IS_LESS_THAN = "hostLessThan";
		const string HOST_EQUALS = "hostEquals";

		static List<VersionMessage> versionMessages = new List<VersionMessage>()
		{
			new VersionMessage()
			{
				If = HOST_IS_LESS_THAN,
				Version = HOST_PLUGIN_SYMANTIC_VERSION,
				LongMessage = "Catalog plugin has been updated",
				ShortMessage = "Please update to V" + HOST_PLUGIN_SYMANTIC_VERSION,
				Then = DO_LOAD_ANYWAY
			}
		};

		// Pros: 
		//	- Enabling DxtCompression makes a big difference in catalog file size, 
		// Cons: 
		//	- Adds an in-game performance hit (Needs investigation).
		//	- Enabling this means anyone with an older plugin trying to open a newer catalog would have to upgrade their plugin first.
		//  - Cannot add border when enabled (the discard, keep and selected borders)
		// We'll leave it disabled for now, and re-enable it some time in the future once we've figured out how to not affect the in-game performance.
		static bool UseDxtCompression = true; 

		public static string SaveCatalog(Catalog catalog)
		{
			// Remove images data to avoid crashing on serialization...
			PreSerializeStubOutImageDataIfRawImageData(ref catalog);

			// Serialize the catalog...
			catalog.VersionMessages = versionMessages;
			var catalogJson = SaveJsonFromCatalog(catalog);
			var catalogData = catalogJson.ToString();
			
			// Inject image data back into serialized Json...
			catalogData = PostSerializeInjectImageData(catalogData, catalog);
			return catalogData;
		}

		private static JSONClass SaveJsonFromCatalog(Catalog catalog)
		{
			var newJson = new JSONClass();
			          
			newJson.Add("DeapVersion", new JSONData(catalog.DeapVersion));
			newJson.Add("PluginVersion", new JSONData(catalog.PluginVersion));

			newJson.Add("CaptureHair", new JSONData(catalog.CaptureHair));
			newJson.Add("CaptureClothes", new JSONData(catalog.CaptureClothes));
			newJson.Add("CaptureMorphs", new JSONData(catalog.CaptureMorphs));

			JSONArray versionMessages = new JSONArray();
			catalog.VersionMessages.Select(ce => SaveJsonFromVersionMessage(ce)).ToList().ForEach(versionMessages.Add);
			newJson.Add("VersionMessages", versionMessages);

			JSONArray catalogEntries = new JSONArray();
			catalog.Entries.Select(ce => SaveJsonFromCatalogEntry(ce)).ToList().ForEach(catalogEntries.Add);
			newJson.Add("Entries", catalogEntries);

			return newJson;
		}

		private static JSONClass SaveJsonFromVersionMessage(VersionMessage versionMessage)
		{
			var newJson = new JSONClass();
			newJson.Add("If", SaveJsonFromString(versionMessage.If));
			newJson.Add("Version", SaveJsonFromString(versionMessage.Version));
			newJson.Add("LongMessage", SaveJsonFromString(versionMessage.LongMessage));
			newJson.Add("ShortMessage", SaveJsonFromString(versionMessage.ShortMessage));
			newJson.Add("Then", SaveJsonFromString(versionMessage.Then));
			return newJson;
		}

		private static JSONData SaveJsonFromString(string message)
		{
			return new JSONData(message);
		}

		public static Catalog LoadCatalog(string fileData)
		{
			// Extract image data, (we do this before deserialization because the JSON serializer cannot handle the size of the image data, and tends to crash)
			List<string> imageArray = PreDeserializeExtractImageData(ref fileData);

			// Deserialize JSON to Catalog...
			var catalogJson = JSONClass.Parse(fileData).AsObject;
			var newCatalog = LoadCatalogFromJson(catalogJson);

			// Apply image data to Catalog...
			for (var i = 0; i < newCatalog.Entries.Count(); i++)
			{
				var entry = newCatalog.Entries.ElementAt(i);
				if (!string.IsNullOrEmpty(entry.Mutation.ImageExternalPath)) { 
					LoadImageFromFile(entry);
				}
				else {
					LoadDataTexture(imageArray[i], newCatalog, entry);
				}
			}
			return newCatalog;
		}

		private static void LoadImageFromFile(CatalogEntry entry)
		{
			entry.ImageAsTexture = TextureLoader.LoadTexture(entry.Mutation.ImageExternalPath);
		}

		private static void LoadDataTexture(string imageData, Catalog newCatalog, CatalogEntry entry)
		{
			var bytes_B64_Compressed_ = imageData;
			string bytes_B64_ = DymeCompression.Decompress(bytes_B64_Compressed_);
			byte[] bytes_ = SerializerService.B64DecodeBytes(bytes_B64_);
			//===================================================================
			Texture2D texture;
			texture = LoadImageForAppropriateCompressionFormat(entry, bytes_);
			//===================================================================
			entry.Mutation.Img_RGB24_W1000H1000_64bEncoded = bytes_B64_;
			entry.ImageAsTexture = texture;
			entry.ImageAsEncodedString = bytes_B64_;
		}

		private static Texture2D LoadImageForAppropriateCompressionFormat(CatalogEntry entry, byte[] bytes_)
		{
			TextureFormat textureFormat;
			if (entry.ImageFormat == TextureFormat.DXT1.ToString())
			{
				//SuperController.LogMessage("Loading with DXT compression");
				textureFormat = TextureFormat.DXT1;
			}
			else
			{
				textureFormat = TextureFormat.RGB24;
			}
			//if (!IsBelowDeapVersion(newCatalog.PluginVersion, "2.0.1"))
			//{
			//	textureFormat = TextureFormat.RGB24;
			//}
			//else
			//{
			//	textureFormat = UseDxtCompression ? TextureFormat.DXT1: TextureFormat.RGB24;
			//}
			return TextureFromRawData(bytes_, 1000, 1000, textureFormat);
		}

		public static Catalog LoadCatalogFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();

			var pluginVersion = LoadStringFromJsonStringProperty(inputObject, "PluginVersion", HOST_PLUGIN_SYMANTIC_VERSION);
			var deapVersion = LoadStringFromJsonStringProperty(inputObject, "DeapVersion", CATALOG_DEAP_VERSION);
			var versionMessages = LoadObjectArrayFromJsonArrayProperty<VersionMessage>(inputObject, "VersionMessages", nameof(VersionMessage));

			var activeVersionMessage = DetermineCatalogVersionCompatibility(deapVersion, versionMessages);

			var newCatalog = new Catalog()
			{
				DeapVersion = deapVersion,
				PluginVersion = pluginVersion,
				VersionMessages = versionMessages,
				ActiveVersionMessage = activeVersionMessage,
				CaptureClothes = LoadBoolFromJsonStringProperty(inputObject, "CaptureClothes", true),
				CaptureHair = LoadBoolFromJsonStringProperty(inputObject, "CaptureHair", true),
				CaptureMorphs = LoadBoolFromJsonStringProperty(inputObject, "CaptureMorphs", true),
				Entries = inputObject.Childs.ElementAt(keys.IndexOf("Entries"))
					.Childs
					.Select(i => LoadCatalogEntryFromJson(i.AsObject))
					.ToList()
			};
			return newCatalog;
		}

		private static bool LoadBoolFromJsonStringProperty(JSONClass inputObject, string propertyName,  bool defaultValue)
		{
			return bool.Parse(LoadStringFromJsonStringProperty(inputObject, propertyName, defaultValue.ToString()));
		}

		private static JSONClass SaveJsonFromCatalogEntry(CatalogEntry entry)
		{
			var newJson = new JSONClass();
			newJson.Add("Mutation", SaveJsonFromMutation(entry.Mutation));
			newJson.Add("ImageFormat", UseDxtCompression? TextureFormat.DXT1.ToString(): TextureFormat.RGB24.ToString());
			newJson.Add("CatalogMode", entry.CatalogMode);
			newJson.Add("UniqueName", entry.UniqueName);
			return newJson;
		}

		private static CatalogEntry LoadCatalogEntryFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();

			var newCatalogEntry = new CatalogEntry()
			{
				Mutation = keys.IndexOf("Mutation") > -1 ? LoadMutationFromJson(inputObject.Childs.ElementAt(keys.IndexOf("Mutation")).AsObject) : new Mutation(),
				ImageFormat = LoadStringFromJsonStringProperty(inputObject, "ImageFormat", null),
				CatalogMode = LoadStringFromJsonStringProperty(inputObject, "CatalogMode", null),
				UniqueName = LoadStringFromJsonStringProperty(inputObject, "UniqueName", null)
			};
			
			return newCatalogEntry;
		}

		private static object LoadVersionMessageFromJson(JSONClass item)
		{
			return new VersionMessage()
			{
				If = LoadStringFromJsonStringProperty(item, "If", null),
				Version = LoadStringFromJsonStringProperty(item, "Version", null),
				LongMessage = LoadStringFromJsonStringProperty(item, "LongMessage", null),
				ShortMessage = LoadStringFromJsonStringProperty(item, "ShortMessage", null),
				Then = LoadStringFromJsonStringProperty(item, "Then", null),
			};
		}

		public static JSONClass SaveJsonFromMutation(Mutation mutation)
		{
			var newJson = new JSONClass();
			newJson.Add("IsActive", new JSONData(mutation.IsActive.ToString()));
			newJson.Add("ImageExternalPath", new JSONData(mutation.ImageExternalPath));
			newJson.Add("AtomName", new JSONData(mutation.AtomName));
			newJson.Add("AtomType", new JSONData(mutation.AtomType));
			newJson.Add("AssetUrl", new JSONData(mutation.AssetUrl));
			newJson.Add("AssetName", new JSONData(mutation.AssetName));
			newJson.Add("ScenePathToOpen", SaveJsonFromString(mutation.ScenePathToOpen));

			JSONArray morphSet = new JSONArray();
			mutation.FaceGenMorphSet.Select(i => SaveJsonFromMorphSet(i)).ToList().ForEach(morphSet.Add);
			newJson.Add("MorphSet", morphSet);

			JSONArray clothinItems = new JSONArray();
			mutation.ClothingItems.Select(i => SaveJsonFromClothingMutation(i)).ToList().ForEach(clothinItems.Add);
			newJson.Add("ClothingItems", clothinItems);

			JSONArray hairItems = new JSONArray();
			mutation.HairItems.Select(i => SaveJsonFromHairMutation(i)).ToList().ForEach(hairItems.Add);
			newJson.Add("HairItems", hairItems);

			JSONArray activeMorphs = new JSONArray();
			mutation.ActiveMorphs.Select(i => SaveJsonFromActiveMorphMutation(i)).ToList().ForEach(activeMorphs.Add);
			newJson.Add("ActiveMorphs", activeMorphs);

			newJson.Add("Img_RGB24_W1000H1000_64bEncoded", new JSONData(mutation.Img_RGB24_W1000H1000_64bEncoded));
			return newJson;
		}

		public static Mutation LoadMutationFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();

			var newMutation = new Mutation()
			{
				IsActive = bool.Parse(LoadStringFromJsonStringProperty(inputObject, "IsActive", true.ToString())), // keys.IndexOf("IsActive") > -1 ? bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("IsActive"))) : true,
				ImageExternalPath = LoadStringFromJsonStringProperty(inputObject, "ImageExternalPath", null),
				ScenePathToOpen = LoadStringFromJsonStringProperty(inputObject, "ScenePathToOpen", null),
				AtomName = LoadStringFromJsonStringProperty(inputObject, "AtomName", null),
				AtomType = LoadStringFromJsonStringProperty(inputObject, "AtomType", null),
				AssetUrl = LoadStringFromJsonStringProperty(inputObject, "AssetUrl", null),
				AssetName = LoadStringFromJsonStringProperty(inputObject, "AssetName", null),
				FaceGenMorphSet = inputObject.Childs.ElementAt(keys.IndexOf("MorphSet"))
					.Childs
					.Select(i => LoadMorphMutationFromJson(i.AsObject))
					.ToList(),
				ClothingItems = inputObject.Childs.ElementAt(keys.IndexOf("ClothingItems"))
					.Childs
					.Select(i => LoadClothingMutationFromJson(i.AsObject))
					.ToList(),
				HairItems = inputObject.Childs.ElementAt(keys.IndexOf("HairItems"))
					.Childs
					.Select(i => LoadHairMutationFromJson(i.AsObject))
					.ToList(),
				ActiveMorphs = inputObject.Childs.ElementAt(keys.IndexOf("ActiveMorphs"))
					.Childs
					.Select(i => LoadActiveMorphMutationFromJson(i.AsObject))
					.ToList(),
			};
			if (string.IsNullOrEmpty(newMutation.ImageExternalPath)) newMutation.ImageExternalPath = null; //...make NULL (instead of empty string)
			if (string.IsNullOrEmpty(newMutation.ScenePathToOpen)) newMutation.ScenePathToOpen = null; //...make NULL (instead of empty string)
			return newMutation;
		}

		public static JSONClass SaveJsonFromMorphSet(MorphMutation morphMutation)
		{
			var newJson = new JSONClass();
			newJson.Add("Name", new JSONData(morphMutation.Name));
			newJson.Add("PreviousValue", new JSONData(morphMutation.PreviousValue.ToString()));
			newJson.Add("Value", new JSONData(morphMutation.Value.ToString()));
			newJson.Add("Active", new JSONData(morphMutation.Active.ToString()));
			return newJson;
		}

		public static MorphMutation LoadMorphMutationFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new MorphMutation()
			{
				Name = inputObject.Childs.ElementAt(keys.IndexOf("Name")).Value,
				PreviousValue = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("PreviousValue")).Value),
				Value = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Value")).Value),
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		public static JSONClass SaveJsonFromClothingMutation(ClothingMutation clothingMutation)
		{
			//var clothingAsStorable = clothingMutation.DAZClothingItem as JSONStorableDynamic;
			var newJson = new JSONClass();
			newJson.Add("DAZClothingItemName", new JSONData(clothingMutation.DAZClothingItemName));
			//newJson.Add("DAZClothingItem", clothing as JSONNode;
			newJson.Add("Active", new JSONData(clothingMutation.Active.ToString()));
			return newJson;
		}

		public static ClothingMutation LoadClothingMutationFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var clothingMutation = new ClothingMutation()
			{
				DAZClothingItemName = inputObject.Childs.ElementAt(keys.IndexOf("DAZClothingItemName")).Value,
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return clothingMutation;
		}

		public static JSONNode SaveJsonFromHairMutation(HairMutation hairMutation)
		{
			var newJson = new JSONClass();
			newJson.Add("DAZHairGroupName", new JSONData(hairMutation.DAZHairGroupName));
			newJson.Add("Active", new JSONData(hairMutation.Active.ToString()));
			return newJson;
		}

		public static HairMutation LoadHairMutationFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new HairMutation()
			{
				DAZHairGroupName = inputObject.Childs.ElementAt(keys.IndexOf("DAZHairGroupName")).Value,
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		public static JSONNode SaveJsonFromDynamicMutation(DynamicMutation mutation)
		{
			var newJson = new JSONClass();
			newJson.Add("DAZDynamicItemName", new JSONData(mutation.DAZDynamicItemName));
			newJson.Add("Active", new JSONData(mutation.Active.ToString()));
			return newJson;
		}

		public static DynamicMutation LoadDynamicMutationFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new DynamicMutation()
			{
				DAZDynamicItemName = inputObject.Childs.ElementAt(keys.IndexOf("DAZDynamicItemName")).Value,
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		public static JSONNode SaveJsonFromActiveMorphMutation(MorphMutation mutationComponent)
		{
			var newJson = new JSONClass();
			newJson.Add("Name", new JSONData(mutationComponent.Name));
			newJson.Add("Value", new JSONData(mutationComponent.Value));
			newJson.Add("PreviousValue", new JSONData(mutationComponent.PreviousValue));
			newJson.Add("Active", new JSONData(mutationComponent.Active));
			return newJson;
		}

		public static MorphMutation LoadActiveMorphMutationFromJson(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new MorphMutation()
			{
				Name = inputObject.Childs.ElementAt(keys.IndexOf("Name")).Value,
				Value = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Value")).Value),
				PreviousValue = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("PreviousValue")).Value),
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		private static VersionMessage DetermineCatalogVersionCompatibility(string catalogDeapVersion, IEnumerable<VersionMessage> pluginMessages)
		{
			if (catalogDeapVersion != null) { 
				var catalogFieldsRemoved = int.Parse(catalogDeapVersion.Split('.').First());
				var pluginFieldsRemoved = int.Parse(CATALOG_DEAP_VERSION.Split('.').First());
				var catalogHasRemovedFields = catalogFieldsRemoved > pluginFieldsRemoved; // Catalog is newer and has removed fields. May Break. Plugin may ne expecting certain fields.
				var catalogHasExtraOldFields = catalogFieldsRemoved < pluginFieldsRemoved; // Catlog is older. Plugin has removed fields. No break. Catalog has fields that won't be used by the plugin.
				if (catalogHasRemovedFields) SuperController.LogMessage($"WARNING: The catalog you are opening is newer and may not contain all neccessary fields (catalog version: {catalogDeapVersion}, your version: {CATALOG_DEAP_VERSION}). Please update the plugin.");
				var catalogFieldsAdded = int.Parse(catalogDeapVersion.Split('.')[2]);
				var pluginFieldsAdded = int.Parse(CATALOG_DEAP_VERSION.Split('.')[2]);
				var catalogHasNewFields = catalogFieldsAdded > pluginFieldsAdded; // Catalog is newer, and has added fields. No Break. Catalog has fields that won't be used by the plugin.
				var pluginHasNewFields = catalogFieldsAdded < pluginFieldsAdded; // Catalog is older. May break. Plugin may be expecting certain fields.
				if (pluginHasNewFields) SuperController.LogMessage($"WARNING: The catalog you are opening is older and may not contain all neccessary fields (catalog version: {catalogDeapVersion}, your version: {CATALOG_DEAP_VERSION}).");
			}
			foreach (var pluginMessage in pluginMessages)
			{
				if (MessageApplies(pluginMessage))
				{
					SuperController.LogMessage(pluginMessage.LongMessage);
					return pluginMessage;
				}
			}
			return null;
		}

		private static bool MessageApplies(VersionMessage pluginMessage)
		{
			switch (pluginMessage.If)
			{
				case HOST_EQUALS: return pluginMessage.Version == HOST_PLUGIN_SYMANTIC_VERSION;
				case HOST_IS_GREATER_THAN: return pluginMessage.Version != HOST_PLUGIN_SYMANTIC_VERSION && IsVersion1GreaterThanVersion2(HOST_PLUGIN_SYMANTIC_VERSION, pluginMessage.Version);
				case HOST_IS_LESS_THAN: return pluginMessage.Version != HOST_PLUGIN_SYMANTIC_VERSION && IsVersion1GreaterThanVersion2(pluginMessage.Version, HOST_PLUGIN_SYMANTIC_VERSION);
			}
			return false;
		}

		private static bool IsVersion1GreaterThanVersion2(string version1, string version2)
		{
			var version1Numbers = version1.Split('.').Select(int.Parse).ToList();
			var version2Numbers = version2.Split('.').Select(int.Parse).ToList();
			if (version1Numbers[0] < version2Numbers[0]) return false;
			if (version1Numbers[1] < version2Numbers[1]) return false;
			if (version1Numbers[2] < version2Numbers[2]) return false;
			return true;
		}

		private static bool IsBelowDeapVersion(string catalogVersion, string pluginVersion)
		{
			var catalogVersionNumbers = catalogVersion.Split('.').Select(int.Parse).ToList();
			var pluginVersionNumbers = pluginVersion.Split('.').Select(int.Parse).ToList();
			return catalogVersionNumbers[0] < pluginVersionNumbers[0]
				|| catalogVersionNumbers[1] < pluginVersionNumbers[1]
				|| catalogVersionNumbers[2] < pluginVersionNumbers[2];
		}

		private static void PreSerializeStubOutImageDataIfRawImageData(ref Catalog catalog)
		{
			var entries = catalog.Entries.ToArray();
			// Serialize the mutations...
			for (var i = 0; i < entries.Count(); i++)
			{
				var entry = entries[i];
				if (entry == null) continue;
				entry.Mutation.Img_RGB24_W1000H1000_64bEncoded = IMAGE_PLACEHOLDER.Replace("{ImageIndex}", i.ToString());
			}
		}

		private static string PostSerializeInjectImageData(string catalogData, Catalog catalog)
		{
			var entries = catalog.Entries.ToArray();
			// Inject image data... (we do this after serialization because the JSON serializer can't handle the size of the image data, and tends to crash.).
			for (var i = 0; i < entries.Count(); i++)
			{
				var entry = entries[i];
				string imageData;
				if (!string.IsNullOrEmpty(entry.Mutation.ImageExternalPath))
				{
					imageData = entry.Mutation.ImageExternalPath;
				}
				else { 
					if (UseDxtCompression) { 
						// Perform DXT compression...
						entry.ImageAsTexture.Compress(false);
						entry.ImageAsTexture.Apply();
					}
					byte[] bytes_ = entry.ImageAsTexture.GetRawTextureData();
					string bytes_B64_ = SerializerService.B64EncodeBytes(bytes_);
					string bytes_B64_Compressed_ = DymeCompression.Compress(bytes_B64_, true, '|');
					imageData = bytes_B64_Compressed_;
				}
				catalogData = catalogData.Replace(IMAGE_PLACEHOLDER.Replace("{ImageIndex}", i.ToString()), $"{IMAGE_START_TAG}{imageData}{IMAGE_END_TAG}");
			}
			return catalogData;
		}

		private static Texture2D TextureFromRawData(byte[] rawData, int width = 1000, int height = 1000, TextureFormat textureFormat = TextureFormat.RGB24)
		{
			try
			{
				Texture2D texture = new Texture2D(width, height, textureFormat, false);
				texture.LoadRawTextureData(rawData);
				texture.Apply();
				return texture;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		private static List<string> PreDeserializeExtractImageData(ref string fileData)
		{
			var overflow = 0;
			var imageArray = new List<string>();
			while (fileData.IndexOf(IMAGE_START_TAG) > -1)
			{
				if (++overflow > 10000) throw new OverflowException("Catalog file may be corrupted");
				var nextImageStartPos = fileData.IndexOf(IMAGE_START_TAG) + IMAGE_START_TAG.Length;
				if (nextImageStartPos == -1) break;
				int imageEndIndex = fileData.IndexOf(IMAGE_END_TAG);
				string bytes_B64_Compressed_ = fileData.Substring(nextImageStartPos, imageEndIndex - nextImageStartPos);
				fileData = fileData.Substring(0, nextImageStartPos - IMAGE_START_TAG.Length) + imageArray.Count() + fileData.Substring(imageEndIndex + IMAGE_END_TAG.Length);
				imageArray.Add(bytes_B64_Compressed_);
			}

			return imageArray;
		}


		private static List<T> LoadObjectArrayFromJsonArrayProperty<T>(JSONClass parentJsonObject, string propertyName, string returnTypeName)
		{
			if (!PropExists(parentJsonObject, propertyName)) return new List<T> { };
			var keys = parentJsonObject.Keys.ToList();
			var jsonArray = parentJsonObject.Childs.ElementAt(keys.IndexOf(propertyName)).AsArray;
			return jsonArray.Childs.Select(item => (T)LoadObjectFromJson<T>(item.AsObject, returnTypeName)).ToList();
		}

		private static string LoadStringFromJsonStringProperty(JSONClass parentJsonObject, string propertyName, string defaultValue)
		{
			if (!PropExists(parentJsonObject, propertyName)) return defaultValue;
			var keys = parentJsonObject.Keys.ToList();
			var jsonNode = parentJsonObject.Childs.ElementAt(keys.IndexOf(propertyName));
			return jsonNode.Value;
		}

		private static T LoadObjectFromJsonObjectProperty<T>(JSONClass parentJsonObject, string propertyName, string typeName, T defaultValue)
		{
			if (!PropExists(parentJsonObject, propertyName)) return defaultValue;
			var keys = parentJsonObject.Keys.ToList();
			var jsonObject = parentJsonObject.Childs.ElementAt(keys.IndexOf(propertyName)).AsObject;
			return (T)LoadObjectFromJson<T>(jsonObject, typeName);
		}

		private static object LoadObjectFromJson<T>(JSONClass item, string returnTypeName)
		{
			switch (returnTypeName)
			{
				case "string": return item.Value;
				case nameof(VersionMessage): return LoadVersionMessageFromJson(item);
				default: return item.Value;
			}
		}

		private static bool PropExists(JSONClass inputObject, string propName)
		{
			var keys = inputObject.Keys.ToList();
			return keys.IndexOf(propName) > -1;
		}

		public static string EncodeString(string input)
		{
			return Convert.ToBase64String(Encoding.ASCII.GetBytes(input));
		}

		public static string DecodeString(string serializedInput)
		{
			return Encoding.ASCII.GetString(Convert.FromBase64String(serializedInput));
		}

		public static byte[] B64DecodeBytes(string serializedInput)
		{
			return Convert.FromBase64String(serializedInput);
		}

		public static string B64EncodeBytes(byte[] input)
		{
			return Convert.ToBase64String(input);
		}


	}
}
