using juniperD.Models;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
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
		List<DAZMorph> _morphs;

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

		protected Stack<Mutation> _mutationStack = new Stack<Mutation>();
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
				// Cache morphs...
				JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
				DAZCharacterSelector character = geometry as DAZCharacterSelector;
				GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;

				_morphs = morphControl.GetMorphs();
				_morphCategoryMap = new Dictionary<string, string>();
				foreach (var m in _morphs)
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
			return _context.containingAtom.type == "Person";
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
			StoredAtom catalogAtom = new StoredAtom();
			catalogAtom.Active = atom.on;
			catalogAtom.AtomType = atom.type;
			catalogAtom.AtomName = atom.name;
			var storableIds = atom.GetStorableIDs();
			var storables = new List<JSONClass>();
			foreach (var storableId in storableIds)
			{
				var storable = atom.GetStorableByID(storableId);
				var storableJson = storable.GetJSON();
				storables.Add(storableJson);
			}
			catalogAtom.Storables = storables;
			return catalogAtom;
		}

		public List<JSONClass> CaptureStorables(Atom atom)
		{
			var storableIds = atom.GetStorableIDs();
			var storables = new List<JSONClass>();
			foreach (var storableId in storableIds)
			{
				var storable = atom.GetStorableByID(storableId);
				var storableJson = storable.GetJSON();
				storables.Add(storableJson);
			}
			return storables;
		}

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
			var finalMorphSet = new List<MorphMutation>();
			foreach (var mutation in _mutationStack)
			{
				foreach (var morph in mutation.FaceGenMorphSet)
				{
					if (finalMorphSet.Any(m => m.Name == morph.Name)) continue;
					finalMorphSet.Add(new MorphMutation()
					{
						Name = morph.Name,
						Value = morph.Value
					});
				}
			}
			return finalMorphSet;
		}

		public void SetMorphBaseValuesCheckpoint()
		{
			var activeMorphs = GetActiveMorphs();
			for (int mIndex = 0; mIndex < activeMorphs.Count; mIndex++)
			{
				var newMorph = activeMorphs[mIndex];
				var morphBase = _morphBaseValues.FirstOrDefault(m => m.Name == newMorph.Name);
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
				if (morph==null) morph = GetMorphByName(displayName);
				var baseMorph = _morphBaseValues.FirstOrDefault(b => b.Name == displayName);
				if (baseMorph == null) return morph.startValue;
				return baseMorph.Value;
			}
			catch(Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public DAZMorph GetMorphByName(string displayName)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
			return morphControl
				.GetMorphs()
				.FirstOrDefault(m => m.displayName == displayName);			
		}

		public List<MorphMutation> GetActiveMorphs()
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
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
				var baseMorph = _morphBaseValues.FirstOrDefault(mo => mo.Name == morph.displayName);
				var baseValue = baseMorph?.Value ?? morph.startValue;
				var currentValue = morph.morphValue;
				if (baseValue != currentValue)
				{
					updateCount++;
					var newMorphMutation = new MorphMutation()
						{
							Name = morph.displayName,
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
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			var items = character.hairItems;
			return items.Where(h => h.active).Select(item => new HairMutation()
			{
				DAZHairGroupName = item.displayName,
				DAZHairGroup = item
			}).ToList();
		}

		public List<DynamicMutation> GetActiveDynamicItems()
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
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
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			var items = character.clothingItems;
			var itemKeys = items.Where(h => h.active).Select(h => new ClothingMutation()
			{
				DAZClothingItemName = h.displayName,
				DAZClothingItem = h
			}).ToList();
			return itemKeys;
		}


		private void NextHair()
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
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
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
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
					} while (morphIndexesToAdjust.Contains(newMorphIndex) || morphUpdateSet.Any(m => m.Name == morph.displayName));
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
						Name = morph.displayName,
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
				mutation.IsActive = true;
				//--------------------------------------------
				var newMorphSet = new List<MorphMutation>();
				for (var i = 0; i < mutation.FaceGenMorphSet.Count(); i++)
				{
					var morphMutation = mutation.FaceGenMorphSet.ElementAt(i);
					newMorphSet.Add(morphMutation);
					AddMorphToggle(ref morphMutation, mutation);
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
					AddClothingToggle(ref clothingItem, mutation);
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
					AddHairToggle(ref hairItem, mutation);
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
					AddActiveMorphToggle(ref item, mutation);
					if (!item.Active) continue;
					ApplyActiveMorphItem(item);
				}
				mutation.ActiveMorphs = newActiveMorphItems;
				//--------------------------------------------

				_mutationStack.Push(mutation);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ApplyMutationMorphItem(MorphMutation morphMutation)
		{
			var morph = _morphs.First(m => m.displayName == morphMutation.Name);
			morph.morphValue = morphMutation.Value;
		}

		private void ApplyClothingItem(ClothingMutation clothingItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZClothingItem dazClothingItem = character.clothingItems.First(h => h.displayName == clothingItem.DAZClothingItemName);
			character.SetActiveClothingItem(dazClothingItem, true);
		}

		private void ApplyHairItem(HairMutation hairItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZHairGroup item = character.hairItems.First(h => h.displayName == hairItem.DAZHairGroupName);
			character.SetActiveHairItem(item, true);
		}

		private void ApplyDynamicItem(DynamicMutation mutationItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			
			//DAZDynamicItem item = geometry.
			//character.SetActiveDynamicItem(item, true);
		}

		private void ApplyActiveMorphItem(MorphMutation mutationItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZMorph item = _morphs.First(h => h.displayName == mutationItem.Name);
			//item.active = true;
			item.SetValue(mutationItem.Value);
		}

		public void UndoPreviousMutation()
		{
			try
			{
				if (_mutationStack.Count == 0) return;

				var lastMutation = _mutationStack.Pop();
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
					_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					//if (!mutationMorph.Active) continue;
					UndoMutationMorph(item);
				}

				foreach (var item in mutation.ClothingItems)
				{
					_context.RemoveToggle(item.UiToggle);
					_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					if (!item.Active) continue;
					RemoveClothingItem(item);
				}

				foreach (var item in mutation.HairItems)
				{
					_context.RemoveToggle(item.UiToggle);
					_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					if (!item.Active) continue;
					RemoveHairItem(item);
				}

				foreach (var item in mutation.ActiveMorphs)
				{
					_context.RemoveToggle(item.UiToggle);
					_context.RemoveUiCatalogSubItem(item.DynamicCheckbox);
					if (!item.Active) continue;
					RemoveActiveMorphItem(item);
				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void UndoMutationMorph(MorphMutation mutationMorph)
		{
			//if (!mutationMorph.Active) return;
			var morphName = mutationMorph.Name;
			var morph = _morphs.First(m => m.displayName == morphName);
			if (morph != null) {
				//SuperController.("UnChanging morph item " + morphName + " to " + mutationMorph.PreviousValue);
				morph.morphValue = mutationMorph.PreviousValue;
			}
		}

		private void RemoveHairItem(HairMutation removeHairItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			foreach (var item in character.hairItems)
			{
				if (item.displayName == removeHairItem.DAZHairGroupName)
				{
					character.SetActiveHairItem(item, false);
					return;
				}
			}
		}

		private void RemoveDynamicItem(DynamicMutation removeHairItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			//foreach (var item in character.dynam)
			//{
			//	if (item.displayName == removeHairItem.DAZDynamicItemName)
			//	{
			//		character.SetActiveDynamicItem(item, false);
			//		return;
			//	}
			//}
		}

		private void RemoveActiveMorphItem(MorphMutation removeItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
			var morph = morphControl.GetMorphs().FirstOrDefault(m => removeItem.Name == m.displayName);
			if (morph == null) throw new Exception($"Could not find morph by the name of '{removeItem}'");
			var baseValue = GetBaseValueForMorph(removeItem.Name);
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


		private void RemoveClothingItem(ClothingMutation removeClothingItem)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			foreach (var item in character.clothingItems)
			{
				if (item.displayName == removeClothingItem.DAZClothingItemName)
				{
					character.SetActiveClothingItem(item, false);
					return;
				}
			}
		}


		private void RemoveToggle(UIDynamicToggle toggle)
		{
			try
			{
				
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void NextLook()
		{
			var mutation = CreateMorphMutation();
			ApplyMutation(ref mutation);
			NextHair();
			MutateClothing();
		}

		public Mutation RetryMutation()
		{
			if (_mutationStack.Count > 0) UndoPreviousMutation();
			var mutation = CreateMorphMutation();
			ApplyMutation(ref mutation);
			return mutation;
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
				appropriateMorphs = _morphs.ToList();
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
			foreach (JSONStorableBool morphSetToggle in _predefinedMorphsSetToggles)
			{
				if (!morphSetToggle.val) continue;
				var morphSet = _predefinedMorphsSets.Sets[morphSetToggle.name];
				foreach (var morphName in morphSet)
				{
					var dazMorph = _morphs.FirstOrDefault(m => m.displayName == morphName);
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
			foreach (var catName in selectedMorphCategories)
			{
				var morphUids = _morphCategoryMap.Where(cat => cat.Value == catName).Select(c => c.Key);
				var morphs = _morphs.Where(m => morphUids.Contains(m.displayName));
				morphSet.AddRange(morphs);
				//if( !selectedMorphCategories.Contains(m.group) || morphSet.Any(am => am.displayName == m.displayName)) continue;
				//morphSet.Add(m);
			}
			return morphSet;
			//return _morphs.Where(m => selectedMorphCategories.Contains(m.group)).ToList();
		}

		private List<DAZMorph> SelectFavoriteMorphs(List<DAZMorph> morphs)
		{
			return morphs.Where(m => m.favorite).ToList();
		}

		private void AddMorphToggle(ref MorphMutation mutationComponent, Mutation parentMutation)
		{
			try
			{
				var itemName = mutationComponent.Name;
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
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.FaceGenMorphSet = parentMutation.FaceGenMorphSet.Where(m => m.Name != name).ToList();
				};
				var infoToggle = _context.AddInfoCheckbox(itemName, mutation.Active, toggleAction, stopTracking);
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
				mutationComponent.DynamicCheckbox = infoToggle;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}

		private void AddHairToggle(ref HairMutation mutationComponent, Mutation parentMutation)
		{
			try
			{
				var itemName = mutationComponent.DAZHairGroupName;
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				//_context.RegisterBool(toggleData);
				var mutation = mutationComponent;

				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyHairItem(mutation);
					else RemoveHairItem(mutation);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.HairItems = parentMutation.HairItems.Where(m => m.DAZHairGroupName != name).ToList();
				};
				var infoToggle = _context.AddInfoCheckbox(itemName, mutation.Active, toggleAction, stopTracking);
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
				mutationComponent.DynamicCheckbox = infoToggle;
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

		private void AddActiveMorphToggle(ref MorphMutation mutationComponent, Mutation parentMutation)
		{
			try
			{
				var itemName = mutationComponent.Name;
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				var mutation = mutationComponent;
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyActiveMorphItem(mutation);
					else RemoveActiveMorphItem(mutation);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.ActiveMorphs = parentMutation.ActiveMorphs.Where(m => m.Name != name).ToList();
				};
				var infoToggle = _context.AddInfoCheckbox(itemName, mutation.Active, toggleAction, stopTracking);
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
				mutationComponent.DynamicCheckbox = infoToggle;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.ToString());
				throw exc;
			}
		}

		private void AddClothingToggle(ref ClothingMutation mutationComponent, Mutation parentMutation)
		{
			try
			{
				var itemName = mutationComponent.DAZClothingItemName;
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
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.ClothingItems = parentMutation.ClothingItems.Where(m => m.DAZClothingItemName != name).ToList();
				};
				newToggle.toggle.onValueChanged.AddListener(toggleAction);
				mutationComponent.UiToggle = newToggle;
				var infoToggle = _context.AddInfoCheckbox(itemName, mutation.Active, toggleAction, stopTracking);
				mutationComponent.DynamicCheckbox = infoToggle;
			}
			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				throw exc;
			}
		}
	}
}
