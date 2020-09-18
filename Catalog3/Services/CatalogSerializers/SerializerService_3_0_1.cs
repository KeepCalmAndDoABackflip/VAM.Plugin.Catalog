﻿using juniperD.Models;
using Dyme.Compression;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace juniperD.Services.CatalogSerializers
{
	class SerializerService_3_0_1
	{
		public static string CATALOG_SYMANTIC_VERSION = "3.0.1";
		public static string CATALOG_DEAP_VERSION = "2.0.2";
		// Deap version 
		// Revision: 0.0.1
		//	- Added fields: 
		//		- ./DeapVersion, 
		//		- ./PluginVersion,
		//		- ./VersionMessages

		public static string DO_LOAD_ANYWAY = "loadAnyway";
		public static string DO_NOT_LOAD = "dontLoad";

		const string IMAGE_SECTION_PREFIX = "\n\n";
		const string IMAGE_TAG_FRONT_PART = "<IMAGE#";
		const string IMAGE_TAG_BACK_PART = ">";

		const string HOST_IS_GREATER_THAN = "hostGreaterThan";
		const string HOST_IS_LESS_THAN = "hostLessThan";
		const string HOST_EQUALS = "hostEquals";

		static List<VersionMessage> versionMessages = new List<VersionMessage>()
		{
			new VersionMessage()
			{
				If = HOST_IS_LESS_THAN,
				Version = CATALOG_SYMANTIC_VERSION,
				LongMessage = "Catalog plugin has been updated",
				ShortMessage = "Please update to V" + CATALOG_SYMANTIC_VERSION,
				Then = DO_LOAD_ANYWAY
			}
		};

		static bool UseDxtCompression = true; 

		public static string SaveCatalog(Catalog catalog)
		{
			// Remove images data to avoid crashing on serialization...
			PreSerializeStubOutImageDataIfRawImageData(ref catalog);

			// Serialize the catalog...
			catalog.VersionMessages = versionMessages;
			var catalogJson = SerializeCatalog(catalog);
			var catalogData = catalogJson.ToString();
			
			// Inject image data back into serialized Json...
			catalogData = PostSerializeInjectImageData(catalogData, catalog);
			return catalogData;
		}


		public static Catalog LoadCatalog(string fileContents)
		{
			// Extract image data, (we do this before deserialization because the JSON serializer cannot handle the size of the image data, and tends to crash)
			Dictionary<int, string> imageArray = PreDeserializeExtractImageData(ref fileContents);

			// Deserialize JSON to Catalog...
			var catalogJson = JSONClass.Parse(fileContents).AsObject;
			var newCatalog = DeserializeIntoCatalog(catalogJson);

			// Apply image data to Catalog...
			for (var i = 0; i < newCatalog.Entries.Count(); i++)
			{
				var entry = newCatalog.Entries.ElementAt(i);
				if (!string.IsNullOrEmpty(entry.Mutation.ImageExternalPath))
				{
					entry.ImageAsTexture = LoadImageFromFile(entry.Mutation.ImageExternalPath);
				}
				else
				{
					if (imageArray.ContainsKey(i)) LoadDataTexture(imageArray[i], entry);
				}
			}
			return newCatalog;
		}

		private static JSONClass SerializeCatalog(Catalog catalog)
		{
			var newJson = new JSONClass();
			          
			newJson.Add("DeapVersion", new JSONData(catalog.DeapVersion));
			newJson.Add("PluginVersion", new JSONData(catalog.PluginVersion));

			newJson.Add("CaptureHair", new JSONData(catalog.CaptureHair));
			newJson.Add("CaptureClothes", new JSONData(catalog.CaptureClothes));
			newJson.Add("CaptureMorphs", new JSONData(catalog.CaptureMorphs));

			JSONArray versionMessages = new JSONArray();
			catalog.VersionMessages.Select(ce => SerializeVersionMessage(ce)).ToList().ForEach(versionMessages.Add);
			newJson.Add("VersionMessages", versionMessages);

			JSONArray catalogEntries = new JSONArray();
			catalog.Entries.Select(ce => SerializeCatalogEntry(ce)).ToList().ForEach(catalogEntries.Add);
			newJson.Add("Entries", catalogEntries);

			return newJson;
		}

		private static JSONClass SerializeVersionMessage(VersionMessage versionMessage)
		{
			var newJson = new JSONClass();
			newJson.Add("If", SerializeString(versionMessage.If));
			newJson.Add("Version", SerializeString(versionMessage.Version));
			newJson.Add("LongMessage", SerializeString(versionMessage.LongMessage));
			newJson.Add("ShortMessage", SerializeString(versionMessage.ShortMessage));
			newJson.Add("Then", SerializeString(versionMessage.Then));
			return newJson;
		}

		private static JSONData SerializeString(string message)
		{
			return new JSONData(message);
		}


		private static Texture2D LoadImageFromFile(string path)
		{
			 return TextureLoader.LoadTexture(path);
		}

		private static void LoadDataTexture(string imageData, CatalogEntry entry)
		{
			var bytes_B64_Compressed_ = imageData;
			string bytes_B64_ = DymeCompression.Decompress(bytes_B64_Compressed_);
			byte[] bytes_ = B64DecodeBytes(bytes_B64_);
			//===================================================================
			Texture2D texture;
			texture = LoadImageForAppropriateCompressionFormat(entry, bytes_);
			//===================================================================
			entry.ImageAsTexture = texture;
		}

		private static Texture2D LoadImageForAppropriateCompressionFormat(CatalogEntry entry, byte[] bytes_)
		{
			TextureFormat textureFormat;
			if (entry.ImageFormat == TextureFormat.DXT1.ToString())
			{
				textureFormat = TextureFormat.DXT1;
			}
			else
			{
				textureFormat = TextureFormat.RGB24;
			}
			return TextureFromRawData(bytes_, 1000, 1000, textureFormat);
		}

		private static Catalog DeserializeIntoCatalog(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();

			var pluginVersion = LoadStringFromJsonStringProperty(inputObject, "PluginVersion", CATALOG_SYMANTIC_VERSION);
			var deapVersion = LoadStringFromJsonStringProperty(inputObject, "DeapVersion", CATALOG_DEAP_VERSION);
			var versionMessages = LoadObjectArrayFromJsonArrayProperty<VersionMessage>(inputObject, "VersionMessages", nameof(VersionMessage));

			var activeVersionMessage = DetermineCatalogVersionCompatibility(deapVersion, versionMessages);

			var newCatalog = new Catalog()
			{
				DeapVersion = deapVersion,
				PluginVersion = pluginVersion,
				VersionMessages = versionMessages,
				ActiveVersionMessage = activeVersionMessage,
				CaptureClothes = DeserializeBoolStringIntoBool(inputObject, "CaptureClothes", true),
				CaptureHair = DeserializeBoolStringIntoBool(inputObject, "CaptureHair", true),
				CaptureMorphs = DeserializeBoolStringIntoBool(inputObject, "CaptureMorphs", true),
				Entries = inputObject.Childs.ElementAt(keys.IndexOf("Entries"))
					.Childs
					.Select(i => DeserializeIntoCatalogEntry(i.AsObject))
					.ToList()
			};
			return newCatalog;
		}

		private static bool DeserializeBoolStringIntoBool(JSONClass inputObject, string propertyName,  bool defaultValue)
		{
			return bool.Parse(LoadStringFromJsonStringProperty(inputObject, propertyName, defaultValue.ToString()));
		}

		private static JSONClass SerializeCatalogEntry(CatalogEntry entry)
		{
			var newJson = new JSONClass();
			newJson.Add("Mutation", SerializeMutation(entry.Mutation));
			newJson.Add("ImageFormat", UseDxtCompression? TextureFormat.DXT1.ToString(): TextureFormat.RGB24.ToString());
			newJson.Add("CatalogMode", entry.CatalogEntryMode);
			newJson.Add("UniqueName", entry.UniqueName);
			return newJson;
		}

		private static CatalogEntry DeserializeIntoCatalogEntry(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();

			var newCatalogEntry = new CatalogEntry()
			{
				Mutation = keys.IndexOf("Mutation") > -1 ? DeserializeIntoMutation(inputObject.Childs.ElementAt(keys.IndexOf("Mutation")).AsObject) : new Mutation(),
				ImageFormat = LoadStringFromJsonStringProperty(inputObject, "ImageFormat", null),
				CatalogEntryMode = LoadStringFromJsonStringProperty(inputObject, "CatalogMode", null),
				UniqueName = LoadStringFromJsonStringProperty(inputObject, "UniqueName", null)
			};
			
			return newCatalogEntry;
		}

		private static VersionMessage DeserializeIntoVersionMessage(JSONClass item)
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

		private static JSONClass SerializeMutation(Mutation mutation)
		{
			var newJson = new JSONClass();
			newJson.Add("IsActive", new JSONData(mutation.IsActive.ToString()));
			newJson.Add("ImageExternalPath", new JSONData(mutation.ImageExternalPath));
			newJson.Add("AtomName", new JSONData(mutation.AtomName));
			newJson.Add("AtomType", new JSONData(mutation.AtomType));
			newJson.Add("ScenePathToOpen", SerializeString(mutation.ScenePathToOpen));


			JSONArray morphSet = new JSONArray();
			mutation.FaceGenMorphSet.Select(i => SerializeMorphSet(i)).ToList().ForEach(morphSet.Add);
			newJson.Add("MorphSet", morphSet);

			JSONArray clothinItems = new JSONArray();
			mutation.ClothingItems.Select(i => SerializeClothingMutation(i)).ToList().ForEach(clothinItems.Add);
			newJson.Add("ClothingItems", clothinItems);

			JSONArray hairItems = new JSONArray();
			mutation.HairItems.Select(i => SerializeHairMutation(i)).ToList().ForEach(hairItems.Add);
			newJson.Add("HairItems", hairItems);

			JSONArray activeMorphs = new JSONArray();
			mutation.ActiveMorphs.Select(i => SerializeActiveMorphMutation(i)).ToList().ForEach(activeMorphs.Add);
			newJson.Add("ActiveMorphs", activeMorphs);

			JSONArray storables = new JSONArray();
			mutation.Storables.ForEach(storables.Add);
			newJson.Add("Storables", storables);

			JSONArray storedAtoms = new JSONArray();
			mutation.StoredAtoms.ForEach(i => storedAtoms.Add(SerializeStoredAtom(i)));
			newJson.Add("StoredAtoms", storedAtoms);

			//newJson.Add("Img_RGB24_W1000H1000_64bEncoded", new JSONData(mutation.Img_RGB24_W1000H1000_64bEncoded));
			return newJson;
		}

		private static Mutation DeserializeIntoMutation(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();

			var newMutation = new Mutation()
			{
				IsActive = bool.Parse(LoadStringFromJsonStringProperty(inputObject, "IsActive", true.ToString())),
				ImageExternalPath = LoadStringFromJsonStringProperty(inputObject, "ImageExternalPath", null),
				ScenePathToOpen = LoadStringFromJsonStringProperty(inputObject, "ScenePathToOpen", null),
				AtomName = LoadStringFromJsonStringProperty(inputObject, "AtomName", null),
				AtomType = LoadStringFromJsonStringProperty(inputObject, "AtomType", null),
				FaceGenMorphSet = inputObject.Childs.ElementAt(keys.IndexOf("MorphSet"))
					.Childs
					.Select(i => DeserializeIntoMorphMutation(i.AsObject))
					.ToList(),
				ClothingItems = inputObject.Childs.ElementAt(keys.IndexOf("ClothingItems"))
					.Childs
					.Select(i => DeserializeIntoClothingMutation(i.AsObject))
					.ToList(),
				HairItems = inputObject.Childs.ElementAt(keys.IndexOf("HairItems"))
					.Childs
					.Select(i => DeserializeIntoHairMutation(i.AsObject))
					.ToList(),
				ActiveMorphs = inputObject.Childs.ElementAt(keys.IndexOf("ActiveMorphs"))
					.Childs
					.Select(i => DeserializeIntoActiveMorphMutation(i.AsObject))
					.ToList(),
			};
			if (keys.Contains("Storables"))
			{
				newMutation.Storables = inputObject.Childs.ElementAt(keys.IndexOf("Storables"))
					.Childs
					.Select(i => i.AsObject)
					.ToList();
			}
			if (keys.Contains("StoredAtoms"))
			{
				newMutation.StoredAtoms = inputObject.Childs.ElementAt(keys.IndexOf("StoredAtoms"))
					.Childs
					.Select(i => DeserializeIntoStoredAtom(i.AsObject))
					.ToList();
			}
			if (string.IsNullOrEmpty(newMutation.ImageExternalPath)) newMutation.ImageExternalPath = null; //...make NULL (instead of empty string)
			if (string.IsNullOrEmpty(newMutation.ScenePathToOpen)) newMutation.ScenePathToOpen = null; //...make NULL (instead of empty string)
			return newMutation;
		}
		private static JSONClass SerializeMorphSet(MorphMutation morphMutation)
		{
			var newJson = new JSONClass();
			newJson.Add("Name", new JSONData(morphMutation.Id));
			newJson.Add("PreviousValue", new JSONData(morphMutation.PreviousValue.ToString()));
			newJson.Add("Value", new JSONData(morphMutation.Value.ToString()));
			newJson.Add("Active", new JSONData(morphMutation.Active.ToString()));
			return newJson;
		}

		private static MorphMutation DeserializeIntoMorphMutation(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new MorphMutation()
			{
				Id = inputObject.Childs.ElementAt(keys.IndexOf("Name")).Value,
				PreviousValue = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("PreviousValue")).Value),
				Value = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Value")).Value),
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		private static JSONClass SerializeClothingMutation(ClothingMutation clothingMutation)
		{
			var newJson = new JSONClass();
			newJson.Add("DAZClothingItemName", new JSONData(clothingMutation.Id));
			newJson.Add("Active", new JSONData(clothingMutation.Active.ToString()));
			return newJson;
		}

		private static ClothingMutation DeserializeIntoClothingMutation(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var clothingMutation = new ClothingMutation()
			{
				Id = inputObject.Childs.ElementAt(keys.IndexOf("DAZClothingItemName")).Value,
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return clothingMutation;
		}

		private static JSONNode SerializeHairMutation(HairMutation hairMutation)
		{
			var newJson = new JSONClass();
			newJson.Add("DAZHairGroupName", new JSONData(hairMutation.Id));
			newJson.Add("Active", new JSONData(hairMutation.Active.ToString()));
			return newJson;
		}

		private static HairMutation DeserializeIntoHairMutation(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new HairMutation()
			{
				Id = inputObject.Childs.ElementAt(keys.IndexOf("DAZHairGroupName")).Value,
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		private static JSONNode SerializeActiveMorphMutation(MorphMutation mutationComponent)
		{
			var newJson = new JSONClass();
			newJson.Add("Name", new JSONData(mutationComponent.Id));
			newJson.Add("Value", new JSONData(mutationComponent.Value));
			newJson.Add("PreviousValue", new JSONData(mutationComponent.PreviousValue));
			newJson.Add("Active", new JSONData(mutationComponent.Active));
			return newJson;
		}

		private static MorphMutation DeserializeIntoActiveMorphMutation(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new MorphMutation()
			{
				Id = inputObject.Childs.ElementAt(keys.IndexOf("Name")).Value,
				Value = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Value")).Value),
				PreviousValue = float.Parse(inputObject.Childs.ElementAt(keys.IndexOf("PreviousValue")).Value),
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value)
			};
			return mutationComponent;
		}

		private static JSONNode SerializeStoredAtom(StoredAtom storedAtom)
		{
			var newJson = new JSONClass();
			newJson.Add("Active", new JSONData(storedAtom.Active));
			newJson.Add("AtomName", new JSONData(storedAtom.AtomName));
			newJson.Add("AtomType", new JSONData(storedAtom.AtomType));

			JSONArray storables = new JSONArray();
			storedAtom.Storables.ForEach(storables.Add);
			newJson.Add("Storables", storables);

			return newJson;
		}

		private static StoredAtom DeserializeIntoStoredAtom(JSONClass inputObject)
		{
			var keys = inputObject.Keys.ToList();
			var mutationComponent = new StoredAtom()
			{
				Active = bool.Parse(inputObject.Childs.ElementAt(keys.IndexOf("Active")).Value),
				AtomType = inputObject.Childs.ElementAt(keys.IndexOf("AtomType")).Value,
				AtomName = inputObject.Childs.ElementAt(keys.IndexOf("AtomName")).Value,
				Storables = inputObject.Childs.ElementAt(keys.IndexOf("Storables"))
					.Childs
					.Select(i => i.AsObject)
					.ToList(),
			};
			return mutationComponent;
		}

		private static VersionMessage DetermineCatalogVersionCompatibility(string catalogDeapVersion, IEnumerable<VersionMessage> pluginMessages)
		{
			if (!string.IsNullOrEmpty(catalogDeapVersion)) {
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
				case HOST_EQUALS: return pluginMessage.Version == CATALOG_SYMANTIC_VERSION;
				case HOST_IS_GREATER_THAN: return pluginMessage.Version != CATALOG_SYMANTIC_VERSION && IsVersion1GreaterThanVersion2(CATALOG_SYMANTIC_VERSION, pluginMessage.Version);
				case HOST_IS_LESS_THAN: return pluginMessage.Version != CATALOG_SYMANTIC_VERSION && IsVersion1GreaterThanVersion2(pluginMessage.Version, CATALOG_SYMANTIC_VERSION);
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
				//entry.Mutation.Img_RGB24_W1000H1000_64bEncoded = "";//IMAGE_PLACEHOLDER.Replace("{ImageIndex}", i.ToString());
			}
		}

		private static string PostSerializeInjectImageData(string catalogData, Catalog catalog)
		{
			var entries = catalog.Entries.ToArray();
			// Inject image data... (we do this after serialization because the JSON serializer can't handle the size of the image data, and tends to crash.).
			catalogData += IMAGE_SECTION_PREFIX;
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
					string bytes_B64_ = B64EncodeBytes(bytes_);
					string bytes_B64_Compressed_ = DymeCompression.Compress(bytes_B64_, true, '|');
					imageData = bytes_B64_Compressed_;
				}
				var imageStartTag = IMAGE_TAG_FRONT_PART + i + IMAGE_TAG_BACK_PART;
				catalogData += imageStartTag + imageData;
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

		private static Dictionary<int, string> PreDeserializeExtractImageData(ref string fileData)
		{
			var overflow = 0;
			var imageArray = new Dictionary<int, string>();
			var imageDataStartindex = fileData.IndexOf(IMAGE_SECTION_PREFIX + IMAGE_TAG_FRONT_PART);
			if (imageDataStartindex == -1) return new Dictionary<int, string>(); // No image data present
			var imageDataSection = fileData.Substring(imageDataStartindex);
			fileData = fileData.Substring(0, fileData.IndexOf(IMAGE_SECTION_PREFIX + IMAGE_TAG_FRONT_PART));
			imageDataSection = imageDataSection.Substring(IMAGE_SECTION_PREFIX.Length);
			while (imageDataSection.IndexOf(IMAGE_TAG_FRONT_PART) > -1)
			{
				if (++overflow > 10000) throw new OverflowException("Catalog file may be corrupted");
				var nextImageStartIndex = imageDataSection.IndexOf(IMAGE_TAG_FRONT_PART);
				if (nextImageStartIndex == -1) break;
				int imageEndIndex = imageDataSection.IndexOf(IMAGE_TAG_FRONT_PART, nextImageStartIndex + IMAGE_TAG_FRONT_PART.Length);
				if (imageEndIndex == -1) imageEndIndex = imageDataSection.Length;
				var imageExtract = imageDataSection.Substring(nextImageStartIndex, imageEndIndex - nextImageStartIndex);
				var catalogIndexExtract = imageExtract.Substring(7, imageExtract.IndexOf(IMAGE_TAG_BACK_PART) - IMAGE_TAG_FRONT_PART.Length);
				string bytes_B64_Compressed_ = imageExtract.Substring(imageExtract.IndexOf(IMAGE_TAG_BACK_PART) + 1);
				imageDataSection = imageDataSection.Substring(imageEndIndex);
				imageArray.Add(int.Parse(catalogIndexExtract), bytes_B64_Compressed_);
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

		private static bool? LoadBoolFromJsonStringProperty(JSONClass parentJsonObject, string propertyName, bool? defaultValue)
		{
			if (!PropExists(parentJsonObject, propertyName)) return defaultValue;
			var keys = parentJsonObject.Keys.ToList();
			var jsonNode = parentJsonObject.Childs.ElementAt(keys.IndexOf(propertyName));
			return jsonNode.AsBool;
		}

		private static float? LoadFloatFromJsonFloatProperty(JSONClass parentJsonObject, string propertyName, float? defaultValue)
		{
			if (!PropExists(parentJsonObject, propertyName)) return defaultValue;
			var keys = parentJsonObject.Keys.ToList();
			var jsonNode = parentJsonObject.Childs.ElementAt(keys.IndexOf(propertyName));
			return jsonNode.AsFloat;
		}

		private static JSONClass LoadObjectFromJsonObjectProperty(JSONClass parentJsonObject, string propertyName, JSONClass defaultValue)
		{
			SuperController.LogMessage("propertyName: " + propertyName);
			SuperController.LogMessage("parentJsonObject: " + parentJsonObject.ToString());
			if (!PropExists(parentJsonObject, propertyName)) {
				SuperController.LogMessage($"prop {propertyName} doesnt exist in " + parentJsonObject.ToString());
				return defaultValue;
			}
			var keys = parentJsonObject.Keys.ToList();
			return parentJsonObject.Childs.ElementAt(keys.IndexOf(propertyName)).AsObject;
		}

		private static object LoadObjectFromJson<T>(JSONClass item, string returnTypeName)
		{
			switch (returnTypeName)
			{
				case "string": return item.Value;
				case nameof(VersionMessage): return DeserializeIntoVersionMessage(item);
				default: return item.Value;
			}
		}

		private static bool PropExists(JSONClass inputObject, string propName)
		{
			var keys = inputObject.Keys.ToList();
			return keys.IndexOf(propName) > -1;
		}

		private static string EncodeString(string input)
		{
			return Convert.ToBase64String(Encoding.ASCII.GetBytes(input));
		}

		private static string DecodeString(string serializedInput)
		{
			return Encoding.ASCII.GetString(Convert.FromBase64String(serializedInput));
		}

		private static byte[] B64DecodeBytes(string serializedInput)
		{
			return Convert.FromBase64String(serializedInput);
		}

		private static string B64EncodeBytes(byte[] input)
		{
			return Convert.ToBase64String(input);
		}

		private static HSVColor GetColorFromJsonClass(JSONClass colorObject)
		{
			var h = LoadFloatFromJsonFloatProperty(colorObject, "h", 0);
			var s = LoadFloatFromJsonFloatProperty(colorObject, "s", 0);
			var v = LoadFloatFromJsonFloatProperty(colorObject, "v", 0);
			var newColor = new HSVColor();
			newColor.H = h ?? 0;
			newColor.S = s ?? 0;
			newColor.V = v ?? 0;
			return newColor;
		}

		private static Vector3 GetVector3FromJsonClass(JSONClass vectorObject)
		{
			var x = LoadFloatFromJsonFloatProperty(vectorObject, "x", 0);
			var y = LoadFloatFromJsonFloatProperty(vectorObject, "y", 0);
			var z = LoadFloatFromJsonFloatProperty(vectorObject, "z", 0);
			var newVector3 = new Vector3();
			newVector3.x = x ?? 0;
			newVector3.y = y ?? 0;
			newVector3.z = z ?? 0;
			return newVector3;
		}
	}
}
