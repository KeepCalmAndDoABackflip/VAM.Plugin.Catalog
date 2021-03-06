﻿using juniperD.Models;
using Leap.Unity;
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

		private List<string> _nonObjectAtomTypes = new List<string>() { "CoreControl", "NavigationPanel", "None" };
		private List<string> _nonObjectAtomNames = new List<string>() { "CoreControl", "NavigationPanel", "None" };

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
		public bool MustCaptureActiveMorphs { get; set; } = false;
		public bool MustCaptureAnimations { get; set; } = false;
		public bool MustCapturePoseMorphs { get; set; }


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
		Dictionary<string, DateTime> _namedCheckpoints = new Dictionary<string, DateTime>();
		protected List<JSONStorableBool> _predefinedMorphsSetToggles = new List<JSONStorableBool>();
		protected List<JSONStorableBool> _categoryMorphsSetToggles = new List<JSONStorableBool>();


		protected List<PoseTransition> _transitioningAtomControllers = new List<PoseTransition>();
		public List<TransitionInProgress> _transitionsWaiting { get; set; } =  new List<TransitionInProgress>();
		public List<TransitionInProgress> _transitionsInProgress = new List<TransitionInProgress>();

		protected Dictionary<string, Stack<Mutation>> _mutationStacks = new Dictionary<string, Stack<Mutation>>();
		protected Dictionary<string, List<MorphMutation>> _activeMorphStackForPerson = new Dictionary<string, List<MorphMutation>>();
		protected Dictionary<string, List<PoseMutation>> _poseBaseValuesForTrackedPerson = new Dictionary<string, List<PoseMutation>>();
		protected Dictionary<string, List<MorphMutation>> _morphBaseValuesForTrackedPerson = new Dictionary<string, List<MorphMutation>>();
		protected Dictionary<string, List<MorphMutation>> _animatedMorphsForPerson = new Dictionary<string, List<MorphMutation>>();
		//protected Dictionary<string, UIDynamicToggle> _approvedMorphToggles = new Dictionary<string, UIDynamicToggle>();
		protected List<int> _hairHistory = new List<int>();
		protected Stack<DAZClothingItem> _clothingStack = new Stack<DAZClothingItem>();

		protected CatalogPlugin _context;

		protected List<string> _nonBodyControllerList = new List<string>()
		{
			"control",
			"hairTool1", "hairTool1UI",
			"hairTool2", "hairTool2UI",
			"hairTool3", "hairTool3UI",
			"hairTool4", "hairTool4UI",
			"hairScalpMaskTool", "hairScalpMaskToolUI",
			"eyeTargetControl"
		};


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
					ApplyMutation(ref emptyDelta, _context.GetUniqueName());
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
				if (MustCapturePoseMorphs) newMutation.PoseMorphs = CapturePoseMorphsForCurrentAtom();
				//if (MustCaptureAnimations) newMutation.Animations = CaptureAnimationsForCurrentPerson();
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

		public void CaptureAtomAction(string atomUid, string storableId, string atomActionName, string initiatorEnum, CatalogEntry catalogEntry)
		{
			try
			{
				var atom = SuperController.singleton.GetAtomByUid(atomUid);
				var atomAction = atom.GetAction(atomActionName);
				StoredAction newStoredAction = new StoredAction
				{
					Active = true,
					AtomName = atomUid,
					StorableId = storableId,
					InitiatorEnum = initiatorEnum,
					ActionName = atomActionName
				};
				catalogEntry.Mutation.StoredActions.Add(newStoredAction);
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
			var mutationStack = GetMutationStackForSelectedAtomOrDefault();
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

		private Stack<Mutation> GetMutationStackForSelectedAtomOrDefault()
		{
			var trackingKey = GetTrackingKeyForCurrentAtom();
			if (trackingKey == null) return null;
			if (!_mutationStacks.ContainsKey(trackingKey))
			{
				_mutationStacks.Add(trackingKey, new Stack<Mutation>());
			}
			return _mutationStacks[trackingKey];
		}

		public string GetTrackingKeyForCurrentAtom()
		{
			var personName = GetContainingOrSelectedAtomOrDefault()?.name;
			if (personName == null)
			{
				SuperController.LogMessage("REQUEST: Please select an Object in the scene");
				_context.ShowPopupMessage("Please select an object in the scene", 2);
				return null;
			}
			var catalogName = _context._catalogName.val;
			var trackingKey = personName + ":" + catalogName;
			return trackingKey;
		}

		public void SetMorphBaseValuesForCurrentPerson()
		{
			try
			{
				var selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
				if (!IsValidObjectAtom(selectedAtom) || selectedAtom.type != "Person") return;
				var trackingKey = GetTrackingKeyForCurrentAtom();
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
				if (morphs == null) return;
				var staleMorphs = GetMorphMutationsFromMorphs(morphs);
				DateTime delay = DateTime.Now;
				while ((DateTime.Now - delay).TotalSeconds < 1) { }
				//List<MorphMutation> activeMorphsAfter1Second = CaptureActiveMorphsForCurrentPerson();
				List<MorphMutation> animatedMorphs = DeltaMorphsForCurrentPerson(staleMorphs);
				var trackingKey = GetTrackingKeyForCurrentAtom();
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
			if (morphs == null) return new List<MorphMutation>();
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
			morph.SetValue(value);
		}

		public void SetMorphValue(string morphId, float value)
		{
			var morph = GetMorphOrDefault(morphId);
			if (morph == null)
			{
				SuperController.LogError("Unable to find morph: " + morphId);
				return;
			}
			SetMorphValue(morph, value);
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

		public float? GetMorphValueOrDefaultForSelectedPersonOrDefault(string morphId)
		{
			var morph = GetMorphForSelectedPersonOrDefault(morphId);
			if (morph == null) return null;
			return GetMorphValue(morph);
		}

		public float GetMorphValue(DAZMorph morph)
		{
			return morph.morphValue;
		}

		public DAZMorph GetMorphByIdOrDefault(List<DAZMorph> morph, string id)
		{
			return morph.FirstOrDefault(m => m.uid == id);
		}

		public TimeSpan TimeSinceLastCheckpoint()
		{
			var currentTime = DateTime.Now;
			var interval = currentTime - _checkpoint;
			_checkpoint = currentTime;
			return interval;
		}

		public TimeSpan TimeSinceLastCheckpoint(string name)
		{
			if (!_namedCheckpoints.ContainsKey(name)) _namedCheckpoints.Add(name, DateTime.Now);
			var currentTime = DateTime.Now;
			var interval = currentTime - _namedCheckpoints[name];
			_namedCheckpoints[name] = currentTime;
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			character.RemoveAllHair();
		}

		public void RemoveAllClothing()
		{
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			character.RemoveAllClothing();
		}

		private void NextHair()
		{
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
					DAZMorph morph = null;
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

		public void ApplyMutation(ref Mutation mutation, string transitionGroupKey, float startDelay = 0, float animatedDurationInSeconds = 0, bool excludeUi = false, Awaiter finalComplete = null, CancellationToken cancellationToken = null)
		{

			if (mutation == null)
			{
				if (finalComplete != null) finalComplete.Complete();
				return;
			}

			AwaiterRegistry awaiterRegistry = new AwaiterRegistry(finalComplete);
			UnityAction<string> ticketCompletion = (ticketStub) =>
			{
				//SuperController.LogMessage("DEBUG: Completing ticket: " + ticketStub);
			};
			var applyMutationComplete = awaiterRegistry.GetTicket();
			var clothingItemsComplete = mutation.ClothingItems.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var hairItemsComplete = mutation.HairItems.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var faceGenMorphSetComplete = mutation.FaceGenMorphSet.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var poseMorphsComplete = mutation.PoseMorphs.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var activeMorphsComplete = mutation.ActiveMorphs.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var storedActionsComplete = mutation.StoredActions.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var storedAtomsComplete = mutation.StoredAtoms.Select(c => awaiterRegistry.GetTicket(ticketCompletion)).ToList();
			var sceneLoadComplete = string.IsNullOrEmpty(mutation.ScenePathToOpen) ? null : awaiterRegistry.GetTicket(ticketCompletion);

			try
			{
				var mutationStack = new Stack<Mutation>();
				if (NeedsSelectedAtom(mutation))
				{
					mutationStack = GetMutationStackForSelectedAtomOrDefault();
					if (mutationStack == null) // ...Is null if no atom is selected 
					{
						// Exit if no atom is selected...
						if (finalComplete != null) finalComplete.Complete();
						return;
					}
				}

				mutation.IsActive = true;

				///---Create Captured Atoms-----------------------------------------
				_context.CreateAtomsForCatalogEntry(mutation, storedAtomsComplete);

				///---Open Scene-----------------------------------------
				if (!string.IsNullOrEmpty(mutation?.ScenePathToOpen))
				{
					SuperController.singleton.Load(mutation.ScenePathToOpen);
					sceneLoadComplete.Complete();
				}
				///---Apply Face Morphs-----------------------------------------
				var newMorphSet = new List<MorphMutation>();
				for (var i = 0; i < mutation.FaceGenMorphSet.Count(); i++)
				{
					var morphMutation = mutation.FaceGenMorphSet.ElementAt(i);
					newMorphSet.Add(morphMutation);
					if (!excludeUi) AddFaceGenMorphToggle(ref morphMutation);
					if (morphMutation.Active) faceGenMorphSetComplete[i].Complete(); // ...TODO: Push this into the method
				}
				mutation.FaceGenMorphSet = newMorphSet;
				///---Apply Clothing Items-----------------------------------------
				var newClothingItems = new List<ClothingMutation>();
				for (var i = 0; i < mutation.ClothingItems.Count(); i++)
				{
					var clothingItem = mutation.ClothingItems.ElementAt(i);
					newClothingItems.Add(clothingItem);
					if (!excludeUi) AddClothingToggle(ref clothingItem);
					if (clothingItem.Active) ApplyClothingItem(clothingItem);
					clothingItemsComplete[i].Complete(); // ...TODO: Push this into the method
				}
				mutation.ClothingItems = newClothingItems;
				///---Apply Hair Items---------------------------------------
				var newHairItems = new List<HairMutation>();
				for (var i = 0; i < mutation.HairItems.Count(); i++)
				{
					var hairItem = mutation.HairItems.ElementAt(i);
					newHairItems.Add(hairItem);
					if (!excludeUi) AddHairToggle(ref hairItem);
					if (hairItem.Active) ApplyHairItem(hairItem);
					hairItemsComplete[i].Complete(); // ...TODO: Push this into the method
				}
				mutation.HairItems = newHairItems;
				///---Apply Morph Transitions-----------------------------------------
				var newActiveMorphItems = new List<MorphMutation>();
				for (var i = 0; i < mutation.ActiveMorphs.Count(); i++)
				{
					var item = mutation.ActiveMorphs.ElementAt(i);
					newActiveMorphItems.Add(item);
					if (!excludeUi) AddActiveMorphToggle(ref item);
					if (item.Active) ApplyActiveMorphItem(item, transitionGroupKey, startDelay, animatedDurationInSeconds, activeMorphsComplete[i], cancellationToken);
					else activeMorphsComplete[i].Complete();
				}
				mutation.ActiveMorphs = newActiveMorphItems;
				///---Apply Pose Transitions-----------------------------------------
				var newPoseItems = new List<PoseMutation>();
				for (var i = 0; i < mutation.PoseMorphs.Count(); i++)
				{
					var item = mutation.PoseMorphs.ElementAt(i);
					newPoseItems.Add(item);
					if (!excludeUi) AddPoseMorphToggle(ref item);
					if (item.Active) ApplyActivePoseItem(item, transitionGroupKey, startDelay, animatedDurationInSeconds, poseMorphsComplete[i], cancellationToken);
					else poseMorphsComplete[i].Complete();
				}
				mutation.PoseMorphs = newPoseItems;
				///---Apply In-Actions-----------------------------------------
				var newActions = new List<StoredAction>();
				for (var i = 0; i < mutation.StoredActions.Count(); i++)
				{
					var item = mutation.StoredActions.ElementAt(i);
					newActions.Add(item);
					if (!excludeUi) AddStoredActionToggle(ref item);
					if (item.Active && item.InitiatorEnum == StoredAction.ENUM_INITIATOR_FRAME_IN) ApplyStoredAction(item);
					storedActionsComplete[i].Complete();
				}
				mutation.StoredActions = newActions;
				///--------------------------------------------
				mutationStack.Push(mutation);
				applyMutationComplete.Complete();

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ApplyStoredAction(StoredAction item)
		{
			var atom = SuperController.singleton.GetAtomByUid(item.AtomName);
			var storable = atom.GetStorableByID(item.StorableId);
			storable.CallAction(item.ActionName);
		}

		private bool NeedsSelectedAtom(Mutation mutation)
		{
			return (mutation.HairItems.Any()
				|| mutation.ClothingItems.Any()
				|| mutation.FaceGenMorphSet.Any()
				|| mutation.PoseMorphs.Any()
				|| mutation.ActiveMorphs.Any());
		}

		public void ApplyMutationMorphItem(MorphMutation morphMutation)
		{
			var morph = GetMorphOrDefault(morphMutation.Id);
			if (morph == null)
			{
				_context.ShowPopupMessage("Morph cannot be found for this atom");
				return;
			}
			SetMorphValue(morph, morphMutation.Value);
		}

		public DAZMorph GetMorphOrDefault(string morphId)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return null;
			var morph = GetMorphByIdOrDefault(morphs, morphId);
			return morph;
		}

		public List<FreeControllerV3> GetControllersForContainingOrSelectedPersonOrDefault()
		{
			var selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
			if (selectedAtom == null || selectedAtom.type != "Person")
			{
				_context.ShowPopupMessage("Please select a Person");
				return null;
			}
			var controllers = selectedAtom.GetComponentsInChildren<FreeControllerV3>(true).ToList();
			return controllers;
		}

		public List<FreeControllerV3> GetControllersForContainingOrSelectedAtomOrDefault()
		{
			var selectedAtom = GetContainingOrSelectedAtomOrDefault();
			if (!IsValidObjectAtom(selectedAtom))
			{
				SuperController.LogMessage("REQUEST: Please select an Object in the scene");
				_context.ShowPopupMessage("Please select an object in the scene", 2);
				return null;
			}
			var controllers = selectedAtom.GetComponentsInChildren<FreeControllerV3>(true).ToList();
			return controllers;
		}

		public List<DAZMorph> GetMorphsForSelectedPersonOrDefault()
		{
			var selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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

		public Atom GetContainingOrSelectedPersonAtomOrDefault()
		{
			if (_context.containingAtom != null && _context.containingAtom.type == "Person") return _context.containingAtom;
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (selectedAtom != null && selectedAtom.type == "Person") return selectedAtom;
			var personAtoms = SuperController.singleton.GetAtoms().Where(a => a.type == "Person").ToList();
			if (personAtoms.Count == 1) return personAtoms.Single();
			//...else there are either no Person atoms, or more than one Person atom.
			SuperController.LogMessage("REQUEST: Please select a Person in the scene");
			_context.ShowPopupMessage("Please select a person in the scene", 2);
			return null;
		}

		public Atom GetContainingOrSelectedAtomOrDefault()
		{
			if (IsValidObjectAtom(_context.containingAtom)) return _context.containingAtom;
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (IsValidObjectAtom(selectedAtom)) return selectedAtom;
			//...else there are either no Person atoms, or more than one Person atom.
			return null;
		}

		public bool IsValidObjectAtom(Atom atom)
		{
			return atom != null
				&& !_nonObjectAtomTypes.Contains(atom.type)
				&& !_nonObjectAtomNames.Contains(atom.name);
		}

		public void ApplyHairItem(HairMutation hairItem)
		{
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			DAZHairGroup item = character.hairItems.FirstOrDefault(h => IsHairItemAbsoluteMatch(hairItem, h));
			if (item == null) item = character.hairItems.FirstOrDefault(h => IsHairItemFallbackMatch(hairItem, h));
			if (item == null)
			{
				_context.ShowPopupMessage("WARNING: Item cannot be used on this Persons type");
				return;
			}
			character.SetActiveHairItem(item, true);
		}

		public bool UndoPreviousMutation(float animatedDurationInSeconds = 0)
		{
			try
			{
				if (_context._overlayMutations.val == true) return false;
				var mutationStack = GetMutationStackForSelectedAtomOrDefault();
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

		private void UndoMutation(Mutation mutation, float animatedDurationInSeconds = 1)
		{
			try
			{
				if (mutation == null) return;
				mutation.IsActive = false;
				foreach (var item in mutation.FaceGenMorphSet)
				{
					if (item.UiToggle != null) _context.RemoveToggle(item.UiToggle);
					UndoMutationMorph(item);
				}

				foreach (var item in mutation.ClothingItems)
				{
					if (item.UiToggle != null) _context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveClothingItem(item);
				}

				foreach (var item in mutation.HairItems)
				{
					if (item.UiToggle != null) _context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveHairItem(item);
				}

				foreach (var item in mutation.ActiveMorphs)
				{
					if (item.UiToggle != null) _context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveActiveMorphItem(item, animatedDurationInSeconds);
				}

				foreach (var item in mutation.PoseMorphs)
				{
					if (item.UiToggle != null) _context.RemoveToggle(item.UiToggle);
					if (!item.Active) continue;
					RemoveActivePoseItem(item, animatedDurationInSeconds);
				}

				foreach (var item in mutation.StoredActions)
				{
					if (item.UiToggle != null) _context.RemoveToggle(item.UiToggle);
				}

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void RunExitFrameRoutines(Mutation mutation)
		{
			foreach (var item in mutation.StoredActions)
			{
				if (!item.Active) continue;
				if (item.InitiatorEnum == StoredAction.ENUM_INITIATOR_FRAME_OUT) ApplyStoredAction(item);
			}
		}

		public void UndoMutationMorph(MorphMutation mutationMorph)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			if (morphs == null) return;
			var morph = morphs.FirstOrDefault(m => m.uid == mutationMorph.Id);
			if (morph == null)
			{
				SuperController.LogError($"WARNING: couldn't find morph to undo ");
				return;
			}
			if (morph != null)
			{
				SetMorphValue(morph, mutationMorph.PreviousValue);
			}
		}

		public void RemoveHairItem(HairMutation removeHairItem)
		{

			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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

			var trackingKey = GetTrackingKeyForCurrentAtom();
			if (!MorphBaseValuesHaveBeenSetForCurrentPerson(trackingKey)) SetMorphBaseValuesForCurrentPerson();

			var baseValuesForPerson = _morphBaseValuesForTrackedPerson[trackingKey];
			var changedMorphs = DeltaMorphsForCurrentPerson(baseValuesForPerson);
			foreach (var changedMorph in changedMorphs)
			{
				changedMorph.Active = true;
			}
			return changedMorphs;
		}

		public List<PoseMutation> CapturePoseMorphsForCurrentAtom()
		{
			var controllers = GetControllersForContainingOrSelectedAtomOrDefault();
			if (controllers == null) return new List<PoseMutation>();
			var posePoints = new List<PoseMutation>();

			foreach (var controller in controllers)
			{
				var newPosePoint = new PoseMutation();
				newPosePoint.Id = controller.name;
				newPosePoint.Active = true;
				newPosePoint.PositionState = controller.currentPositionState.ToString();
				newPosePoint.RotationState = controller.currentRotationState.ToString();
				newPosePoint.Rotation = controller.transform.localRotation; //GetControllerRotation(controller);
				newPosePoint.Position = controller.transform.localPosition;
				newPosePoint.StartAtTimeRatio = 0;
				newPosePoint.EndAtTimeRatio = 1;
				if (controller.containingAtom.type == "Person" && _nonBodyControllerList.Contains(controller.name)) newPosePoint.Active = false;
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

		public DAZMorph GetMorphForSelectedPersonOrDefault(string morphId)
		{
			var morphs = GetMorphsForSelectedPersonOrDefault();
			return morphs.FirstOrDefault(h => GetMorphId(h) == morphId);
		}

		public void ApplyActiveMorphItem(MorphMutation mutationItem, string transitionGroupKey, float startDelay = 0, float duration = 0, Awaiter whenFinishedCallback = null, CancellationToken isCancelled = null)
		{
			var trackingKey = GetTrackingKeyForCurrentAtom();
			if (!MorphBaseValuesHaveBeenSetForCurrentPerson(trackingKey)) _morphBaseValuesForTrackedPerson.Add(trackingKey, new List<MorphMutation>());
			DAZMorph morph = GetMorphForSelectedPersonOrDefault(mutationItem.Id);
			if (morph == null)
			{
				_context.ShowPopupMessage("Morph cannot be used on this Person");
				whenFinishedCallback.Complete();
				return;
			}
			if (!_morphBaseValuesForTrackedPerson[trackingKey].Any(m => m.Id == GetMorphId(morph)))
			{
				InitializeBaseMorphForPerson(trackingKey, morph);
			}
			if (!_activeMorphStackForPerson.ContainsKey(trackingKey)) _activeMorphStackForPerson.Add(trackingKey, new List<MorphMutation>());
			_activeMorphStackForPerson[trackingKey].Add(mutationItem);

			// Queue transition...
			var transitionId = Guid.NewGuid().ToString();
			Awaiter whenFinishedManagedTransitionCallback = GetManagedTransitionContainer(transitionId, whenFinishedCallback);
			try
			{
				IEnumerator transition = TransitionApplyMorph(morph, mutationItem.Value, startDelay, duration, whenFinishedManagedTransitionCallback, true, isCancelled);
				var timeout = startDelay + (duration * 2) + 1;
				var newTransitionAndTimeout = new TransitionInProgress(transitionId, transitionGroupKey, transition, startDelay, duration, timeout, isCancelled);
				newTransitionAndTimeout.Description = $"{mutationItem.Label}: GrpId: {transitionGroupKey}, Id: {transitionId}";
				_transitionsWaiting.Add(newTransitionAndTimeout);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				whenFinishedCallback.Complete();
			}
			//_context.StartCoroutine(TransitionApplyMorph(morph, mutationItem.Value, startDelay, duration, whenFinishedCallback, true));
		}

		private Awaiter GetManagedTransitionContainer(string transitionUniqueId, Awaiter whenFinishedCallback, UnityAction onCancelCallback = null)
		{
			return new Awaiter(() =>
			{
				try
				{
					TransitionInProgress transitionToRemove = null;
					transitionToRemove = GetTransitionInProgress(transitionUniqueId);
					if (transitionToRemove == null) SuperController.LogError("cant find transition: " + transitionUniqueId);
					else RemoveTransitionInProgress(transitionToRemove);
				}
				catch (Exception e) { SuperController.LogError(e.ToString()); }
				if (whenFinishedCallback != null) whenFinishedCallback.Complete();
			}, transitionUniqueId, onCancelCallback);
		}

		public void ApplyActivePoseItem(PoseMutation mutationItem, string transitionGroupKey, float startDelay = 0, float duration = 0, Awaiter whenFinishedCallback = null, CancellationToken cancellationToken = null)
		{
			if (!mutationItem.Active)
			{
				whenFinishedCallback.Complete();
				return;
			}
			var controllers = GetControllersForContainingOrSelectedAtomOrDefault();
			if (controllers == null)
			{
				whenFinishedCallback.Complete();
				return;
			}
			var controller = controllers.FirstOrDefault(c => c.name == mutationItem.Id);
			if (controller == null)
			{
				SuperController.LogMessage("WARNING: could not find controller: " + controller.name);
				whenFinishedCallback.Complete();
				return;
			}

			// Start transitioning to next pose...
			var trackingKey = GetTrackingKeyForCurrentAtom();
			startDelay += duration * mutationItem.StartAtTimeRatio;
			duration = (mutationItem.EndAtTimeRatio - mutationItem.StartAtTimeRatio) * duration;

			// Queue transition...
			var transitionId = Guid.NewGuid().ToString();
			Awaiter whenFinishedManagedTransitionCallback = GetManagedTransitionContainer(transitionId, whenFinishedCallback);
			try
			{
				IEnumerator transition = TransitionApplyPose(controller, mutationItem, startDelay, duration, whenFinishedManagedTransitionCallback, cancellationToken);
				var timeout = startDelay + (duration * 2) + 1;
				var newTransitionAndTimeout = new TransitionInProgress(transitionId, transitionGroupKey, transition, startDelay, duration, timeout, cancellationToken);
				newTransitionAndTimeout.Description = $"{controller.name}: GrpId: {transitionGroupKey}, Id: {transitionId}";
				_transitionsWaiting.Add(newTransitionAndTimeout);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				whenFinishedCallback.Complete();
			}
		}

		private void RemoveTransitionInProgress(TransitionInProgress transitionToRemove)
		{
			_transitionsInProgress.Remove(transitionToRemove);
		}

		private TransitionInProgress GetTransitionInProgress(string transitionId)
		{
			return _transitionsInProgress.SingleOrDefault(t => t.UniqueKey == transitionId);
		}

		public IEnumerator TransitionApplyPose(FreeControllerV3 controller, PoseMutation poseMutation, float startDelay = 0, float transitionTimeInSeconds = 0, Awaiter whenFinishedCallback = null, CancellationToken cancellationToken = null)
		{


			float actualStartDelay = startDelay;

			if (transitionTimeInSeconds > 0)
			{
				var startTime = transitionTimeInSeconds * poseMutation.StartAtTimeRatio;
				var endTime = transitionTimeInSeconds * poseMutation.EndAtTimeRatio;
				transitionTimeInSeconds = endTime - startTime;
				actualStartDelay = actualStartDelay + startTime;
			}
			if (actualStartDelay > 0) yield return new WaitForSeconds(startDelay);

			try
			{
				// Set the rotation and position states for the controller...
				controller.SetPositionStateFromString(poseMutation.PositionState);
				controller.SetRotationStateFromString(poseMutation.RotationState);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				if (whenFinishedCallback != null) whenFinishedCallback.Complete();
				throw e;
			}

			if (controller.currentPositionState == FreeControllerV3.PositionState.Off && controller.currentRotationState == FreeControllerV3.RotationState.Off)
			{
				if (whenFinishedCallback != null) whenFinishedCallback.Complete();
				yield break;
			}

			var transitionKey = _context.GetUniqueName();
			var newTransition = new PoseTransition(transitionKey);

			if (transitionTimeInSeconds == 0)
			{
				SetControllerPosition(controller, poseMutation.Position, whenFinishedCallback);
				SetControllerRotation(controller, poseMutation.Rotation, whenFinishedCallback);
				if (whenFinishedCallback != null) whenFinishedCallback.Complete();
				yield break;
			}

			float framesPerSecond = 25;

			Vector3 newPosition = GetControllerPosition(controller, whenFinishedCallback);
			Quaternion initialRotation = GetControllerRotation(controller, whenFinishedCallback);
			Vector3 positionDelta = poseMutation.Position - newPosition;

			var numberOfIterations = transitionTimeInSeconds * framesPerSecond;
			var positionIterationDistance = positionDelta / numberOfIterations;

			newTransition.XPositionEnabled = positionDelta.x != 0;
			newTransition.YPositionEnabled = positionDelta.y != 0;
			newTransition.ZPositionEnabled = positionDelta.z != 0;

			RegisterAndMergeTransitionIntoActiveTransitions(newTransition);

			for (int i = 0; i < numberOfIterations; i++)
			{
				if (cancellationToken?.IsCancelled ?? false) {
					whenFinishedCallback.Cancel();
					yield break;
				}
				newPosition = IncrementPositionAndRotation(controller, poseMutation, newPosition, initialRotation, numberOfIterations, positionIterationDistance, i);
				yield return new WaitForSeconds(transitionTimeInSeconds / numberOfIterations);
			}

			// Set final position and rotation...
			SetControllerPosition(controller, poseMutation.Position, whenFinishedCallback);
			SetControllerRotation(controller, poseMutation.Rotation, whenFinishedCallback);
			RemoveTransitionFromActiveTransitions(newTransition);
			if (whenFinishedCallback != null) whenFinishedCallback.Complete();
		}

		private Quaternion GetControllerRotation(FreeControllerV3 controller, Awaiter onFailureCallback = null)
		{
			try
			{
				return controller.transform.localRotation;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				if (onFailureCallback != null) onFailureCallback.Complete();
				throw e;
			}
		}

		private Vector3 GetControllerPosition(FreeControllerV3 controller, Awaiter onFailureCallback = null)
		{
			try
			{
				return controller.transform.localPosition;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				if (onFailureCallback != null) onFailureCallback.Complete();
				throw e;
			}
		}

		private bool SetControllerPosition(FreeControllerV3 controller, Vector3 position, Awaiter onFailureCallback = null)
		{
			try
			{
				controller.transform.localPosition = position;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				if (onFailureCallback != null) onFailureCallback.Complete();
				throw e;
			}
			return true;
		}

		private bool SetControllerRotation(FreeControllerV3 controller, Quaternion rotation, Awaiter onFailureCallback = null)
		{
			try
			{
				controller.transform.localRotation = rotation;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				if (onFailureCallback != null) onFailureCallback.Complete();
				throw e;
			}
			return true;
		}

		private Vector3 IncrementPositionAndRotation(FreeControllerV3 controller, PoseMutation poseMutation, Vector3 newPosition, Quaternion initialRotation, float numberOfIterations, Vector3 positionIterationDistance, int iteration)
		{
			try
			{
				float positionEaseFactor = GetEaseFactor(iteration, numberOfIterations);
				float shiftingReducer = GetShiftingReducer(iteration + 1, numberOfIterations);
				float stepRatio = 1 / numberOfIterations * (iteration + 1);

				var positionNudgeDistance = new Vector3(positionIterationDistance.x * positionEaseFactor, positionIterationDistance.y * positionEaseFactor, positionIterationDistance.z * positionEaseFactor);
				// Determine new points and angles...
				newPosition += positionNudgeDistance;
				// Perform nudges...
				var nextPosition = new Vector3(newPosition.x, newPosition.y, newPosition.z);
				var nextRotation = Quaternion.Lerp(initialRotation, poseMutation.Rotation, stepRatio + shiftingReducer);
				SetControllerPosition(controller, nextPosition);
				SetControllerRotation(controller, nextRotation);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}

			return newPosition;
		}

		private static Quaternion GetRotationDelta(Quaternion fromRotation, Quaternion toRotation)
		{
			var delataRotation = toRotation * Quaternion.Inverse(fromRotation);
			return delataRotation;
		}

		private static Vector4 GetVector4(Quaternion quaternion)
		{
			return new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
		}

		private void RemoveTransitionFromActiveTransitions(PoseTransition newTransition)
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

		private static float GetEaseFactor(int iterationNumber, float totalIterations, int curveMultiplier = 2)
		{
			float mean = totalIterations / 2;
			float deviation = Math.Abs(mean - iterationNumber); //...how far away we are from the center of the bell curve
			float tail = mean - deviation; //...how far away we are from the edges
			float tailRatio = tail / mean; //...ratio of how close we are to the center of the bell curve
			float heightFactor = tailRatio * curveMultiplier;
			return heightFactor;
		}

		private static float GetShiftingReducer(int iterationNumber, float totalIterations)
		{
			var increment = 1 / (totalIterations); // increment = 1/5 = 0.2
			var progressRatio = iterationNumber / totalIterations; // 0.2 = 1 / 5
			var completionRatio = 1 - progressRatio;
			var reducer = progressRatio - completionRatio;   // reducer = 0.2 - (1-0.2) = 0.2 - 0.8 = -0.6
			return increment * reducer; // 0.2 * -0.6 = -0.12
		}

		public IEnumerator TransitionApplyMorph(DAZMorph morph, float targetValue, float startDelay = 0, float transitionTimeInSeconds = 0, Awaiter whenFinishedCallback = null, bool IsPrimaryMutation = true, CancellationToken isCancelled = null)
		{
			yield return new WaitForSeconds(startDelay);

			if (transitionTimeInSeconds == 0)
			{
				SetMorphValue(morph, targetValue);
				whenFinishedCallback?.Complete();
				yield break;
			}
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
				try
				{
					if (isCancelled?.IsCancelled ?? false) {
						whenFinishedCallback?.Cancel();
						yield break;
					}
					float easeFactor = GetEaseFactor(i, amountOfIterations);
					var actualValue = GetMorphValue(morph);
					if (actualValue != morphValue && !IsPrimaryMutation) // some other process is changing the value of the same morph, exit the loop.
					{
						otherManipulatorPresent = true;
						break;
					}
					morphValue += iterationDistance * easeFactor;
					SetMorphValue(morph, morphValue);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					whenFinishedCallback?.Complete();
					yield break;
				}
				yield return new WaitForSeconds(transitionTimeInSeconds / amountOfIterations);
			}
			//SetMorphValue(morph, targetValue);
			if (!otherManipulatorPresent) SetMorphValue(morph, targetValue);
			whenFinishedCallback?.Complete();
		}

		private void InitializeBaseMorphForPerson(string trackingKey, DAZMorph morph)
		{
			InitializeBaseMorphForPerson(trackingKey, GetMorphId(morph), GetMorphValue(morph));
		}

		public void InitializeBaseMorphForPerson(string trackingKey, string morphId, float morphValue)
		{
			if (!_morphBaseValuesForTrackedPerson.ContainsKey(trackingKey)) {  
				_morphBaseValuesForTrackedPerson.Add(trackingKey, new List<MorphMutation>());
			}
			_morphBaseValuesForTrackedPerson[trackingKey].Add(new MorphMutation()
			{
				Id = morphId,
				Value = morphValue
			});
		}

		public void RemoveActiveMorphItem(MorphMutation mutationItemToRemove, float animatedDurationInSeconds = 0, CancellationToken isCancelled = null)
		{
			var trackingKey = GetTrackingKeyForCurrentAtom();
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
			_context.StartCoroutine(TransitionApplyMorph(morph, baseValue, 0, animatedDurationInSeconds, null, false, isCancelled));
			//SetMorphValue(morph, baseValue);
			_activeMorphStackForPerson[trackingKey].Remove(mutationItemToRemove);
		}

		public void RemoveStoredAction(StoredAction mutationItemToRemove)
		{

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
			var trackingKey = GetTrackingKeyForCurrentAtom();
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
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
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return null;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			return character.clothingItems;
		}

		public IEnumerable<DAZHairGroup> GetHairItemsForSelectedPersonOrDefault()
		{
			Atom selectedAtom = GetContainingOrSelectedPersonAtomOrDefault();
			if (selectedAtom == null) return null;
			JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			return character.hairItems;
		}

		public void NextLook()
		{
			var mutation = CreateMorphMutation();
			ApplyMutation(ref mutation, _context.GetUniqueName());
			NextHair();
			MutateClothing();
		}

		public void RetryMutation()
		{
			var mutationStack = GetMutationStackForSelectedAtomOrDefault();
			if (mutationStack == null) return;
			if (mutationStack.Count > 0) UndoPreviousMutation();
			var mutation = CreateMorphMutation();
			ApplyMutation(ref mutation, _context.GetUniqueName());
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
					if (isChecked) ApplyActiveMorphItem(mutation, _context.GetUniqueName());
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
					if (isChecked) ApplyActivePoseItem(mutation, _context.GetUniqueName());
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

		private void AddStoredActionToggle(ref StoredAction mutationComponent)
		{
			try
			{
				var itemName = $"{mutationComponent.AtomName}:{mutationComponent.ActionName}";
				var toggleData = new JSONStorableBool(itemName, mutationComponent.Active);
				var newToggle = _context.CreateToggle(toggleData, true);
				var mutation = mutationComponent;
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					mutation.Active = isChecked;
					if (isChecked) ApplyStoredAction(mutation);
					else RemoveStoredAction(mutation);
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
