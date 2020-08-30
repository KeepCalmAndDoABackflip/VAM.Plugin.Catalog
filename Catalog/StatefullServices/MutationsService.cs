using juniperD.Models;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace juniperD.StatefullServices
{

	public class MutationsService
	{

		#region PluginInfo    
		public string pluginAuthor = "juniperD";
		public string pluginName = "FaceGen";
		public string pluginVersion = "1.0";
		public string pluginDate = "01/01/2020";
		public string pluginDescription = @"Apply random mutations to your character to get different looks";
		#endregion

		public bool CaptureFaceGenMorphs { get; set; } = true;
		public bool CaptureHair { get; set; } = false;
		public bool CaptureClothes { get; set; } = false;
		public bool CaptureDynamicItems { get; set; } = false;
		public bool CaptureActiveMorphs { get; set; } = false;

		// Cache
		//List<DAZMorph> _morphs;

		// Morphs that are not set to default, but different from the last checkpoint...
		List<MorphMutation> _morphBaseValues = new List<MorphMutation>();

		// Morphs that are currently different from the last checkpoint or from their default values...
		List<MorphMutation> _morphNewValues = new List<MorphMutation>();

		Dictionary<string, string> _morphCategoryMap = new Dictionary<string, string>();
		List<string> _morphCategories = new List<string>();
		PredefinedMorphSets _predefinedMorphsSets = new PredefinedMorphSets();

		// Settings...
		protected JSONStorableFloat _varianceRangeJSON;
		protected JSONStorableFloat _maxMorphCountJSON;

		// Morph sets state...
		protected JSONStorableBool _useFavoriteMorphs;
		protected JSONStorableBool _filterOutLeftRightMorphs;
		protected JSONStorableBool _usePredefinedMorphs;
		protected JSONStorableBool _useAllMorphs;
		protected JSONStorableBool _useCategoryMorphs;

		protected List<JSONStorableBool> _predefinedMorphsSetToggles = new List<JSONStorableBool>();
		protected List<JSONStorableBool> _categoryMorphsSetToggles = new List<JSONStorableBool>();

		protected Dictionary<string, Stack<Mutation>> _mutationStacks = new Dictionary<string, Stack<Mutation>>();
		//protected Dictionary<string, UIDynamicToggle> _approvedMorphToggles = new Dictionary<string, UIDynamicToggle>();
		protected List<int> _hairHistory = new List<int>();
		protected Stack<DAZClothingItem> _clothingStack = new Stack<DAZClothingItem>();

		protected CatalogPlugin _context;

		public void Init(CatalogPlugin context)
		{

			_context = context;
			if (!IsPersonAtom()) return;

			try
			{
				var morphs = GetMorphsForSelectedPersonOrDefault() ?? new List<DAZMorph>();
				_morphCategoryMap = new Dictionary<string, string>();
				foreach (var m in morphs)
				{
					if (_morphCategoryMap.ContainsKey(m.displayName)) continue;
					var categoryName = m.morphBank.GetMorphRegionName(m.morphName);
					_morphCategories.Add(categoryName);
					_morphCategoryMap.Add(m.displayName, categoryName);
				}
				_morphCategories = _morphCategories
					.Distinct()
					.OrderBy(c => c)
					.ToList();

				#region Mutation settings
				_context.CreateSpacer();
				_varianceRangeJSON = new JSONStorableFloat("Mutation Variance", 10f, 0f, 100f);
				_varianceRangeJSON.storeType = JSONStorableParam.StoreType.Full;
				_context.RegisterFloat(_varianceRangeJSON);
				var varianceSlider = _context.CreateSlider(_varianceRangeJSON);
				varianceSlider.valueFormat = "F0";

				_maxMorphCountJSON = new JSONStorableFloat("Maximum Morph Count", 10f, 0f, 100f);
				_maxMorphCountJSON.storeType = JSONStorableParam.StoreType.Full;
				_context.RegisterFloat(_maxMorphCountJSON);
				var morphCountSlider = _context.CreateSlider(_maxMorphCountJSON);
				morphCountSlider.valueFormat = "F0";

				#endregion

				#region Controls
				_context.CreateSpacer();

				_context.CreateButton("Re/try mutation").button.onClick.AddListener(() =>
				{
					RetryMutation();
				});

				_context.CreateButton("Undo mutation").button.onClick.AddListener(() =>
				{
					UndoPreviousMutation();
				});

				_context.CreateButton("Keep mutation").button.onClick.AddListener(() =>
				{
					var emptyDelta = CreateBufferMutation();
					ApplyMutation(ref emptyDelta);
				});

				_context.CreateButton("Retry Hair").button.onClick.AddListener(() =>
				{
					NextHair();
				});

				_context.CreateButton("Add Clothing item").button.onClick.AddListener(() =>
				{
					var removePrevious = false;
					var addNew = true;
					MutateClothing(removePrevious, addNew);
				});

				_context.CreateButton("Retry Clothing item").button.onClick.AddListener(() =>
				{
					var removePrevious = true;
					var addNew = true;
					MutateClothing(removePrevious, addNew);
				});

				_context.CreateButton("Undo Clothing").button.onClick.AddListener(() =>
				{
					var removePrevious = true;
					var addNew = false;
					MutateClothing(removePrevious, addNew);
				});

				_context.CreateButton("Add Whole Appearance").button.onClick.AddListener(() =>
				{
					NextLook();
				});

				#endregion

				#region All Morphs
				_context.CreateSpacer();
				_useAllMorphs = new JSONStorableBool("---- ALL MORPHS ----", false);
				_useAllMorphs.storeType = JSONStorableParam.StoreType.Full;
				_context.RegisterBool(_useAllMorphs);
				_context.CreateToggle(_useAllMorphs);
				_filterOutLeftRightMorphs = new JSONStorableBool("     Filter out left/right morphs", true);
				_context.RegisterBool(_filterOutLeftRightMorphs);
				_context.CreateToggle(_filterOutLeftRightMorphs);
				_useFavoriteMorphs = new JSONStorableBool("     Use Favorite Morphs Only", false);
				_context.RegisterBool(_useFavoriteMorphs);
				_context.CreateToggle(_useFavoriteMorphs);
				UpdateMorphToggleColors(_filterOutLeftRightMorphs.toggle, _useAllMorphs.val);
				UpdateMorphToggleColors(_useFavoriteMorphs.toggle, _useAllMorphs.val);
				#endregion All Morphs

				#region Predefined morphs
				_context.CreateSpacer();
				_usePredefinedMorphs = new JSONStorableBool("--- PREDEFINED MORPHS ---", true);
				_usePredefinedMorphs.storeType = JSONStorableParam.StoreType.Full;
				_context.RegisterBool(_usePredefinedMorphs);
				_context.CreateToggle(_usePredefinedMorphs);
				foreach (var set in _predefinedMorphsSets.Sets)
				{
					var morphSetToggle = new JSONStorableBool(set.Key, true);
					morphSetToggle.storeType = JSONStorableParam.StoreType.Full;
					_predefinedMorphsSetToggles.Add(morphSetToggle);
					_context.RegisterBool(morphSetToggle);
					var predefinednMorphSetToggleControl = _context.CreateToggle(morphSetToggle);
					UpdateMorphToggleColors(predefinednMorphSetToggleControl.toggle, morphSetToggle.val);
				}
				#endregion Predefined morphs

				#region Category morphs
				_context.CreateSpacer();
				_useCategoryMorphs = new JSONStorableBool("--- CATEGORY MORPHS ---", false);
				_useCategoryMorphs.storeType = JSONStorableParam.StoreType.Full;
				_context.RegisterBool(_useCategoryMorphs);
				_context.CreateToggle(_useCategoryMorphs);
				foreach (var category in _morphCategories)
				{
					var morphSetToggle = new JSONStorableBool(category, false);
					morphSetToggle.storeType = JSONStorableParam.StoreType.Full;
					_categoryMorphsSetToggles.Add(morphSetToggle);
					_context.RegisterBool(morphSetToggle);
					var morphSetToggleControl = _context.CreateToggle(morphSetToggle);
					UpdateMorphToggleColors(morphSetToggleControl.toggle, morphSetToggle.val);
				}
				#endregion Category morphs

				_useAllMorphs.toggle.onValueChanged.AddListener((checkedVal) =>
				{
					UpdateMorphToggleColors(_filterOutLeftRightMorphs.toggle, checkedVal);
					UpdateMorphToggleColors(_useFavoriteMorphs.toggle, checkedVal);
					if (!checkedVal) return;
					_usePredefinedMorphs.val = false;
					_useCategoryMorphs.val = false;
				});

				_usePredefinedMorphs.toggle.onValueChanged.AddListener((checkedVal) =>
				{
					_predefinedMorphsSetToggles.ForEach(t => UpdateMorphToggleColors(t.toggle, checkedVal));
					if (!checkedVal) return;
					_useCategoryMorphs.val = false;
					_useAllMorphs.val = false;
				});

				_useCategoryMorphs.toggle.onValueChanged.AddListener((checkedVal) =>
				{
					_categoryMorphsSetToggles.ForEach(t => UpdateMorphToggleColors(t.toggle, checkedVal));
					if (!checkedVal) return;
					_usePredefinedMorphs.val = false;
					_useAllMorphs.val = false;
				});

				//#region Custom Morphs
				//_context.CreateSpacer();
				//var morphSelection = new JSONStorableStringChooser("--- Select Morphs 1---", _morphs.Select(m => m.displayName).ToList(), null, "--- Select Morphs 2---");
				//morphSelection.storeType = JSONStorableParam.StoreType.Full;
				//_context.RegisterStringChooser(morphSelection);
				//var selectMorphs = _context.CreateScrollablePopup(morphSelection);
				//#endregion Custom Morphs

				//_includeHairInMutation = new JSONStorableBool("---Hair", true);
				//_context.RegisterBool(_includeHairInMutation);
				//_context.CreateToggle(_includeHairInMutation);
				//_usePredefinedMorphs.val = true;
			}

			catch (Exception e)
			{
				SuperController.LogError("Exception caught: " + e);
			}
		}

		private bool IsPersonAtom()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return false;
			return selectedAtom.type == "Person";
		}

		public Mutation CaptureCurrentMutation()
		{
			try
			{
				var newMutation = new Mutation();
				if (CaptureClothes) newMutation.ClothingItems = GetActiveClothes();
				if (CaptureHair) newMutation.HairItems = GetActiveHair();
				if (CaptureDynamicItems) newMutation.DynamicItems = GetActiveDynamicItems();
				if (CaptureFaceGenMorphs) newMutation.FaceGenMorphSet = GetCurrentMutationMorphs();
				if (CaptureActiveMorphs) newMutation.ActiveMorphs = GetActiveMorphs();
				return newMutation;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}

		public void CaptureAdditionalAtom(Atom atom, CatalogEntry catalogEntry)
		{
			try
			{
				var additionalAtom = CaptureAtom(atom);
				catalogEntry.Mutation.StoredAtoms.Add(additionalAtom);
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}

		public Mutation CaptureAtomVerbose(Atom atom)
		{
			try
			{
				var newMutation = new Mutation();
				newMutation.AtomName = atom.name;
				newMutation.AtomType = atom.type;
				newMutation.StoredAtoms = new List<StoredAtom>{CaptureAtom(atom) };
				return newMutation;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}

		//private Dictionary<string, Dictionary<string,string>> SerializeStorables(Atom atom)
		//{
		//	Dictionary<string, Dictionary<string, string>> storables = new Dictionary<string, Dictionary<string, string>>();
		//	var storableIds = atom.GetStorableIDs();
		//	foreach (var storableId in storableIds)
		//	{
		//		JSONStorable storable = _context.containingAtom.GetStorableByID(storableId);
		//		var paramAndActionNames = storable.GetAllParamAndActionNames();
		//		var storableParams =  new Dictionary<string,string>();
		//		foreach (var paramName in paramAndActionNames)
		//		{
		//			var paramType = storable.GetParamOrActionType(paramName);
		//			if (paramType == JSONStorable.Type.Bool)
		//			{
		//				var paramValue = storable.GetBoolParamValue(paramName);
		//				var serializedParamType = SerializeType(paramType);
		//				if (serializedParamType == null) continue;
		//				storableParams.Add(serializedParamType, paramValue.ToString());
		//			}
		//		}
		//		storables.Add(storableId, storableParams);
		//	}
		//	return storables;
		//}

		//string SerializeType(JSONStorable.Type type)
		//{
		//	switch (type)
		//	{
		//		case JSONStorable.Type.Action: return "Action";
		//		case JSONStorable.Type.AudioClipAction: return "AudioClipAction";
		//		case JSONStorable.Type.Bool: return "Bool";
		//		case JSONStorable.Type.Color: return "Color";
		//		case JSONStorable.Type.Float: return "Float";
		//		case JSONStorable.Type.None: return "None";
		//		case JSONStorable.Type.PresetFilePathAction: return "PresetFilePathAction";
		//		case JSONStorable.Type.SceneFilePathAction: return "SceneFilePathAction";
		//		case JSONStorable.Type.String: return "String";
		//		case JSONStorable.Type.StringChooser: return "StringChooser";
		//		case JSONStorable.Type.Url: return "Url";
		//		case JSONStorable.Type.Vector3: return "Vector3";
		//	}
		//	SuperController.LogError("Unrecognized type");
		//	return null;
		//}

		//public Mutation CaptureAtom(Atom atom)
		//{
		//	try
		//	{
		//		var newMutation = new Mutation();
		//		newMutation.AtomName = atom.name;
		//		newMutation.AtomType = atom.type;
		//		if (atom.type == "CustomUnityAsset")
		//		{
		//			CustomUnityAssetLoader customAsset = atom.GetStorableByID("asset") as CustomUnityAssetLoader;
		//			newMutation.AssetUrl = customAsset.GetUrlParamValue("assetUrl"); ;
		//			newMutation.AssetName = customAsset.GetStringChooserParamValue("assetName"); ;
		//		}
		//		return newMutation;
		//	}
		//	catch (Exception exc)
		//	{
		//		SuperController.LogError(exc.Message + ": " + exc.StackTrace);
		//		throw exc;
		//	}
		//}

		public StoredAtom CaptureAtom(Atom atom)
		{
			JSONClass atomsJSON = SuperController.singleton.GetSaveJSON(atom);
			JSONArray atomsArrayJSON = atomsJSON["atoms"].AsArray;
			JSONClass atomJSON = (JSONClass)atomsArrayJSON[0];
			JSONArray storablesArrayJSON = atomJSON["storables"].AsArray;

			StoredAtom catalogAtom = new StoredAtom();
			catalogAtom.Active = atom.on;
			catalogAtom.AtomType = atom.type;
			catalogAtom.AtomName = atom.name;
			
			var storables = new List<JSONClass>();
			for (int i = 0; i < storablesArrayJSON.Count; i++)
			{
				JSONClass storableJSON = (JSONClass)storablesArrayJSON[i];
				storables.Add(storableJSON);
			}
			//var storableIds = atom.GetStorableIDs();
			//foreach (var storableId in storableIds)
			//{
			//	var storable = atom.GetStorableByID(storableId);
			//	var storableJson = storable.GetJSON();
			//	storables.Add(storableJson);
			//}
			catalogAtom.Storables = storables;
			catalogAtom.FullAtom = atomJSON; //...this doesn't work. Only records the atom's id
			return catalogAtom;
		}

		//public List<JSONClass> CaptureStorables(Atom atom)
		//{
		//	var storableIds = atom.GetStorableIDs();
		//	var storables = new List<JSONClass>();
		//	foreach (var storableId in storableIds)
		//	{
		//		var storable = atom.GetStorableByID(storableId);
		//		var storableJson = storable.GetJSON();
		//		storables.Add(storableJson);
		//	}
		//	return storables;
		//}

		//public JSONStorable CloneStorable(JSONStorable inputStorable)
		//{
		//	var newStorable = new JSONStorable();
			
		//	var newBoolParamNames = inputStorable.GetBoolParamNames();
		//	foreach (var paramName in newBoolParamNames)
		//	{
		//		var newValue = inputStorable.GetBoolParamValue(paramName);
		//		var newBool = new JSONStorableBool(paramName, newValue);
		//		newStorable.RegisterBool(newBool);
		//	}

		//	var newColorParamNames = inputStorable.GetColorParamNames();
		//	foreach (var paramName in newColorParamNames)
		//	{
		//		var newValue = inputStorable.GetColorParamValue(paramName);
		//		var newColor = new JSONStorableColor(paramName, newValue);
		//		newStorable.RegisterColor(newColor);
		//	}

		//	var newFloatParamNames = inputStorable.GetFloatParamNames();
		//	foreach (var paramName in newFloatParamNames)
		//	{
		//		var newValue = inputStorable.GetFloatParamValue(paramName);
		//		newStorable.RegisterFloat(new JSONStorableFloat(paramName, newValue, newValue, newValue));
		//	}

		//	var newStringChooserParamNames = inputStorable.GetStringChooserParamNames();
		//	foreach (var paramName in newStringChooserParamNames)
		//	{
		//		var stringChooserValue = inputStorable.GetStringChooserParamValue(paramName);
		//		newStorable.RegisterStringChooser(new JSONStorableStringChooser(paramName, new List<string>() { stringChooserValue }, stringChooserValue, stringChooserValue));
		//	}

		//	var newStringParamNames = inputStorable.GetStringParamNames();
		//	foreach (var paramName in newStringParamNames)
		//	{
		//		newStorable.RegisterString(new JSONStorableString(paramName, inputStorable.GetStringParamValue(paramName)));
		//	}

		//	var newUrlParamNames = inputStorable.GetUrlParamNames();
		//	foreach (var paramName in newUrlParamNames)
		//	{
		//		newStorable.RegisterUrl(new JSONStorableUrl(paramName, inputStorable.GetUrlParamValue(paramName)));
		//	}

		//	var newVector3ParamNames = inputStorable.GetVector3ParamNames();
		//	foreach (var paramName in newVector3ParamNames)
		//	{
		//		var newVectorValue = inputStorable.GetVector3ParamValue(paramName);
		//		newStorable.RegisterVector3(new JSONStorableVector3(paramName, newVectorValue, newVectorValue, newVectorValue));
		//	}
		//	return newStorable;
		//}

		private void UpdateMorphToggleColors(Toggle toggle, bool enabled)
		{
			var cols = toggle.colors;
			cols.normalColor = enabled ? Color.green : Color.black;
			toggle.colors = cols;
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void FixedUpdate()
		{
		}

		public List<MorphMutation> GetCurrentMutationMorphs()
		{
			var mutationStack = GetMutationStackForSelectedPersonOrDefault();
			if (mutationStack == null) return new List<MorphMutation>();;
			var finalMorphSet = new List<MorphMutation>();
			foreach (var mutation in mutationStack)
			{
				foreach (var morph in mutation.FaceGenMorphSet)
				{
					if (finalMorphSet.Any(m => m.Id == morph.Id)) continue;
					finalMorphSet.Add(new MorphMutation()
					{
						Id = morph.Id,
						Value = morph.Value
					});
				}
			}
			return finalMorphSet;
		}

		private Stack<Mutation> GetMutationStackForSelectedPersonOrDefault()
		{
			var personName = GetSelectedPersonAtomOrDefault()?.name;
			var catalogName = _context._catalogName.val;
			if (personName == null) return null;
			var mutationKey = personName + ":" + catalogName;
			if (!_mutationStacks.ContainsKey(mutationKey))
			{
				_mutationStacks.Add(mutationKey, new Stack<Mutation>());
			}
			return _mutationStacks[mutationKey];
		}

		public void SetMorphBaseValuesCheckpoint()
		{
			var activeMorphs = GetActiveMorphs();
			for (int mIndex = 0; mIndex < activeMorphs.Count; mIndex++)
			{
				var newMorph = activeMorphs[mIndex];
				var morphBase = _morphBaseValues.FirstOrDefault(m => m.Id == newMorph.Id);
				if (morphBase == null)
				{
					_morphBaseValues.Add(newMorph);
				}
				else
				{
					morphBase.Value = newMorph.Value;
				}
			}
		}

		public List<MorphMutation> GetCurrentMorphBaseValues() {
			return _morphBaseValues;
		}

		private float GetBaseValueForMorph(string displayName, DAZMorph morph = null)
		{
			try { 
				if (morph==null) morph = GetMorphByNameOrDefault(displayName);
				var baseMorph = _morphBaseValues.FirstOrDefault(b => b.Id == displayName);
				if (baseMorph == null) return morph.startValue;
				return baseMorph.Value;
			}
			catch(Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public DAZMorph GetMorphByNameOrDefault(string displayName)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return null;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
			return morphControl
				.GetMorphs()
				.FirstOrDefault(m => m.displayName == displayName);			
		}

		public List<MorphMutation> GetActiveMorphs()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return new List<MorphMutation>();
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;

			var allMorphs = morphControl.GetMorphs();
			var morphMutations = new List<MorphMutation>();
			var updateCount = 0;
			var alreadyChecked = new List<string>();
			for (int m = 0; m < allMorphs.Count; m++)
			{
				var morph = allMorphs[m];
				if (alreadyChecked.Contains(morph.displayName)) continue;
				alreadyChecked.Add(morph.displayName);
				var baseMorph = _morphBaseValues.FirstOrDefault(mo => mo.Id == morph.displayName);
				var baseValue = baseMorph?.Value ?? morph.startValue;
				var currentValue = morph.morphValue;
				if (baseValue != currentValue)
				{
					updateCount++;
					var newMorphMutation = new MorphMutation()
						{
							Id = morph.displayName,
							Value = morph.morphValue,
							PreviousValue = GetBaseValueForMorph(morph.displayName, morph),
							MorphItem = morph,
							Active = true,
						};
						morphMutations.Add(newMorphMutation);
				}
			}
			return morphMutations;
		}

		//public IEnumerable<HairMutation> GetActiveSkin()
		//{
		//	JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
		//	DAZCharacterSelector character = geometry as DAZCharacterSelector;
		//	var itemKeys = items.Where(h => h.active).Select(h => h.displayName).ToList();
		//	return itemKeys.Select(itemKey => new HairMutation()
		//	{
		//		DAZHairGroupName = itemKey
		//	});
		//}

		public List<HairMutation> GetActiveHair()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return new List<HairMutation>();
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			var items = character.hairItems;
			return items.Where(h => h.active).Select(item => new HairMutation()
			{
				Id = item.displayName,
				DAZHairGroup = item
			}).ToList();
		}

		public List<DynamicMutation> GetActiveDynamicItems()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return new List<DynamicMutation>();
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			//var items = character.dynami;
			//return items.Where(h => h.active).Select(item => new DynamicMutation()
			//{
			//	DAZDynamicItemName = item.name,
			//	DAZDynamicItem = item
			//}).ToList();
			return new List<DynamicMutation>();
		}

		public List<ClothingMutation> GetActiveClothes()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return new List<ClothingMutation>();
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			var items = character.clothingItems;
			var itemKeys = items.Where(h => h.active).Select(h => new ClothingMutation()
			{
				Id = h.displayName,
				DAZClothingItem = h
			}).ToList();
			return itemKeys;
		}


		public void RemoveAllHair()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			character.RemoveAllHair();
		}

		public void RemoveAllClothing()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			character.RemoveAllClothing();
		}

		private void NextHair()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			// Select a random hair style
			var hairStyles = character.hairItems;
			System.Random randomizer = new System.Random();
			var randomHairStyleIndex = randomizer.Next(0, hairStyles.Count());
			character.RemoveAllHair();
			character.SetActiveHairItem(hairStyles[randomHairStyleIndex], true);
			_hairHistory.Add(randomHairStyleIndex);
		}

		//private void NextSkin()
		//{
		//	JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
		//	DAZCharacterSelector character = geometry as DAZCharacterSelector;
		//	// Select a random hair style
		//	System.Random randomHairStyle = new System.Random();
		//	var randomHairStyleIndex = randomHairStyle.Next(0, hairStyles.Count());
		//	character.RemoveAllHair();
		//	character.SetActiveHairItem(hairStyles[randomHairStyleIndex], true);
		//	HairHistory.Add(randomHairStyleIndex);
		//}

		private void MutateClothing(bool removePrevious = false, bool addNewItem = true)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			// Remove previous item...
			if (_clothingStack.Count > 0 && removePrevious) _clothingStack.Pop();
			// Add new item...
			if (addNewItem) AddNewItemOfClothing(character);
			// Refresh character clothing...
			RefreshCharacterClothing(character);
		}

		private void AddNewItemOfClothing(DAZCharacterSelector character)
		{
			System.Random randomClothingItemGenerator = new System.Random();
			var clothingItems = character.clothingItems;
			var randomClothingIndex = randomClothingItemGenerator.Next(0, clothingItems.Count());
			var randomClothingItem = clothingItems[randomClothingIndex];
			_clothingStack.Push(randomClothingItem);
		}

		private void RefreshCharacterClothing(DAZCharacterSelector character)
		{
			// Refresh character clothing...
			character.RemoveAllClothing();
			foreach (var item in _clothingStack)
			{
				character.SetActiveClothingItem(item, true);
			}
		}

		public Mutation CreateBufferMutation()
		{
			return new Mutation();
		}

		public Mutation CreateMorphMutation()
		{
			try
			{
				// Get random or favorite morphs...
				IEnumerable<DAZMorph> morphs = GetAppropriateMorphs();

				var newMutation = new Mutation();

				// Determine list of sub-morphs to adjust from morph set...
				System.Random randomMorphIndex = new System.Random();
				int availableMorphCount = morphs.Count();
				if (availableMorphCount == 0) throw new Exception("No morphs found!");
				int morphsCountUserSetting = (int)Math.Ceiling(_maxMorphCountJSON.val);
				var numberOfMorphsToAdjust = Math.Min(morphsCountUserSetting, availableMorphCount);
				var morphIndexesToAdjust = new List<int>();
				// Adjust each morph by random value...
				System.Random randomMorphValueIncrementMagnitude = new System.Random();

				var morphUpdateSet = new List<MorphMutation>();

				for (var i = 0; i < numberOfMorphsToAdjust; i++)
				{
					int newMorphIndex = 0;
					int overflow = 0;
					DAZMorph morph = null; ;
					do
					{
						if (++overflow > 100)
						{
							morph = null;
							break;
						}
						newMorphIndex = randomMorphIndex.Next(0, availableMorphCount - 1);
						morph = morphs.ElementAt(newMorphIndex);
					} while (morphIndexesToAdjust.Contains(newMorphIndex) || morphUpdateSet.Any(m => m.Id == morph.displayName));
					morphIndexesToAdjust.Add(newMorphIndex);
					if (morph == null) continue;

					var currentVal = morph.morphValue;
					var maxVal = morph.max;
					var minVal = morph.min;
					var range = maxVal - minVal;
					var appliedRange = range * (_varianceRangeJSON.val / 100);
					var actualMax = currentVal + appliedRange;
					if (actualMax > maxVal) actualMax = maxVal;
					var actualMin = minVal - appliedRange;
					if (actualMin < minVal) actualMin = minVal;
					float newValue = (float)((randomMorphValueIncrementMagnitude.NextDouble() * actualMax) - actualMin);
					string valueDiff = currentVal + ":" + newValue;
					var newMorph = new MorphMutation
					{
						Id = morph.displayName,
						Value = newValue,
						PreviousValue = currentVal,
						Active = true
					};
					morphUpdateSet.Add(newMorph);
				}

				newMutation.FaceGenMorphSet = morphUpdateSet;

				return newMutation;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
			throw new Exception("Unknown execution path");
		}

		public void ApplyMutation(ref Mutation mutation)
		{
			try
			{
				var mutationStack = GetMutationStackForSelectedPersonOrDefault();
				if (mutationStack == null) return;

				mutation.IsActive = true;
				//--------------------------------------------
				var newMorphSet = new List<MorphMutation>();
				for (var i = 0; i < mutation.FaceGenMorphSet.Count(); i++)
				{
					var morphMutation = mutation.FaceGenMorphSet.ElementAt(i);
					newMorphSet.Add(morphMutation);
					AddFaceGenMorphToggle(ref morphMutation);
					if (!morphMutation.Active) continue;
					ApplyMutationMorphItem(morphMutation);
				}
				mutation.FaceGenMorphSet = newMorphSet;
				//--------------------------------------------
				var newClothingItems = new List<ClothingMutation>();
				for (var i = 0; i < mutation.ClothingItems.Count(); i++)
				{
					var clothingItem = mutation.ClothingItems.ElementAt(i);
					newClothingItems.Add(clothingItem);
					AddClothingToggle(ref clothingItem);
					if (!clothingItem.Active) continue;
					ApplyClothingItem(clothingItem);
				}
				mutation.ClothingItems = newClothingItems;
				//--------------------------------------------
				var newHairItems = new List<HairMutation>();
				for (var i = 0; i < mutation.HairItems.Count(); i++)
				{
					var hairItem = mutation.HairItems.ElementAt(i);
					newHairItems.Add(hairItem);
					AddHairToggle(ref hairItem);
					if (!hairItem.Active) continue;
					ApplyHairItem(hairItem);
				}
				mutation.HairItems = newHairItems;
				////--------------------------------------------
				//var newDynamicItems = new List<DynamicMutation>();
				//for (var i = 0; i < mutation.DynamicItems.Count(); i++)
				//{
				//	var dynamicItem = mutation.DynamicItems.ElementAt(i);
				//	newDynamicItems.Add(dynamicItem);
				//	AddDynamicItemToggle(ref dynamicItem);
				//	if (!dynamicItem.Active) continue;
				//	ApplyDynamicItem(dynamicItem);
				//}
				//mutation.DynamicItems = newDynamicItems;
				//--------------------------------------------
				var newActiveMorphItems = new List<MorphMutation>();
				for (var i = 0; i < mutation.ActiveMorphs.Count(); i++)
				{
					var item = mutation.ActiveMorphs.ElementAt(i);
					newActiveMorphItems.Add(item);
					AddActiveMorphToggle(ref item);
					if (!item.Active) continue;
					ApplyActiveMorphItem(item);
				}
				mutation.ActiveMorphs = newActiveMorphItems;
				//--------------------------------------------
				mutationStack.Push(mutation);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void ApplyMutationMorphItem(MorphMutation morphMutation)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			var morph = morphs.FirstOrDefault(m => m.displayName == morphMutation.Id);
			if (morph == null) {
				_context.ShowPopupMessage("Morph cannot be found for this atom");
				return;
			}
			morph.morphValue = morphMutation.Value;
		}

		private List<DAZMorph> GetMorphsForAllPersonAtoms()
		{
			var atoms = SuperController.singleton.GetAtoms().Where(a => a.type =="Person").ToList();
			List<DAZMorph> allMorphs = new List<DAZMorph>(); 
			foreach (var atom in atoms) 
			{ 
				JSONStorable geometry = atom.GetStorableByID("geometry");
				DAZCharacterSelector character = geometry as DAZCharacterSelector;
				GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
				var morphs = morphControl.GetMorphs();
				foreach (var morph in morphs)
				{
					if (!allMorphs.Any(m => m.displayName == morph.displayName))
					{
						allMorphs.Add(morph);
					}
				}
			}
			return allMorphs;
		}

		private List<DAZMorph> GetMorphsForSelectedPersonOrDefault()
		{
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (_context.containingAtom.type == "Person") selectedAtom = _context.containingAtom;
			if (selectedAtom == null || selectedAtom.type != "Person")
			{
				_context.ShowPopupMessage("Please select a Person");
				return null;
			}
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
			return morphControl.GetMorphs();
		}

		public void ApplyClothingItem(ClothingMutation clothingItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZClothingItem dazClothingItem = character.clothingItems.FirstOrDefault(h => h.displayName == clothingItem.Id);
			
			if (dazClothingItem == null) {
				_context.ShowPopupMessage("Item cannot be used on this Persons type");
				return;
			}
			character.SetActiveClothingItem(dazClothingItem, true);
		}

		private Atom GetSelectedPersonAtomOrDefault()
		{
			if (_context.containingAtom.type == "Person") return _context.containingAtom;
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (selectedAtom == null)
			{
				SuperController.LogMessage("Please select a Person in the scene");
				return null;
			}
			if (selectedAtom.type != "Person")
			{
				SuperController.LogMessage("Please select a Person atom");
				return null;
			}
			return selectedAtom;
		}

		public void ApplyHairItem(HairMutation hairItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZHairGroup item = character.hairItems.FirstOrDefault(h => h.displayName == hairItem.Id);
			if (item == null) {
				_context.ShowPopupMessage("Item cannot be used on this Persons type");
				return;
			}
			character.SetActiveHairItem(item, true);
		}

		//public void ApplyDynamicItem(DynamicMutation mutationItem)
		//{
		//	Atom selectedAtom = GetSelectedPersonAtomOrDefault();
		//	if (selectedAtom == null) return;
		//	JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
		//	DAZCharacterSelector character = geometry as DAZCharacterSelector;
			
		//	//DAZDynamicItem item = geometry.
		//	//character.SetActiveDynamicItem(item, true);
		//}

		public void ApplyActiveMorphItem(MorphMutation mutationItem)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			DAZMorph item = morphs.FirstOrDefault(h => h.displayName == mutationItem.Id);
			if (item == null) {
				_context.ShowPopupMessage("Morph cannot be used on this Person");
				return;
			}
			//item.active = true;
			item.SetValue(mutationItem.Value);
		}

		public void UndoPreviousMutation()
		{
			try
			{
				var mutationStack = GetMutationStackForSelectedPersonOrDefault();
				if (mutationStack == null || mutationStack.Count == 0) return;
				var lastMutation = mutationStack.Pop();
				UndoMutation(lastMutation);
			}

			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
			}
		}

		private void UndoMutation(Mutation mutation)
		{
			try
			{
				mutation.IsActive = false;
				foreach (var item in mutation.FaceGenMorphSet)
				{
					_context.RemoveToggle(item.UiToggle);
					//_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					//if (!mutationMorph.Active) continue;
					UndoMutationMorph(item);
				}

				foreach (var item in mutation.ClothingItems)
				{
					_context.RemoveToggle(item.UiToggle);
					//_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					if (!item.Active) continue;
					RemoveClothingItem(item);
				}

				foreach (var item in mutation.HairItems)
				{
					_context.RemoveToggle(item.UiToggle);
					//_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					if (!item.Active) continue;
					RemoveHairItem(item);
				}

				foreach (var item in mutation.ActiveMorphs)
				{
					_context.RemoveToggle(item.UiToggle);
					//_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					if (!item.Active) continue;
					RemoveActiveMorphItem(item);
				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void UndoMutationMorph(MorphMutation mutationMorph)
		{
			//if (!mutationMorph.Active) return;
			var morphName = mutationMorph.Id;
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			var morph = morphs.FirstOrDefault(m => m.displayName == morphName);
			if (morph == null) {
				//_context.ShowPopupMessage("Morph cannot be found for this Persons type"); 
				return;
			}
			if (morph != null) {
				//SuperController.("UnChanging morph item " + morphName + " to " + mutationMorph.PreviousValue);
				morph.morphValue = mutationMorph.PreviousValue;
			}
		}

		public void RemoveHairItem(HairMutation removeHairItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			foreach (var item in character.hairItems)
			{
				if (item.displayName == removeHairItem.Id)
				{
					character.SetActiveHairItem(item, false);
					return;
				}
			}
		}

		//public void RemoveDynamicItem(DynamicMutation removeHairItem)
		//{
		//	Atom selectedAtom = GetSelectedPersonAtom();
		//	if (selectedAtom == null) return;
		//	JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
		//	DAZCharacterSelector character = geometry as DAZCharacterSelector;
		//	//foreach (var item in character.dynam)
		//	//{
		//	//	if (item.displayName == removeHairItem.DAZDynamicItemName)
		//	//	{
		//	//		character.SetActiveDynamicItem(item, false);
		//	//		return;
		//	//	}
		//	//}
		//}

		public void RemoveActiveMorphItem(MorphMutation removeItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
			var morph = morphControl.GetMorphs().FirstOrDefault(m => removeItem.Id == m.displayName);
			if (morph == null) throw new Exception($"Could not find morph by the name of '{removeItem}'");
			var baseValue = GetBaseValueForMorph(removeItem.Id);
			morph.SetValue(baseValue);
			//foreach (var item in _morphNewValues)
			//{
			//	if (item.Name == removeItem.Name)
			//	{
			//		//item.active = false;
			//		item.MorphItem.SetValue(GetBaseValueForMorph(item.Name));
			//		return;
			//	}
			//}
		}


		public void RemoveClothingItem(ClothingMutation removeClothingItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			foreach (var item in character.clothingItems)
			{
				if (item.displayName == removeClothingItem.Id)
				{
					character.SetActiveClothingItem(item, false);
					return;
				}
			}
		}

		public void NextLook()
		{
			var mutation = CreateMorphMutation();
			ApplyMutation(ref mutation);
			NextHair();
			MutateClothing();
		}

		public void RetryMutation()
		{
			var mutationStack = GetMutationStackForSelectedPersonOrDefault();
			if (mutationStack == null) return;
			if (mutationStack.Count > 0) UndoPreviousMutation();
			var mutation = CreateMorphMutation();
			ApplyMutation(ref mutation);
			return;
		}

		//private void RemoveUnusedMorphs()
		//{
		//	CurrentMutationToggles = CurrentMutationToggles.Where(e => e.val == e.max).ToList();
		//}

		private IEnumerable<DAZMorph> GetAppropriateMorphs()
		{
			var appropriateMorphs = new List<DAZMorph>();
			if (_useAllMorphs.val)
			{
				var morphs = GetMorphsForSelectedPersonOrDefault();
				if (morphs == null) return new List<DAZMorph>();
				appropriateMorphs = morphs.ToList();
				appropriateMorphs = DistinctByDisplayName(appropriateMorphs);
				appropriateMorphs = SelectFavoriteMorphs(appropriateMorphs);
				appropriateMorphs = ExcludeLeftAndRightMorphs(appropriateMorphs);
			}
			if (_usePredefinedMorphs.val)
			{
				appropriateMorphs = SelectMorphsBySet();
			}
			if (_useCategoryMorphs.val)
			{
				appropriateMorphs = SelectMorphsByCategory();
			}
			return appropriateMorphs;
		}

		private List<DAZMorph> DistinctByDisplayName(IEnumerable<DAZMorph> morphs)
		{
			var distictList = new List<DAZMorph>();
			foreach (var morph in morphs)
			{
				if (distictList.Any(m => m.displayName == morph.displayName)) continue;
				distictList.Add(morph);
			}
			return distictList;
		}

		private List<DAZMorph> ExcludeLeftAndRightMorphs(List<DAZMorph> morphs)
		{
			if (!_filterOutLeftRightMorphs.val) return morphs;
			return morphs
				.Where(m => !m.displayName.ToLower().Contains("left") && !m.displayName.ToLower().Contains("right"))
				.Where(m => !m.displayName.ToLower().StartsWith("l ") && !m.displayName.ToLower().StartsWith("r "))
				.Where(m => !m.displayName.ToLower().Contains(" l ") && !m.displayName.ToLower().Contains("r  "))
				.ToList();
		}

		private List<DAZMorph> SelectMorphsBySet()
		{
			var filteredByMorphSet = new List<DAZMorph>();
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return new List<DAZMorph>();
			foreach (JSONStorableBool morphSetToggle in _predefinedMorphsSetToggles)
			{
				if (!morphSetToggle.val) continue;
				var morphSet = _predefinedMorphsSets.Sets[morphSetToggle.name];
				foreach (var morphName in morphSet)
				{
					var dazMorph = morphs.FirstOrDefault(m => m.displayName == morphName);
					if (dazMorph != null && !filteredByMorphSet.Any(m => m.displayName == morphName))
					{
						filteredByMorphSet.Add(dazMorph);
					}
				}
			}
			return filteredByMorphSet;
		}

		private List<DAZMorph> SelectMorphsByCategory()
		{
			var selectedMorphCategories = _categoryMorphsSetToggles.Where(t => t.val == true).Select(t => t.name);
			var morphSet = new List<DAZMorph>();
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return new List<DAZMorph>();
			foreach (var catName in selectedMorphCategories)
			{
				var morphUids = _morphCategoryMap.Where(cat => cat.Value == catName).Select(c => c.Key);
				var selectedMorphs = morphs.Where(m => morphUids.Contains(m.displayName));
				morphSet.AddRange(selectedMorphs);
			}
			return morphSet;
		}

		private List<DAZMorph> SelectFavoriteMorphs(List<DAZMorph> morphs)
		{
			return morphs.Where(m => m.favorite).ToList();
		}

		private void AddFaceGenMorphToggle(ref MorphMutation mutationComponent)
		{
			try
			{
				var itemName = mutationComponent.Id;
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				//_context.RegisterBool(toggleData);
				var mutation = mutationComponent;
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyMutationMorphItem(mutation);
					else UndoMutationMorph(mutation);
				};
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
				
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}

		private void AddHairToggle(ref HairMutation mutationComponent)
		{
			try
			{
				var itemName = mutationComponent.Id;
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				var mutation = mutationComponent;

				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyHairItem(mutation);
					else RemoveHairItem(mutation);
				};
				//UnityAction<string> stopTracking = (name) =>
				//{
				//	parentMutation.HairItems = parentMutation.HairItems.Where(m => m.Id != name).ToList();
				//};
				//var infoToggle = _context.AddEntrySubItemToggle(itemName, mutation.Active, toggleAction, stopTracking);
				//mutationComponent.DynamicCheckbox = infoToggle;
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.ToString());
				throw exc;
			}
		}

		//private void AddDynamicItemToggle(ref DynamicMutation mutationComponent)
		//{
		//	try
		//	{
		//		var itemName = mutationComponent.DAZDynamicItemName;
		//		var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
		//		var newToggle = _context.CreateToggle(toggleData, true);
		//		//_context.RegisterBool(toggleData);
		//		var mutation = mutationComponent;
		//		UnityAction<bool> toggleAction =(isChecked) =>
		//		{
		//			mutation.Active = isChecked;
		//			if (isChecked) ApplyDynamicItem(mutation);
		//			else RemoveDynamicItem(mutation);
		//		};
		//		UnityAction<string> stopTracking = (name) =>
		//		{
		//			parentMutation.HairItems = parentMutation.HairItems.Where(m => m.DAZHairGroupName != name).ToList();
		//		};
		//		var infoToggle = _context.AddInfoCheckbox(itemName, mutation.Active, toggleAction);
		//		newToggle.toggle.onValueChanged.AddListener(toggleAction);
		//		mutationComponent.UiToggle = newToggle;
		//		mutationComponent.UiToggleInfoBox = infoToggle;
		//	}
		//	catch (Exception exc)
		//	{
		//		SuperController.LogError(exc.ToString());
		//		throw exc;
		//	}
		//}

		private void AddActiveMorphToggle(ref MorphMutation mutationComponent)
		{
			try
			{
				var itemName = mutationComponent.Id;
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				var mutation = mutationComponent;
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyActiveMorphItem(mutation);
					else RemoveActiveMorphItem(mutation);
				};
				//UnityAction<string> stopTracking = (name) =>
				//{
				//	parentMutation.ActiveMorphs = parentMutation.ActiveMorphs.Where(m => m.Id != name).ToList();
				//};
				//var infoToggle = _context.AddEntrySubItemToggle(itemName, mutation.Active, toggleAction, stopTracking);
				//mutationComponent.DynamicCheckbox = infoToggle;
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.ToString());
				throw exc;
			}
		}

		private void AddClothingToggle(ref ClothingMutation mutationComponent)
		{
			try
			{
				var itemName = mutationComponent.Id;
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				//_context.RegisterBool(toggleData);
				var mutation = mutationComponent;
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyClothingItem(mutation);
					else RemoveClothingItem(mutation);
				};
				//UnityAction<string> stopTracking = (name) =>
				//{
				//	parentMutation.ClothingItems = parentMutation.ClothingItems.Where(m => m.Id != name).ToList();
				//};
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
				//var infoToggle = _context.AddEntrySubItemToggle(itemName, mutation.Active, toggleAction, stopTracking);
				//mutationComponent.DynamicCheckbox = infoToggle;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}
	}
}
