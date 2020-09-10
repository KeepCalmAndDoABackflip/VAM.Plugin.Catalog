using juniperD.Models;
using SimpleJSON;
using System;
using System.Collections;
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

		public bool MustCaptureFaceGenMorphs { get; set; } = true;
		public bool MustCaptureHair { get; set; } = false;
		public bool MustCaptureClothes { get; set; } = false;
		//public bool MustCaptureDynamicItems { get; set; } = false;
		public bool MustCaptureActiveMorphs { get; set; } = false;
		//public bool MustCapturePose { get; set; } = false;
		public bool MustCaptureAnimations { get; set; } = false;
		public bool MustCapturePoseMorphs { get; internal set; }

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

		DateTime _checkpoint = DateTime.Now;
		protected List<JSONStorableBool> _predefinedMorphsSetToggles = new List<JSONStorableBool>();
		protected List<JSONStorableBool> _categoryMorphsSetToggles = new List<JSONStorableBool>();

		protected List<PoseTransition> _transitioningAtomControllers = new List<PoseTransition>();

		protected Dictionary<string, Stack<Mutation>> _mutationStacks = new Dictionary<string, Stack<Mutation>>();
		protected Dictionary<string, List<MorphMutation>> _activeMorphStackForPerson = new Dictionary<string, List<MorphMutation>>();
		protected Dictionary<string, List<PoseMutation>> _poseBaseValuesForTrackedPerson = new Dictionary<string, List<PoseMutation>>();
		protected Dictionary<string, List<MorphMutation>> _morphBaseValuesForTrackedPerson = new Dictionary<string, List<MorphMutation>>();
		protected Dictionary<string, List<MorphMutation>> _animatedMorphsForPerson = new Dictionary<string, List<MorphMutation>>();
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
				if (MustCaptureClothes) newMutation.ClothingItems = GetActiveClothes();
				if (MustCaptureHair) newMutation.HairItems = GetActiveHair();
				if (MustCaptureFaceGenMorphs) newMutation.FaceGenMorphSet = GetCurrentMutationMorphs();
				if (MustCaptureActiveMorphs) newMutation.ActiveMorphs = CaptureActiveMorphsForCurrentPerson();
				if (MustCapturePoseMorphs) newMutation.PoseMorphs = CapturePoseMorphsForCurrentPerson();
				if (MustCaptureAnimations) newMutation.Animations = CaptureAnimationsForCurrentPerson();
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
				newMutation.StoredAtoms = new List<StoredAtom> { CaptureAtom(atom) };
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
			if (mutationStack == null) return new List<MorphMutation>(); ;
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
			var trackingKey = GetTrackinKeyForCurrentPerson();
			if (!_mutationStacks.ContainsKey(trackingKey))
			{
				_mutationStacks.Add(trackingKey, new Stack<Mutation>());
			}
			return _mutationStacks[trackingKey];
		}

		public string GetTrackinKeyForCurrentPerson()
		{
			var personName = GetSelectedPersonAtomOrDefault()?.name;
			var catalogName = _context._catalogName.val;
			if (personName == null) return null;
			var trackingKey = personName + ":" + catalogName;
			return trackingKey;
		}

		public void SetMorphBaseValuesForCurrentPerson()
		{
			try
			{
				var trackingKey = GetTrackinKeyForCurrentPerson();
				if (!_animatedMorphsForPerson.ContainsKey(trackingKey)) _animatedMorphsForPerson.Add(trackingKey, new List<MorphMutation>());
				SetAnimatedMorphsForCurrentPerson();
				if (!_morphBaseValuesForTrackedPerson.ContainsKey(trackingKey)) _morphBaseValuesForTrackedPerson.Add(trackingKey, new List<MorphMutation>());
				List<DAZMorph> morphs = GetMorphsForSelectedPersonOrDefault();
				var animatedMorphIds = _animatedMorphsForPerson[trackingKey].Select(m => m.Id).ToList();
				List<DAZMorph> nonAnimatedMorphs = morphs.Where(bm => !animatedMorphIds.Contains(GetMorphId(bm))).ToList();
				List<MorphMutation> morphBaseValues = new List<MorphMutation>();
				for (int mIndex = 0; mIndex < nonAnimatedMorphs.Count; mIndex++)
				{
					var morph = nonAnimatedMorphs[mIndex];
					MorphMutation newMorph = GetMutationMorphFromMorph(morph);
					morphBaseValues.Add(newMorph);
				}
				_morphBaseValuesForTrackedPerson[trackingKey] = morphBaseValues;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private MorphMutation GetMutationMorphFromMorph(DAZMorph morph)
		{
			return new MorphMutation()
			{
				Id = GetMorphId(morph),
				Value = GetMorphValue(morph),
				Label = morph.displayName,
				PreviousValue = morph.startValue
			};
		}

		//private void WaitForAnimatedMorphsCheck()
		//{
		//	_checkingForAnimatedMorphs = true;
		//	_context.StartCoroutine(SetAnimatedMorphsForCurrentPerson());
		//	DateTime overflow = DateTime.Now;
		//	while (_checkingForAnimatedMorphs) { 
		//		if ((DateTime.Now - overflow).TotalSeconds > 5)
		//		{
		//			SuperController.LogError("Got stuck waiting for animated morphs");
		//			break;
		//		}
		//	}
		//}

		void SetAnimatedMorphsForCurrentPerson()
		{
			try
			{
				var morphs = GetMorphsForSelectedPersonOrDefault();
				var staleMorphs = GetMorphMutationsFromMorphs(morphs);
				DateTime delay = DateTime.Now;
				while ((DateTime.Now - delay).TotalSeconds < 1) { }
				//List<MorphMutation> activeMorphsAfter1Second = CaptureActiveMorphsForCurrentPerson();
				List<MorphMutation> animatedMorphs = DeltaMorphsForCurrentPerson(staleMorphs);
				var trackingKey = GetTrackinKeyForCurrentPerson();
				if (!_animatedMorphsForPerson.ContainsKey(trackingKey))
				{
					_animatedMorphsForPerson.Add(trackingKey, new List<MorphMutation>());
				}
				_animatedMorphsForPerson[trackingKey] = animatedMorphs;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private List<MorphMutation> GetMorphMutationsFromMorphs(List<DAZMorph> morphs)
		{
			return morphs.Select(GetMutationMorphFromMorph).ToList();
		}

		private List<MorphMutation> DeltaMorphsForCurrentPerson(List<MorphMutation> previousMorphs)
		{
			var currentMorphs = GetMorphsForSelectedPersonOrDefault();
			var newMorphs = GetMorphMutationsFromMorphs(currentMorphs);
			var changedMorphs = new List<MorphMutation>();
			for (var p = 0; p < previousMorphs.Count; p++)
			{
				var prevMorph = previousMorphs[p];
				var currMorph = newMorphs.FirstOrDefault(c => c.Id == prevMorph.Id);
				if (currMorph == null) continue;
				if (currMorph.Value != prevMorph.Value)
				{
					currMorph.PreviousValue = prevMorph.Value;
					changedMorphs.Add(currMorph);
				}
				newMorphs.Remove(currMorph);
			}
			return changedMorphs;
		}

		public void SetMorphValue(DAZMorph morph, float value)
		{
			morph.morphValue = value;
			//morph.appliedValue = value;
			morph.SetValue(value);
		}

		public bool IsClothingItemAbsoluteMatch(ClothingMutation mutation, DAZClothingItem item)
		{
			return mutation.Id == GetClothingId(item);
		}

		public bool IsClothingItemFallbackMatch(ClothingMutation mutation, DAZClothingItem item)
		{
			return mutation.Id == item.displayName;
		}

		public string GetClothingId(DAZClothingItem item)
		{
			return item.uid;
		}

		public bool IsHairItemAbsoluteMatch(HairMutation mutation, DAZHairGroup item)
		{
			return mutation.Id == GetHairId(item);
		}

		public bool IsHairItemFallbackMatch(HairMutation mutation, DAZHairGroup item)
		{
			return mutation.Id == item.displayName;
		}

		public string GetHairId(DAZHairGroup item)
		{
			return item.uid;
		}

		public string GetMorphId(DAZMorph morph)
		{
			return morph.uid;
		}

		public float GetMorphValue(DAZMorph morph)
		{
			return morph.morphValue;
		}

		public DAZMorph GetMorphByIdOrDefault(List<DAZMorph> morph, string id)
		{
			return morph.FirstOrDefault(m => m.uid == id);
		}

		private TimeSpan TimeSinceLastCheckpoint()
		{
			var currentTime = DateTime.Now;
			var interval = currentTime - _checkpoint;
			_checkpoint = currentTime;
			return interval;
		}

		public List<MorphMutation> GetCurrentMorphBaseValues()
		{
			return _morphBaseValues;
		}

		public DAZMorph GetMorphByNameOrDefault(string morphId)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			return morphs.FirstOrDefault(m => GetMorphId(m) == morphId);
		}

		public List<HairMutation> GetActiveHair()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return new List<HairMutation>();
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			var items = character.hairItems;
			return items.Where(h => h.active).Select(item => new HairMutation()
			{
				Id = GetHairId(item), 
				Label = item.displayName,
				DAZHairGroup = item
			}).ToList();
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
				Id = GetClothingId(h),
				Label = h.displayName,
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
					} while (morphIndexesToAdjust.Contains(newMorphIndex) || morphUpdateSet.Any(m => m.Id == GetMorphId(morph)));
					morphIndexesToAdjust.Add(newMorphIndex);
					if (morph == null) continue;

					var currentVal = GetMorphValue(morph);
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
						Id = GetMorphId(morph),
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

		public void ApplyMutation(ref Mutation mutation, float startDelay = 0, float animatedDurationInSeconds = 0, UnityAction whenFinishedCallback = null)
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
				var newActiveMorphItems = new List<MorphMutation>();
				for (var i = 0; i < mutation.ActiveMorphs.Count(); i++)
				{
					var item = mutation.ActiveMorphs.ElementAt(i);
					newActiveMorphItems.Add(item);
					AddActiveMorphToggle(ref item);
					if (!item.Active) continue;
					ApplyActiveMorphItem(item, startDelay, animatedDurationInSeconds, whenFinishedCallback);
				}
				mutation.ActiveMorphs = newActiveMorphItems;
				////--------------------------------------------
				var newPoseItems = new List<PoseMutation>();
				for (var i = 0; i < mutation.PoseMorphs.Count(); i++)
				{
					var item = mutation.PoseMorphs.ElementAt(i);
					newPoseItems.Add(item);
					AddPoseMorphToggle(ref item);
					if (!item.Active) continue;
					ApplyActivePoseItem(item, startDelay, animatedDurationInSeconds);
				}
				mutation.PoseMorphs = newPoseItems;
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
			var morph = GetMorphByIdOrDefault(morphs, morphMutation.Id);
			if (morph == null)
			{
				_context.ShowPopupMessage("Morph cannot be found for this atom");
				return;
			}
			SetMorphValue(morph, morphMutation.Value);
		}

		public List<FreeControllerV3> GetControllerForSelectedPersonOrDefault()
		{
			var selectedAtom = GetSelectedPersonAtomOrDefault();
			if (_context.containingAtom.type == "Person") selectedAtom = _context.containingAtom;
			if (selectedAtom == null || selectedAtom.type != "Person")
			{
				_context.ShowPopupMessage("Please select a Person");
				return null;
			}
			var controllers = selectedAtom.GetComponentsInChildren<FreeControllerV3>(true).ToList();
			return controllers;
		}

		public List<DAZMorph> GetMorphsForSelectedPersonOrDefault()
		{
			var selectedAtom = GetSelectedPersonAtomOrDefault();
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
			DAZClothingItem dazClothingItem = character.clothingItems.FirstOrDefault(h => IsClothingItemAbsoluteMatch(clothingItem, h));
			if (dazClothingItem == null) dazClothingItem = character.clothingItems.FirstOrDefault(h => IsClothingItemFallbackMatch(clothingItem, h));
			if (dazClothingItem == null)
			{
				_context.ShowPopupMessage("Item cannot be used on this Persons type");
				return;
			}
			character.SetActiveClothingItem(dazClothingItem, true);
		}

		public Atom GetSelectedPersonAtomOrDefault()
		{
			if (_context.containingAtom.type == "Person") return _context.containingAtom;
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (selectedAtom != null && selectedAtom.type == "Person") return selectedAtom;
			var personAtoms = SuperController.singleton.GetAtoms().Where(a => a.type == "Person").ToList();
			if (personAtoms.Count == 1) return personAtoms.Single();
			//...else there are either no Person atoms, or more than one Person atom.
			SuperController.LogMessage("Please select a Person in the scene");
			return null;
		}

		public void ApplyHairItem(HairMutation hairItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZHairGroup item = character.hairItems.FirstOrDefault(h => IsHairItemAbsoluteMatch(hairItem, h));
			if (item == null) item = character.hairItems.FirstOrDefault(h => IsHairItemFallbackMatch(hairItem, h));
			if (item == null)
			{
				_context.ShowPopupMessage("Item cannot be used on this Persons type");
				return;
			}
			character.SetActiveHairItem(item, true);
		}

		public bool UndoPreviousMutation(float animatedDurationInSeconds = 0)
		{
			try
			{
				if (_context._overlayMutations.val == true) return false;
				var mutationStack = GetMutationStackForSelectedPersonOrDefault();
				if (mutationStack == null || mutationStack.Count == 0)
				{
					//_context.DebugLog("No previous mutation found");
					return false;
				}
				var lastMutation = mutationStack.Pop();
				UndoMutation(lastMutation, animatedDurationInSeconds);
				return true;
			}

			catch (Exception exc)
			{
				SuperController.LogError(exc.Message + ": " + exc.StackTrace);
				return false;
			}
		}

		private void UndoMutation(Mutation mutation, float animatedDurationInSeconds = 0)
		{
			try
			{
				mutation.IsActive = false;
				foreach (var item in mutation.FaceGenMorphSet)
				{
					_context.RemoveToggle(item.UiToggle);
					UndoMutationMorph(item);
				}

				foreach (var item in mutation.ClothingItems)
				{
					_context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveClothingItem(item);
				}

				foreach (var item in mutation.HairItems)
				{
					_context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveHairItem(item);
				}

				foreach (var item in mutation.ActiveMorphs)
				{
					_context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveActiveMorphItem(item, animatedDurationInSeconds);
				}

				foreach (var item in mutation.PoseMorphs)
				{
					_context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveActivePoseItem(item, animatedDurationInSeconds);
				}

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void UndoMutationMorph(MorphMutation mutationMorph)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			var morph = morphs.FirstOrDefault(m => m.uid == mutationMorph.Id);
			if (morph == null)
			{
				SuperController.LogError($"couldn't find morph to undo ");
				return;
			}
			if (morph != null)
			{
				//SuperController.LogMessage($"setting morph({GetMorphValue(morph)}) to {mutationMorph.PreviousValue}");
				SetMorphValue(morph, mutationMorph.PreviousValue);
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
				if (IsHairItemAbsoluteMatch(removeHairItem, item))
				{
					character.SetActiveHairItem(item, false);
					return;
				}
			}
			foreach (var item in character.hairItems)
			{
				if (IsHairItemFallbackMatch(removeHairItem, item))
				{
					character.SetActiveHairItem(item, false);
					return;
				}
			}
		}

		public List<MorphMutation> CaptureActiveMorphsForCurrentPerson()
		{

			var trackingKey = GetTrackinKeyForCurrentPerson();
			if (!MorphBaseValuesHaveBeenSetForCurrentPerson(trackingKey)) SetMorphBaseValuesForCurrentPerson();

			var baseValuesForPerson = _morphBaseValuesForTrackedPerson[trackingKey];
			var changedMorphs = DeltaMorphsForCurrentPerson(baseValuesForPerson);
			foreach (var changedMorph in changedMorphs)
			{
				changedMorph.Active = true;
			}
			return changedMorphs;
		}

		public List<PoseMutation> CapturePoseMorphsForCurrentPerson()
		{

			var trackingKey = GetTrackinKeyForCurrentPerson();
			var controllers = GetControllerForSelectedPersonOrDefault();
			var posePoints = new List<PoseMutation>();
			foreach (var controller in controllers)
			{

				var newPosePoint = new PoseMutation();
				newPosePoint.Id = controller.name;
				newPosePoint.Active = true;
				newPosePoint.PositionState = controller.currentPositionState.ToString();
				newPosePoint.RotationState = controller.currentRotationState.ToString();
				//if (controller.currentPositionState != FreeControllerV3.PositionState.On && controller.currentRotationState != FreeControllerV3.RotationState.On) continue;
				newPosePoint.Rotation = controller.transform.localRotation.eulerAngles;
				newPosePoint.Position = controller.transform.localPosition;
				if (controller.containingAtom.type == "Person" && controller.name == "control") newPosePoint.Active = false;
				posePoints.Add(newPosePoint);
			}

			return posePoints;
		}

		private bool PoseBaseValuesHaveBeenSetForCurrentPerson(string trackingKey)
		{
			return _poseBaseValuesForTrackedPerson.ContainsKey(trackingKey);
		}

		private bool MorphBaseValuesHaveBeenSetForCurrentPerson(string trackingKey)
		{
			return _morphBaseValuesForTrackedPerson.ContainsKey(trackingKey);
		}

		public void ApplyActiveMorphItem(MorphMutation mutationItem, float startDelay = 0, float duration = 0, UnityAction whenFinishedCallback = null)
		{
			var trackingKey = GetTrackinKeyForCurrentPerson();
			if (!MorphBaseValuesHaveBeenSetForCurrentPerson(trackingKey)) _morphBaseValuesForTrackedPerson.Add(trackingKey, new List<MorphMutation>());
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			DAZMorph morph = morphs.FirstOrDefault(h => GetMorphId(h) == mutationItem.Id);
			if (morph == null)
			{
				_context.ShowPopupMessage("Morph cannot be used on this Person");
				return;
			}
			if (!_morphBaseValuesForTrackedPerson[trackingKey].Any(m => m.Id == GetMorphId(morph)))
			{
				InitializeBaseMorphForPerson(trackingKey, morph);
			}
			_context.StartCoroutine(TransitionApplyMorph(morph, mutationItem.Value, startDelay, duration, whenFinishedCallback, true));
			//SetMorphValue(morph, mutationItem.Value);
			if (!_activeMorphStackForPerson.ContainsKey(trackingKey)) _activeMorphStackForPerson.Add(trackingKey, new List<MorphMutation>());
			_activeMorphStackForPerson[trackingKey].Add(mutationItem);
		}

		public void ApplyActivePoseItem(PoseMutation mutationItem, float startDelay = 0, float duration = 0, UnityAction whenFinishedCallback = null)
		{
			var trackingKey = GetTrackinKeyForCurrentPerson();
			//if (!MorphBaseValuesHaveBeenSetForCurrentPerson(trackingKey)) _morphBaseValuesForTrackedPerson.Add(trackingKey, new List<MorphMutation>());
			var controllers = GetControllerForSelectedPersonOrDefault();
			//foreach (var item in mutationItem.Points)
			//{
			var controller = controllers.FirstOrDefault(c => c.name == mutationItem.Id);
			if (controller == null)
			{
				SuperController.LogMessage("WARNING: could not find controller: " + controller.name);
				return;
			}

			controller.SetPositionStateFromString(mutationItem.PositionState);
			controller.SetRotationStateFromString(mutationItem.RotationState);
			if (controller.currentPositionState == FreeControllerV3.PositionState.Off && controller.currentRotationState == FreeControllerV3.RotationState.Off) return;
			_context.StartCoroutine(TransitionApplyPose(controller, mutationItem.Position, mutationItem.Rotation, startDelay, duration, 1, whenFinishedCallback));
			//controller.transform.localPosition = mutationItem.Position;
			//controller.transform.localRotation = Quaternion.Euler(mutationItem.Rotation);
			
			//}
			//if (morphs == null) return;
			//DAZMorph morph = morphs.FirstOrDefault(h => GetMorphId(h) == mutationItem.Id);
			//if (morph == null)
			//{
			//	_context.ShowPopupMessage("Morph cannot be used on this Person");
			//	return;
			//}
			//if (!_morphBaseValuesForTrackedPerson[trackingKey].Any(m => m.Id == GetMorphId(morph)))
			//{
			//	InitializeBaseMorphForPerson(trackingKey, morph);
			//}
			//_context.StartCoroutine(TransitionApplyMorph(morph, mutationItem.Value, startDelay, duration));
			////SetMorphValue(morph, mutationItem.Value);
			//if (!_activeMorphStackForPerson.ContainsKey(trackingKey)) _activeMorphStackForPerson.Add(trackingKey, new List<MorphMutation>());
			//_activeMorphStackForPerson[trackingKey].Add(mutationItem);
		}

		public IEnumerator TransitionApplyPose(FreeControllerV3 controller, Vector3 targetPosition, Vector3 targetRotation, float startDelay = 0, float transitionTimeInSeconds = 0, float smoothMultiplier = 1, UnityAction whenFinishedCallback = null)
		{
			yield return new WaitForSeconds(startDelay);

			var transitionKey = _context.GetUniqueName();
			var newTransition = new PoseTransition(transitionKey);
			
			if (transitionTimeInSeconds == 0)
			{
				controller.transform.localPosition = targetPosition;
				controller.transform.localRotation = Quaternion.Euler(targetRotation);
			}
			else
			{
				if (smoothMultiplier < 1) smoothMultiplier = 1;
				float framesPerSecond = 25 * smoothMultiplier;
				Vector3 newPosition = controller.transform.localPosition;
				Vector3 newRotation = controller.transform.localRotation.eulerAngles;
				Vector3 totalPositionToAdd = targetPosition - newPosition;
				float largestDistanceToTravel = GetLargestMagnitudeFromVector(totalPositionToAdd);
				//Vector3 totalRotationToAdd = targetRotation - currentRotation;
				Vector3 shortestRotationVector = GetShortestRotationVector(newRotation, targetRotation);
				Vector3 totalRotationToAdd = shortestRotationVector;


				if (newRotation != targetRotation) { 
					//SuperController.LogMessage($"currentRotation: {newRotation}, targetRotation: {targetRotation}");
					//SuperController.LogMessage($"totalRotationToAdd: {totalRotationToAdd}");
					//SuperController.LogMessage($"largestDistanceToTravel: {largestDistanceToTravel}");
				}
				var numberOfIterations = transitionTimeInSeconds * framesPerSecond;
				var positionSingleIterationDistance = totalPositionToAdd / numberOfIterations;
				var rotationSingleIterationDistance = totalRotationToAdd / numberOfIterations;
				
				newTransition.XPositionEnabled = totalPositionToAdd.x != 0;
				newTransition.YPositionEnabled = totalPositionToAdd.y != 0;
				newTransition.ZPositionEnabled = totalPositionToAdd.z != 0;
				newTransition.XRotationEnabled = totalRotationToAdd.x != 0;
				newTransition.YRotationEnabled = totalRotationToAdd.y != 0;
				newTransition.ZRotationEnabled = totalRotationToAdd.z != 0;
				
				RegisterAndMergeTransitionIntoActiveTransitions(newTransition);

				for (int i = 0; i < numberOfIterations; i++)
				{
					try
					{
						float easeFactor = GetEaseFactor(i, numberOfIterations);

						var positionNudgeDistance = new Vector3(positionSingleIterationDistance.x * easeFactor, positionSingleIterationDistance.y * easeFactor, positionSingleIterationDistance.z * easeFactor);
						var rotationNudgeDistance = new Vector3(rotationSingleIterationDistance.x * easeFactor, rotationSingleIterationDistance.y * easeFactor, rotationSingleIterationDistance.z * easeFactor);

						// Move point...
						newPosition += positionNudgeDistance;
						newRotation += rotationNudgeDistance;

						//var xPosition = newTransition.XPositionEnabled ? newPosition.x : controller.transform.localPosition.x;
						//var yPosition = newTransition.YPositionEnabled ? newPosition.y : controller.transform.localPosition.y;
						//var zPosition = newTransition.ZPositionEnabled ? newPosition.z : controller.transform.localPosition.z;
						//var xRotation = newTransition.XRotationEnabled ? newRotation.x : controller.transform.localRotation.x;
						//var yRotation = newTransition.YRotationEnabled ? newRotation.y : controller.transform.localRotation.y;
						//var zRotation = newTransition.ZRotationEnabled ? newRotation.z : controller.transform.localRotation.z;

						var xPosition = newPosition.x;
						var yPosition = newPosition.y;
						var zPosition = newPosition.z;
						var xRotation = newRotation.x;
						var yRotation = newRotation.y;
						var zRotation = newRotation.z;

						var finalPosition = new Vector3(xPosition, yPosition, zPosition);
						var finalRotation = new Vector3(xRotation, yRotation, zRotation);

						controller.transform.localPosition = finalPosition;
						controller.transform.Rotate(rotationNudgeDistance);//.localRotation = Quaternion.Euler(finalRotation);
					}
					catch (Exception e)
					{
						SuperController.LogError(e.ToString());
					}
					yield return new WaitForSeconds(1 / numberOfIterations);
				}
				controller.transform.localPosition = targetPosition;
				controller.transform.localRotation = Quaternion.Euler(targetRotation);
				RemoveActiveTransition(newTransition);
				if (whenFinishedCallback != null) whenFinishedCallback.Invoke();
			}
		}

		private void RemoveActiveTransition(PoseTransition newTransition)
		{
			_transitioningAtomControllers.Remove(newTransition);
		}

		private void RegisterAndMergeTransitionIntoActiveTransitions(PoseTransition newTransition)
		{
			foreach (var olderTransition in _transitioningAtomControllers)
			{
				if (olderTransition.AtomName == newTransition.AtomName && olderTransition.ControllerName == newTransition.ControllerName)
				{
					// Override previous position control
					if (newTransition.XPositionEnabled) olderTransition.XPositionEnabled = false;
					if (newTransition.YPositionEnabled) olderTransition.YPositionEnabled = false;
					if (newTransition.ZPositionEnabled) olderTransition.ZPositionEnabled = false;
					// Override previous rotation control
					if (newTransition.XRotationEnabled) olderTransition.XRotationEnabled = false;
					if (newTransition.YRotationEnabled) olderTransition.YRotationEnabled = false;
					if (newTransition.ZRotationEnabled) olderTransition.ZRotationEnabled = false;
				}
			}
			_transitioningAtomControllers.Add(newTransition);
		}

		private float GetLargestMagnitudeFromVector(Vector3 totalPositionToAdd)
		{
			var xyMax = Math.Abs(totalPositionToAdd.x) > Math.Abs(totalPositionToAdd.y) ? totalPositionToAdd.x : totalPositionToAdd.y;
			return Math.Abs(totalPositionToAdd.z) > Math.Abs(xyMax) ? totalPositionToAdd.z : xyMax;
		}

		private static float GetEaseFactor(int interationNumber, float totalIterations)
		{
			var mean = totalIterations/2;
			var deviation = Math.Abs(mean - interationNumber); //...how far away we are from the center of the bell curve
			var tail = mean - deviation; //...how far away we are from the edges
			var tailRatio = tail / mean; //...ratio of how close we are to the center of the bell curve
			var heightFactor = tailRatio * 2;
			return heightFactor;
		}

		private Vector3 GetShortestRotationVector(Vector3 currentRotation, Vector3 targetRotation)
		{
			//SuperController.LogMessage("X:");
			var xDist = GetShortestDistanceOnAxisRotation(currentRotation.x, targetRotation.x);
			//SuperController.LogMessage("Y:");
			var yDist = GetShortestDistanceOnAxisRotation(currentRotation.y, targetRotation.y);
			//SuperController.LogMessage("Z:");
			var zDist = GetShortestDistanceOnAxisRotation(currentRotation.z, targetRotation.z);
			return new Vector3(xDist, yDist, zDist);
		}

		private static float GetShortestDistanceOnAxisRotation(float currentAngle, float targetAngle)
		{
			var angleDistance = targetAngle - currentAngle;
			var complimentDistance = (360 - angleDistance % 360) * (angleDistance < 0 ? -1 : 1);
			//var moveCouterClockwise  = (targetAngle > currentAngle && angleDistance > complimentDistance) || (targetAngle < currentAngle && angleDistance < complimentDistance);
			var moveClockwise = (targetAngle < currentAngle && angleDistance < complimentDistance) || (targetAngle > currentAngle && angleDistance > complimentDistance);
			var final = Math.Min(Math.Abs(angleDistance), Math.Abs(complimentDistance)) * (moveClockwise ? -1 : 1);//== Math.Abs(angleDistance) ? angleDistance : complimentDistance;

			//if (angleDistance > 0) {
			//	SuperController.LogMessage("   ANGLE: " + angleDistance);
			//	SuperController.LogMessage("   COMPL: " + complimentDistance);
			//	SuperController.LogMessage("   DIREC: " + (moveClockwise ? "clockwise" : "counterClockwise"));
			//	SuperController.LogMessage("   FINAL: " + final);
			//}

			//var counterClockwiseTargetDistance =  Math.Max(currentAngle, targetAngle) - Math.Min(currentAngle, targetAngle);
			//var clockwiseTargetDistanc = 360 - Math.Max(currentAngle, targetAngle) + Math.Min(currentAngle, targetAngle);
			
			//if (final > 0) SuperController.LogMessage($"   angle1: {angle1}, compliment {compliment}, final: {final}");
			return final;
		}

		Vector3 GetBellCurveVector(float iteration, float totalIterations, Vector3 distanceVector)
		{

			var maxDistance = new Vector3(distanceVector.x * 1.5f, distanceVector.y * 1.5f, distanceVector.z * 1.5f);
			var factor = GetBellCurveYPercent(iteration, totalIterations);
			Vector3 actualDistance = new Vector3(maxDistance.x * factor, maxDistance.y * factor, maxDistance.z * factor);
			//SuperController.LogMessage("factor: " + factor);
			//SuperController.LogMessage($"distanceVector: {distanceVector.x},{distanceVector.y},{distanceVector.z}");
			//SuperController.LogMessage($"maxDistance: {maxDistance.x},{maxDistance.y},{maxDistance.z}");
			//SuperController.LogMessage($"actualDistance: {actualDistance.x},{actualDistance.y},{actualDistance.z}");
			return actualDistance;
		}

		private static float GetBellCurveYPercent(float iteration, float totalIterations)
		{
			var xSqr = iteration * iteration;
			var totItSqr = totalIterations * totalIterations;
			var y = (-xSqr + totItSqr);
			var yPercent = y / totItSqr * 100;
			return yPercent;
		}

		//float GetBellCurveYGivenX(float x, float maxDist, float averageDist, float standardDeviation)
		//{
		//	double avg = (double)averageDist;
		//	double amp = (double)maxDist;
		//	double sd = (double)standardDeviation;
		//	return (float)gauss(x, amp, avg, sd);
		//}

		//double gauss(double x, double amp, double ave, double sd)
		//{
		//	var v1 = (x - ave) / (2d * sd * sd);
		//	var v2 = -v1 * v1 / 2d;
		//	var v3 = amp * Math.Exp(v2);
		//	return v3;
		//}

		//// The normal distribution function.
		//private float F(float x, float one_over_2pi, float mean, float stddev, float var)
		//{
		//	return (float)(one_over_2pi * Math.Exp(-(x - mean) * (x - mean) / (2 * var)));
		//}

		//private double GetBellCurvePoint(double Percentage, double Midpoint)
		//{
		//	if (Percentage > Midpoint)
		//	{
		//		Percentage = 1 - Percentage;
		//		return 1 - ((Percentage - ((1 - Percentage) * Percentage)) * (1 / (1 - Midpoint)));
		//	}
		//	else
		//	{
		//		return (Percentage - ((1 - Percentage) * Percentage)) * (1 / Midpoint);
		//	}
		//}


		private static bool IsPointAtTarget(Vector3 currentValue, Vector3 targetValue, Vector3 totalDistanceToMove)
		{
			var xAtTarget = IsAxisAtTarget(currentValue.x, targetValue.x, totalDistanceToMove.x);
			var yAtTarget = IsAxisAtTarget(currentValue.y, targetValue.y, totalDistanceToMove.y);
			var zAtTarget = IsAxisAtTarget(currentValue.z, targetValue.z, totalDistanceToMove.z);

			return IsAxisAtTarget(currentValue.x, targetValue.x, totalDistanceToMove.x)
				&& IsAxisAtTarget(currentValue.y, targetValue.y, totalDistanceToMove.y)
				&& IsAxisAtTarget(currentValue.z, targetValue.z, totalDistanceToMove.z);
		}

		private static Vector3 PushPointTowardTarget(Vector3 currentValue, Vector3 targetValue, Vector3 iterationDistanceToMove, FreeControllerV3 controller, bool positionAsTrueRotationAsFalse)
		{
			currentValue += iterationDistanceToMove;
			if (positionAsTrueRotationAsFalse)
			{
				//controller.transform.Translate(iterationDistanceToMove * Time.deltaTime);
				controller.transform.localPosition = currentValue;
			}
			else
			{
				controller.transform.localRotation = Quaternion.Euler(currentValue);
			}

			return currentValue;
		}

		private static bool IsAxisAtTarget(float currentValue, float targetValue, float totalDistanceToMove)
		{
			if (totalDistanceToMove == 0) return true;
			var result = (totalDistanceToMove < 0 && currentValue <= targetValue) || (totalDistanceToMove > 0 && currentValue >= targetValue);
			return result;
		}

		public IEnumerator TransitionApplyMorph(DAZMorph morph, float targetValue, float startDelay = 0, float transitionTimeInSeconds = 0, UnityAction whenFinishedCallback = null, bool IsMasterMutation = true)
		{
			yield return new WaitForSeconds(startDelay);
			if (transitionTimeInSeconds == 0)
			{
				SetMorphValue(morph, targetValue);
			}
			else
			{
				float framesPerSecond = 25;
				float morphValue = GetMorphValue(morph);
				var totalToAdd = targetValue - morphValue;
				var amountOfIterations = transitionTimeInSeconds * framesPerSecond;
				var iterationDistance = totalToAdd / amountOfIterations;
				// if total to add is negative then targetvalue is less than morph value
				var otherManipulatorPresent = false;
				//while ((totalToAdd < 0 && morphValue > targetValue) || (totalToAdd > 0 && morphValue < targetValue))
				for (var i = 0; i < amountOfIterations; i++)
				{
					float easeFactor = GetEaseFactor(i, amountOfIterations);
					var actualValue = GetMorphValue(morph);
					if (actualValue != morphValue && !IsMasterMutation) // some other process is changing the value of the same morph, exit the loop.
					{
						otherManipulatorPresent = true;
						break;
					}
					morphValue += iterationDistance * easeFactor;
					SetMorphValue(morph, morphValue);
					yield return new WaitForSeconds(1 / amountOfIterations);
				}
				//SetMorphValue(morph, targetValue);
				if (!otherManipulatorPresent) SetMorphValue(morph, targetValue);
			}
			if (whenFinishedCallback != null) whenFinishedCallback.Invoke();
		}

		private void InitializeBaseMorphForPerson(string trackingKey, DAZMorph morph)
		{
			_morphBaseValuesForTrackedPerson[trackingKey].Add(new MorphMutation()
			{
				Id = GetMorphId(morph),
				Value = GetMorphValue(morph)
			});
		}

		public void RemoveActiveMorphItem(MorphMutation mutationItemToRemove, float animatedDurationInSeconds = 0)
		{
			var trackingKey = GetTrackinKeyForCurrentPerson();
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			DAZMorph morph = morphs.FirstOrDefault(m => GetMorphId(m) == mutationItemToRemove.Id);
			if (morph == null)
			{
				return;
			}
			var baseMorph = _morphBaseValuesForTrackedPerson[trackingKey].FirstOrDefault(m => m.Id == mutationItemToRemove.Id);// GetBaseValueForMorph(mutationItemToRemove.Id);
			if (baseMorph == null) return;

			var baseValue = baseMorph.Value;
			_context.StartCoroutine(TransitionApplyMorph(morph, baseValue, 0, animatedDurationInSeconds, null, false));
			//SetMorphValue(morph, baseValue);

			_activeMorphStackForPerson[trackingKey].Remove(mutationItemToRemove);
			//foreach (var item in _morphNewValues)
			//{
			//	if (item.Name == removeItem.Name)
			//	{
			//		//item.active = false;
			//		item.MorphItem.SetValue(baseValue);
			//		return;
			//	}
			//}
		}

		public void RemoveActivePoseItem(PoseMutation mutationItemToRemove, float animatedDurationInSeconds = 0)
		{
			//var trackingKey = GetTrackinKeyForCurrentPerson();
			//var controllers = GetControllerForSelectedPersonOrDefault();
			//var controller = controllers.FirstOrDefault(c => c.name == mutationItemToRemove.Id);
			//if (controller == null)
			//{
			//	SuperController.LogMessage("WARNING: could not find controller: " + controller.name);
			//	return;
			//}
			//_context.StartCoroutine(TransitionApplyPose(controller, mutationItemToRemove.Position, mutationItemToRemove.Rotation, 0, animatedDurationInSeconds));
		}

		public List<AnimationLink> CaptureAnimationsForCurrentPerson()
		{

			var trackingKey = GetTrackinKeyForCurrentPerson();
			var animationLinks = GetSceneAnimations();
			return animationLinks;
		}

		public List<AnimationLink> GetSceneAnimations()
		{
			List<AnimationLink> links = new List<AnimationLink>();
			var sceneJson = SuperController.singleton.GetSaveJSON();
			JSONArray atomsArray = sceneJson["atoms"].AsArray;
			// First pass, to get animation-atoms...
			for (int a = 0; a < atomsArray.Count; a++)
			{
				JSONClass atomJson = atomsArray[a].AsObject;
				string atomName = atomJson["id"].Value;
				string atomType = atomJson["type"].Value;
				JSONArray atomStorables = atomJson["storables"]?.AsArray;
				AnimationLink animPattern = null;
				if (atomType == "AnimationPattern")
				{
					animPattern = links.FirstOrDefault(an => an.Name == atomName) ?? new AnimationLink() { Name = atomName };
					animPattern.AnimationPatternJSON = atomJson;
					var animationPatternStorable = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == "AnimatedObject");
					var slaveLink = animationPatternStorable["receiver"]?.Value;
					animPattern.SlaveAtom = slaveLink.Split(':').First();
					animPattern.SlaveController = slaveLink.Split(':').Last();
					var animationPattern = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == "AnimationPattern");
					var stepNames = animationPattern["steps"].AsArray.Childs.Select(c => c.Value).ToList();
					animPattern.AnimationSteps.AddRange(stepNames.Select(s => new AnimationLinkStep() { Name = s }));
					// Grab AnimationPattern Position and Rotation data...
					var animationPatternControl = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == "control");
					var positionObject = animationPatternControl["position"].AsObject;
					var rotationObject = animationPatternControl["rotation"].AsObject;
					Vector3 atomPosition = new Vector3(float.Parse(positionObject["x"]), float.Parse(positionObject["y"]), float.Parse(positionObject["z"]));
					Quaternion atomRotation = Quaternion.Euler(float.Parse(rotationObject["x"]), float.Parse(rotationObject["y"]), float.Parse(rotationObject["z"]));
					animPattern.AnimationAtomPosition = atomPosition;
					animPattern.AnimationAtomRotation = atomRotation;
				}
				if (atomType == "AnimationStep")
				{
					animPattern = links.FirstOrDefault(an => an.AnimationSteps.Any(s => s.Name == atomName)) ?? new AnimationLink() { Name = atomName };
					var animStep = animPattern.AnimationSteps.FirstOrDefault(s => s.Name == atomName) ?? new AnimationLinkStep() { Name = atomName };
					// Grab AnimationStep Position and Rotation data...
					var animationStepControl = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == "control");
					var positionObject = animationStepControl["position"].AsObject;
					var rotationObject = animationStepControl["rotation"].AsObject;
					Vector3 atomPosition = new Vector3(float.Parse(positionObject["x"]), float.Parse(positionObject["y"]), float.Parse(positionObject["z"]));
					Quaternion atomRotation = Quaternion.Euler(float.Parse(rotationObject["x"]), float.Parse(rotationObject["y"]), float.Parse(rotationObject["z"]));
					animStep.StepPosition = atomPosition;
					animStep.StepRotation = atomRotation;
					// Add JSON...
					animStep.AnimationStepJSON = atomJson;
				}
				if (animPattern != null && !links.Any(p => p.Name == animPattern.Name)) links.Add(animPattern);
			}

			// Second pass, to get slave-atom position and rotation data...
			for (int a = 0; a < atomsArray.Count; a++)
			{
				JSONClass atomJson = atomsArray[a].AsObject;
				string atomName = atomJson["id"].Value;
				string atomType = atomJson["type"].Value;
				JSONArray atomStorables = atomJson["storables"]?.AsArray;
				// Get Parent animation position...
				var animPatterns = links.Where(an => an.SlaveAtom == atomName).ToList();
				foreach (var anim in animPatterns)
				{
					var slaveAtomControl = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == "control");
					var positionObject = slaveAtomControl["position"].AsObject;
					var rotationObject = slaveAtomControl["rotation"].AsObject;
					Vector3 atomPosition = new Vector3(float.Parse(positionObject["x"]), float.Parse(positionObject["y"]), float.Parse(positionObject["z"]));
					Quaternion atomRotation = Quaternion.Euler(float.Parse(rotationObject["x"]), float.Parse(rotationObject["y"]), float.Parse(rotationObject["z"]));
					anim.SlaveAtomPosition = atomPosition;
					anim.SlaveAtomRotation = atomRotation;

					// Get slave controller position and rotation data
					var slaveAtomController = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == anim.SlaveController);
					positionObject = slaveAtomControl["position"].AsObject;
					rotationObject = slaveAtomControl["rotation"].AsObject;
					atomPosition = new Vector3(float.Parse(positionObject["x"]), float.Parse(positionObject["y"]), float.Parse(positionObject["z"]));
					atomRotation = Quaternion.Euler(float.Parse(rotationObject["x"]), float.Parse(rotationObject["y"]), float.Parse(rotationObject["z"]));
					anim.SlaveControllerPosition = atomPosition;
					anim.SlaveControllerRotation = atomRotation;
				}
			}
			return links;
		}

		public void AddAnimationToController(AnimationLink animation, string targetAtomName, string targetControllerName)
		{
			var controller = _context.GetControllerOrDefault(targetAtomName, targetControllerName);
			if (controller == null)
			{
				SuperController.LogMessage("WARNING: Controller not found");
				return;
			}
			var newAnimationName = _context.GetNextAvailableName("Anim_" + targetAtomName + "_" + targetControllerName);
			Action<Atom> onAnimationPatternCreatedCallback = (animationAtom) =>
			{
				try
				{
					var animationReceiverStorable = animationAtom.GetStorableByID("AnimatedObject");
					var animationReceiverJson = animationReceiverStorable.GetJSON();
					if (animationReceiverJson["receiver"] == null) animationReceiverJson.Add("receiver", new JSONNode());
					animationReceiverJson["receiver"] = targetAtomName + ":" + targetControllerName;
					animationReceiverStorable.RestoreFromJSON(animationReceiverJson);

					animationAtom.transform.position = controller.transform.position;
					animationAtom.transform.rotation = controller.transform.rotation;
					animationAtom.mainController.currentPositionState = FreeControllerV3.PositionState.ParentLink;
					animationAtom.mainController.currentRotationState = FreeControllerV3.RotationState.ParentLink;

					var sourceControllerPos = animation.SlaveControllerPosition;
					var sourceControllerRot = animation.SlaveControllerRotation;
					for (var i = 0; i < animation.AnimationSteps.Count; i++)
					{
						var step = animation.AnimationSteps[i];
						var sourceStepPos = step.StepPosition;
						var sourceStepRot = step.StepRotation;
						var tagetControllerPos = controller.transform.position;
						var tagetControllerRot = controller.transform.rotation;
						var targetStepPos = ((sourceStepPos - sourceControllerPos) + tagetControllerPos);
						//var targetStepRot = Quaternion.Euler(sourceStepRot.eulerAngles + new Vector3(0,0,0));
						var targetStepRot = Quaternion.Euler(sourceStepRot.eulerAngles - sourceControllerRot.eulerAngles + tagetControllerRot.eulerAngles);
						AddChildAnimationStep(newAnimationName, i, targetStepPos, targetStepRot);
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			_context.StartCoroutine(_context.CreateAtom("AnimationPattern", newAnimationName, controller.transform.position + new Vector3(0.5f, 0.5f, 0.5f), controller.transform.rotation, onAnimationPatternCreatedCallback));
		}

		public Vector3 RotateAroundXAxis(float angle, Vector3 baseVector)
		{
			var rotateAroundXMatrix = new float[]{
				1, 0, 0,
				0, (float)Math.Cos(angle), (float)-Math.Sin(angle),
				0, (float)Math.Sin(angle), (float)Math.Cos(angle)
			};
			return DotProduct(rotateAroundXMatrix, baseVector);
		}

		public Vector3 RotateAroundYAxis(float angle, Vector3 baseVector)
		{
			var rotateAroundYMatrix = new float[]{
				(float)Math.Cos(angle), 0, (float)Math.Sin(angle),
				0, 1, 0,
				(float)-Math.Sin(angle), 0, (float)Math.Cos(angle)
			};
			return DotProduct(rotateAroundYMatrix, baseVector);
		}

		public Vector3 RotateAroundZAxis(float angle, Vector3 baseVector)
		{
			var rotateAroundZMatrix = new float[]{
				(float)Math.Cos(angle), (float)-Math.Sin(angle), 0,
				(float)Math.Sin(angle), (float)Math.Cos(angle), 0,
				0, 0, 1
			};
			return DotProduct(rotateAroundZMatrix, baseVector);
		}

		private Vector3 DotProduct(float[] matrix, Vector3 baseVector)
		{
			return new Vector3(
					matrix[0] * baseVector.x + matrix[1] * baseVector.x + matrix[2] * baseVector.x,
					matrix[3] * baseVector.y + matrix[4] * baseVector.y + matrix[5] * baseVector.y,
					matrix[6] * baseVector.z + matrix[7] * baseVector.z + matrix[8] * baseVector.z
			);
		}

		private void AddChildAnimationStep(string parentAnimationName, int stepNumber, Vector3 position, Quaternion rotation)
		{

			Action<Atom> onAnimationStep1CreatedCallback = (animationStep) =>
			{
				try
				{
					// Adding parent pattern to step...
					var animationStepAsJson = animationStep.GetJSON();
					animationStepAsJson["parentAtom"] = parentAnimationName;
					animationStep.RestoreFromJSON(animationStepAsJson);
					// Adding step to pattern...
					var parentAnimationPattern = _context.GetAtomById(parentAnimationName);
					var animationPatternStorable = parentAnimationPattern.GetStorableByID("AnimationPattern");
					var animationPatternStorableJson = animationPatternStorable.GetJSON();
					animationPatternStorableJson["steps"].Add(new JSONNode());
					animationPatternStorableJson["steps"][animationPatternStorableJson["steps"].Count - 1] = animationStep.name;
					animationPatternStorable.RestoreFromJSON(animationPatternStorableJson);
					animationPatternStorable.LateRestoreFromJSON(animationPatternStorableJson);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			var newAnimationStepName = parentAnimationName + "Step" + stepNumber;
			_context.StartCoroutine(_context.CreateAtom("AnimationStep", newAnimationStepName, position, rotation, onAnimationStep1CreatedCallback));
		}

		public void RemoveClothingItem(ClothingMutation removeClothingItem)
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			foreach (var item in character.clothingItems)
			{
				if (IsClothingItemAbsoluteMatch(removeClothingItem, item))
				{
					character.SetActiveClothingItem(item, false);
					return;
				}
			}
			foreach (var item in character.clothingItems)
			{
				if (IsClothingItemFallbackMatch(removeClothingItem, item))
				{
					character.SetActiveClothingItem(item, false);
					return;
				}
			}
		}

		public IEnumerable<DAZClothingItem> GetClothingItemsForSelectedPersonOrDefault()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return null;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			return character.clothingItems;
		}

		public IEnumerable<DAZHairGroup> GetHairItemsForSelectedPersonOrDefault()
		{
			Atom selectedAtom = GetSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return null;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			return character.hairItems;
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
				appropriateMorphs = DistinctById(appropriateMorphs);
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

		private List<DAZMorph> DistinctById(IEnumerable<DAZMorph> morphs)
		{
			var distictList = new List<DAZMorph>();
			foreach (var morph in morphs)
			{
				if (distictList.Any(m => GetMorphId(m) == GetMorphId(morph))) continue;
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
				var morphNames = _morphCategoryMap.Where(cat => cat.Value == catName).Select(c => c.Key);
				var selectedMorphs = morphs.Where(m => morphNames.Contains(m.displayName));
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

		private void AddPoseMorphToggle(ref PoseMutation mutationComponent)
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
					if (isChecked) ApplyActivePoseItem(mutation);
					else RemoveActivePoseItem(mutation);
				};
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
