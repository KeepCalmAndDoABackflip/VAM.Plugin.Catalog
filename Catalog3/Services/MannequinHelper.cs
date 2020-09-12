using juniperD.Contracts;
using juniperD.Models;
using juniperD.StatefullServices;
using juniperD.Utils;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace juniperD.Services
{
	public class MannequinHelper
	{
		
		const string POINT_ACTION_NONE = "No action";
		const string POINT_ACTION_SELECT_CONTROLLER = "Select Point in Scene";
		const string POINT_ACTION_NEXT_MODE = "Next Mode";
		const string POINT_ACTION_NEXT_POSITION_MODE = "Next Position Mode";
		const string POINT_ACTION_NEXT_ROTATION_MODE = "Next Rotation Mode";
		const string POINT_ACTION_ON_OFF = "On/Off";
		const string POINT_ACTION_POSITION_ON_OFF = "Position On/Off";
		const string POINT_ACTION_ROTATION_ON_OFF = "Rotation On/Off";

		CatalogPlugin _context;
		CatalogUiHelper _parentWindow;

		public MannequinHelper(CatalogPlugin context, CatalogUiHelper parentWindow)
		{
			_context = context;
			_parentWindow = parentWindow;
		}

		public DynamicMannequinPicker CreateMannequinPicker()
		{
			Atom defaultAtom = GetAtomPreferSelectedThenPersonThenAnyOrDefault();

			var newMannequinPicker = new DynamicMannequinPicker();

			//_mannequinPickers.Add(newMannequinPicker);
			// Create floating window...
			newMannequinPicker.Window = CatalogUiHelper.CreatePanel(_parentWindow.canvas.gameObject, 0, 0, 0, 0, new Color(0.1f, 0.1f, 0.1f, 1f), Color.clear);
			newMannequinPicker.BackPanel = CatalogUiHelper.CreatePanel(newMannequinPicker.Window, 520, 600, -10, -360, new Color(0.1f, 0.1f, 0.1f, 1f), Color.clear);

			var selectionHaloIcon = _context._imageLoaderService.GetFutureImageFromFileOrCached(_context.GetPluginPath() + "/Resources/PointRotation2.png");
			newMannequinPicker.ButtonSelectionHalo = _parentWindow.CreateButton(newMannequinPicker.Window, "", 30, 30, 0, 0, new Color(0.5f, 1, 1, 1f), new Color(0.5f, 1, 1, 1f), Color.clear, selectionHaloIcon);
			newMannequinPicker.ButtonSelectionHalo.transform.localScale = Vector3.zero;

			// Add drag ability to window...
			_context.AddDragging(newMannequinPicker.BackPanel.gameObject, newMannequinPicker.Window.gameObject);

			newMannequinPicker.MannequinOverlay = CatalogUiHelper.CreatePanel(newMannequinPicker.Window, 256, 512, -10, -310, new Color(0.5f, 0.5f, 0.5f), Color.clear);
			// Close button...
			var closeIcon = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/Close.png");
			newMannequinPicker.CloseButton = _parentWindow.CreateButton(newMannequinPicker.Window, "", 25, 25, 0, -345, new Color(0.5f, 0.3f, 0.3f, 1), new Color(1f, 0.5f, 0.5f, 1), new Color(1f, 0.3f, 0.3f, 1), closeIcon);
			_context.SetTooltipForDynamicButton(newMannequinPicker.CloseButton, () => "Close");
			newMannequinPicker.CloseButton.button.onClick.AddListener(() =>
			{
				CloseMannequinPicker(newMannequinPicker);
			});

			// Minimize button...
			var minimizeIcon = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/Minimize4.png");
			newMannequinPicker.MinimizeButton = _parentWindow.CreateButton(newMannequinPicker.Window, "", 25, 25, 40, -345, new Color(0.5f, 0.3f, 0.3f, 1), new Color(1f, 0.5f, 0.5f, 1), new Color(1f, 0.3f, 0.3f, 1), minimizeIcon);
			_context.SetTooltipForDynamicButton(newMannequinPicker.MinimizeButton, () => "Minimize");
			newMannequinPicker.MinimizeButton.button.onClick.AddListener(() =>
			{
				MinimizeMannequinPicker(newMannequinPicker);
			});
			// Refresh button...
			var refreshIcon = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/Refresh.png");
			newMannequinPicker.RefreshButton = _parentWindow.CreateButton(newMannequinPicker.Window, "", 30, 30, 80, -348, new Color(0.5f, 0.3f, 0.3f, 1), new Color(1f, 0.5f, 0.5f, 1), new Color(1f, 0.3f, 0.3f, 1), refreshIcon);
			_context.SetTooltipForDynamicButton(newMannequinPicker.RefreshButton, () => "Refresh");
			newMannequinPicker.RefreshButton.button.onClick.AddListener(() =>
			{
				RefreshMannequinPicker(newMannequinPicker); // ...Window Refresh button click
			});


			// "Atom" combo box...
			//------------------------------------
			var similarAtoms = _context.GetSceneAtoms().Where(a => a.type == defaultAtom.type).ToList();
			var personAtomNames = similarAtoms.Select(a => a.name).ToList();
			UnityAction<string> onPersonSelectorSelect = (atomName) =>
			{
				MannequinSelectAtom(atomName, newMannequinPicker); //...On atom select.
			};
			newMannequinPicker.AtomSelector = _parentWindow.CreateDynamicDropdown(newMannequinPicker.Window, "Atom", personAtomNames, 250, 30, 250, -360, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.4f, 0.1f, 0.1f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), 17, 0, onPersonSelectorSelect);

			// Add mini compoinents for minimized state...
			//------------------------------------
			newMannequinPicker.MiniOverlay = _parentWindow.CreateButton(newMannequinPicker.Window, "", _context.CatalogEntryFrameSize.val, _context.CatalogEntryFrameSize.val, 0, -250, new Color(0.4f, 0.7f, 0.7f), new Color(0.6f, 0.9f, 0.9f), Color.clear);
			newMannequinPicker.MiniOverlay.transform.localScale = Vector3.zero;
			_context.SetTooltipForDynamicButton(newMannequinPicker.MiniOverlay, () => newMannequinPicker.SelectedControllerName + ":" + newMannequinPicker.SelectedPointAction);
			newMannequinPicker.MiniOverlay.button.onClick.AddListener(() =>
			{
				SelectMannequinJoint(newMannequinPicker.SelectedJoint, newMannequinPicker);
			});

			newMannequinPicker.AtomMiniLabel = _parentWindow.CreateButton(newMannequinPicker.Window, "Atom: ", _context.CatalogEntryFrameSize.val - 20, 20, 0, -310, new Color(0.1f, 0.1f, 0.1f, 0.5f), Color.clear, new Color(0.4f, 0.7f, 0.3f, 1));
			newMannequinPicker.AtomMiniLabel.buttonText.fontSize = 17;
			newMannequinPicker.AtomMiniLabel.buttonText.fontStyle = FontStyle.Italic;
			newMannequinPicker.AtomMiniLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			newMannequinPicker.AtomMiniLabel.transform.localScale = Vector3.zero;

			newMannequinPicker.ControllerMiniLabel = _parentWindow.CreateButton(newMannequinPicker.Window, "Controller: ", _context.CatalogEntryFrameSize.val - 20, 20, 0, -290, new Color(0.1f, 0.1f, 0.1f, 0.5f), Color.clear, new Color(0.4f, 0.7f, 03f, 1));
			newMannequinPicker.ControllerMiniLabel.buttonText.fontSize = 17;
			newMannequinPicker.ControllerMiniLabel.buttonText.fontStyle = FontStyle.Italic;
			newMannequinPicker.ControllerMiniLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			newMannequinPicker.ControllerMiniLabel.transform.localScale = Vector3.zero;
			List<string> controllerNames = _context.GetControllerNamesForAtom(defaultAtom);
			//var personAtomNames = personAtoms.Select(a => a.name).ToList();
			UnityAction<string> onPointSelectorSelect = (controllerName) =>
			{
				MannequinSelectController(controllerName, newMannequinPicker);
			};
			if (newMannequinPicker.PointSelector != null) newMannequinPicker.PointSelector.ClearDropdownList(_context);
			newMannequinPicker.PointSelector = _parentWindow.CreateDynamicDropdown(newMannequinPicker.Window, "Point", controllerNames, 250, 30, 250, -310, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.4f, 0.1f, 0.1f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), 17, 0, onPointSelectorSelect);
			//=================================

			// "When Point select" combo box...
			//------------------------------------
			var selectorActions = new List<string>() {
				POINT_ACTION_NONE,
				POINT_ACTION_SELECT_CONTROLLER,
				POINT_ACTION_NEXT_MODE,
				POINT_ACTION_NEXT_POSITION_MODE,
				POINT_ACTION_NEXT_ROTATION_MODE,
				POINT_ACTION_ON_OFF,
				POINT_ACTION_POSITION_ON_OFF,
				POINT_ACTION_ROTATION_ON_OFF
			};
			UnityAction<string> onPointActionSelect = (pointAction) =>
			{
				newMannequinPicker.SelectedPointAction = pointAction;
			};
			newMannequinPicker.PointActionSelector = _parentWindow.CreateDynamicDropdown(newMannequinPicker.Window, "When Point Select", selectorActions, 250, 30, 250, -260, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.4f, 0.1f, 0.1f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), 17, 0, onPointActionSelect);
			newMannequinPicker.SelectedPointAction = POINT_ACTION_SELECT_CONTROLLER;
			newMannequinPicker.PointActionSelector.selectedOption.buttonText.text = POINT_ACTION_SELECT_CONTROLLER;
			//=================================

			// "Add Feature to point" combo box...
			//------------------------------------
			var selectorFeatures = new List<string>()
			{
				PointAddOptionEnum.ANIMATION,
				PointAddOptionEnum.TRIGGER
			};
			UnityAction<string> onAddAction = (selectedLink) =>
			{
				if (selectedLink == PointAddOptionEnum.ANIMATION)
				{
					AddAnimationToController(newMannequinPicker.SelectedAtomName, newMannequinPicker.SelectedControllerName);
				}
				if (selectedLink == PointAddOptionEnum.TRIGGER)
				{
					AddTriggerToController(newMannequinPicker.SelectedAtomName, newMannequinPicker.SelectedControllerName, newMannequinPicker);
				}
			};
			newMannequinPicker.AddFeatureSelector = _parentWindow.CreateDynamicDropdown(newMannequinPicker.Window, "Add Feature To Point", selectorFeatures, 250, 30, 250, -210, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.4f, 0.1f, 0.1f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), 17, 0, onAddAction);
			newMannequinPicker.AddFeatureSelector.selectedOption.buttonText.text = "(select feature)";
			//=================================

			// "Slave", "Master" list boxes...
			//------------------------------------
			UnityAction<string> onLinkedItemSelect = (selectedLink) =>
			{
				var linkName = selectedLink;
				var atomName = linkName.Split(':').First();
				var controllerName = linkName.Split(':').Last();
				SuperController.singleton.SelectController(atomName, controllerName);
				newMannequinPicker.MasterLinkedAtomSelector.selectedOption.buttonText.text = "(select master)";
				newMannequinPicker.SlaveLinkedAtomSelector.selectedOption.buttonText.text = "(select slave)";
			};
			newMannequinPicker.MasterLinkedAtomSelector = _parentWindow.CreateDynamicDropdown(newMannequinPicker.Window, "Master Points", new List<string>(), 250, 30, 250, -160, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.4f, 0.1f, 0.1f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), 17, 70, onLinkedItemSelect);
			newMannequinPicker.MasterLinkedAtomSelector.selectedOption.buttonText.text = "(select master)";
			newMannequinPicker.SlaveLinkedAtomSelector = _parentWindow.CreateDynamicDropdown(newMannequinPicker.Window, "Slave Points", new List<string>(), 250, 30, 250, -40, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.4f, 0.1f, 0.1f, 1f), new Color(0.8f, 0.8f, 0.8f, 1f), 17, 70, onLinkedItemSelect);
			newMannequinPicker.SlaveLinkedAtomSelector.selectedOption.buttonText.text = "(select slave)";
			//=================================

			MannequinSelectAtom(defaultAtom?.name, newMannequinPicker); //...On create mannequin window

			return newMannequinPicker;
		}



		private void MinimizeMannequinPicker(DynamicMannequinPicker picker)
		{
			try
			{
				picker.Minimized = !picker.Minimized;
				bool minimize = picker.Minimized;
				var backpanelRectTransform = picker.BackPanel.GetComponents<RectTransform>().First();
				//backpanelRectTransform.rect.width = minimize ? _defaultFrameSize: 500;
				//backpanelRectTransform.rect.height = _defaultFrameSize: 500;
				var widgetWidth = minimize ? _context.CatalogEntryFrameSize.val : 520;
				var widgetHeight = minimize ? _context.CatalogEntryFrameSize.val : 600;
				backpanelRectTransform.sizeDelta = new Vector2(widgetWidth, widgetHeight);
				var localLeft = minimize ? 90 : 250;
				var localTop = minimize ? 260 : 60;
				picker.BackPanel.transform.localPosition = new Vector3(localLeft, localTop, picker.MannequinOverlay.transform.localPosition.z);

				picker.MannequinOverlay.transform.localScale = minimize ? Vector3.zero : Vector3.one;

				picker.AtomMiniLabel.transform.localScale = minimize ? Vector3.one : Vector3.zero;
				picker.ControllerMiniLabel.transform.localScale = minimize ? Vector3.one : Vector3.zero;
				picker.MiniOverlay.transform.localScale = minimize ? Vector3.one : Vector3.zero;
				
				picker.ButtonSelectionHalo.transform.localScale = minimize ? Vector3.zero : Vector3.one;

				if (picker.AtomSelector != null) picker.AtomSelector.MinimizeDynamicDropdown(_context, minimize);
				if (picker.PointSelector != null) picker.PointSelector.MinimizeDynamicDropdown(_context, minimize);
				if (picker.PointActionSelector != null) picker.PointActionSelector.MinimizeDynamicDropdown(_context, minimize);
				if (picker.AddFeatureSelector != null) picker.AddFeatureSelector.MinimizeDynamicDropdown(_context, minimize);
				if (picker.SlaveLinkedAtomSelector != null) picker.SlaveLinkedAtomSelector.MinimizeDynamicDropdown(_context, minimize);
				if (picker.MasterLinkedAtomSelector != null) picker.MasterLinkedAtomSelector.MinimizeDynamicDropdown(_context, minimize);

				foreach (var jointPoint in picker.JointPoints)
				{
					jointPoint.positionButton.transform.localScale = minimize ? Vector3.zero : Vector3.one;
					jointPoint.rotationButton.transform.localScale = minimize ? Vector3.zero : Vector3.one;
				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void AddTriggerToController(string targetAtomName, string targetControllerName, DynamicMannequinPicker picker = null)
		{
			var controller = _context.GetControllerOrDefault(targetAtomName, targetControllerName);
			if (controller == null)
			{
				SuperController.LogMessage("WARNING: No controller selected");
				return;
			}
			var newTriggerName = _context.GetNextAvailableName("Trigger_" + targetAtomName + "_" + targetControllerName);
			Action<Atom> onAnimationPatternCreatedCallback = (triggerAtom) =>
			{
				try
				{
					var appropriateScale = GetRecommendedTriggerScaleForController(targetAtomName, targetControllerName);
					var scaleStorable = triggerAtom.GetStorableByID("Scale");
					if (scaleStorable == null)
					{
						var triggerAtomJson = triggerAtom.GetJSON();
						var newScaleStorable = new JSONClass();
						newScaleStorable.Add("id", new JSONNode());
						newScaleStorable["id"] = "scale";
						newScaleStorable.Add("scale", new JSONNode());
						newScaleStorable["scale"] = $"{appropriateScale.Scale}";
						triggerAtomJson["storables"].AsArray.Add(newScaleStorable);

						triggerAtom.Restore(triggerAtomJson);
					}
					else
					{
						var scaleJson = scaleStorable.GetJSON();
						if (scaleJson["scale"] == null) scaleJson.Add("scale", new JSONNode());
						scaleJson["scale"] = $"{appropriateScale}";
						scaleStorable.RestoreFromJSON(scaleJson);
						scaleStorable.LateRestoreFromJSON(scaleJson);
					}

					var controlStorable = triggerAtom.GetStorableByID("control");
					var triggerControlJson = controlStorable.GetJSON();
					if (triggerControlJson["linkTo"] == null) triggerControlJson.Add("linkTo", new JSONNode());
					triggerControlJson["linkTo"] = targetAtomName + ":" + targetControllerName;
					controlStorable.RestoreFromJSON(triggerControlJson);
					controlStorable.LateRestoreFromJSON(triggerControlJson);

					var triggerStorable = triggerAtom.GetStorableByID("Trigger");
					var triggerJson = triggerStorable.GetJSON();
					if (triggerJson["invertAtomFilter"] == null) triggerJson.Add("invertAtomFilter", new JSONNode());
					triggerJson["invertAtomFilter"] = "true";
					if (triggerJson["atomFilter"] == null) triggerJson.Add("atomFilter", new JSONNode());
					triggerJson["atomFilter"] = targetAtomName;
					triggerStorable.RestoreFromJSON(triggerJson);
					triggerStorable.LateRestoreFromJSON(triggerJson);

					triggerAtom.mainController.transform.position = controller.transform.position;
					triggerAtom.mainController.transform.rotation = controller.transform.rotation;
					triggerAtom.mainController.currentPositionState = FreeControllerV3.PositionState.ParentLink;
					triggerAtom.mainController.currentRotationState = FreeControllerV3.RotationState.ParentLink;
					
					//if (picker != null) RefreshMannequinPicker(picker);

					SelectAtom(triggerAtom);
					
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			_context.StartCoroutine(_context.CreateAtom("CollisionTrigger", newTriggerName, controller.transform.position, controller.transform.rotation, onAnimationPatternCreatedCallback));

		}

		private TriggerForController GetRecommendedTriggerScaleForController(string atomName, string targetControllerName)
		{
			var atom = _context.GetAtomById(atomName);
			var controller = _context.GetControllerOrDefault(atom, targetControllerName);
			TriggerForController newTriggerInfo = new TriggerForController();
			newTriggerInfo.Scale = Mappings.ControllerTriggerScale[targetControllerName];
			return newTriggerInfo;
		}

		private Atom GetAtomPreferSelectedThenPersonThenAnyOrDefault()
		{
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (selectedAtom != null) return selectedAtom;

			var sceneAtoms = SuperController.singleton.GetAtoms();
			var firstPersonAtom = sceneAtoms.FirstOrDefault(a => a.type == "Person");
			if (firstPersonAtom != null) return firstPersonAtom;

			var firstAvailableAtom = sceneAtoms.FirstOrDefault();
			if (firstAvailableAtom != null) return firstAvailableAtom;

			_context.ShowPopupMessage("Please select an atom from the scene", 2);
			return null;
		}

		private void AddAnimationToController(string targetAtomName, string targetControllerName)
		{
			var controller = _context.GetControllerOrDefault(targetAtomName, targetControllerName);
			if (controller == null)
			{
				SuperController.LogMessage("WARNING: No controller selected");
				return;
			}
			var newAnimationName = _context.GetNextAvailableName("Anim_" + targetAtomName + "_" + targetControllerName);
			Action<Atom> onAnimationPatternCreatedCallback = (animationAtom) =>
			{
				try
				{
					//if (targetControllerName != "control") { // ...We only want to anchor to the controller if its not the controller being animated.
					//	var animationControlStorable = animationAtom.GetStorableByID("control");
					//	var animationControlJson = animationControlStorable.GetJSON();
					//	if (animationControlJson["linkTo"] == null) animationControlJson.Add("linkTo", new JSONNode());
					//	animationControlJson["linkTo"] = targetAtomName + ":" + targetControllerName;
					//	animationControlStorable.LateRestoreFromJSON(animationControlJson);
					//}

					var animationReceiverStorable = animationAtom.GetStorableByID("AnimatedObject");
					var animationReceiverJson = animationReceiverStorable.GetJSON();
					if (animationReceiverJson["receiver"] == null) animationReceiverJson.Add("receiver", new JSONNode());
					animationReceiverJson["receiver"] = targetAtomName + ":" + targetControllerName;
					animationReceiverStorable.RestoreFromJSON(animationReceiverJson);

					animationAtom.transform.position = controller.transform.position;// + new Vector3(0.5f, 0.5f, 0.5f);
					animationAtom.transform.rotation = controller.transform.rotation;
					animationAtom.mainController.currentPositionState = FreeControllerV3.PositionState.ParentLink;
					animationAtom.mainController.currentRotationState = FreeControllerV3.RotationState.ParentLink;

					AddChildAnimationStep(newAnimationName, 1, controller.transform.position, controller.transform.rotation);
					AddChildAnimationStep(newAnimationName, 2, controller.transform.position + new Vector3(0, 0, 0.1f), controller.transform.rotation);
					SelectAtom(animationAtom);
					//if (picker != null) RefreshMannequinPicker(picker);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			_context.StartCoroutine(_context.CreateAtom("AnimationPattern", newAnimationName, controller.transform.position + new Vector3(0.5f, 0.5f, 0.5f), controller.transform.rotation, onAnimationPatternCreatedCallback));
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
					//animationStep.Restore(animationStepAsJson);
					animationStep.RestoreFromJSON(animationStepAsJson);
					//animationStep.LateRestoreFromJSON(animationStepAsJson);

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

		private void SelectAtom(Atom animationAtom)
		{
			SuperController.singleton.SelectController(animationAtom.mainController);
		}


		private void MannequinSelectAtom(string atomName, DynamicMannequinPicker picker)
		{
			if (atomName == null) return;
			picker.SelectedAtomName = atomName;
			picker.AtomMiniLabel.buttonText.text = atomName;
			var atom = _context.GetAtomById(atomName);
			var controllerNames = _context.GetControllerNamesForAtom(atom); //atom?.GetComponentsInChildren<FreeControllerV3>().Select(c => c.name).ToList() ?? new List<string>();
			var selectedController = SuperController.singleton.GetSelectedController();
			if (selectedController == null || selectedController.containingAtom.name != atomName) selectedController = atom.mainController;
			picker.SelectedControllerName = selectedController.name;
			picker.PointSelector.items = controllerNames;
			picker.PointSelector.Refresh();
			MannequinSelectController(selectedController.name, picker);
		}

		private void MannequinSelectController(string controllerName, DynamicMannequinPicker picker)
		{
			picker.SelectedControllerName = controllerName;
			picker.ControllerMiniLabel.buttonText.text = controllerName;
			RefreshMannequinPicker(picker); // ...on select controller
			var selectedJoint = picker.JointPoints.FirstOrDefault(j => j.controllerName == controllerName);
			picker.SelectedJoint = selectedJoint;
			picker.PointSelector.selectedOption.buttonText.text = controllerName;
			SelectMannequinJoint(selectedJoint, picker);
		}

		private void RefreshMannequinPicker(DynamicMannequinPicker picker)
		{
			if (picker.SelectedAtomName == null) return;
			var atom = SuperController.singleton.GetAtomByUid(picker.SelectedAtomName);

			//var controllerNames = defaultAtom?.GetComponentsInChildren<FreeControllerV3>().Select(c => c.name).ToList() ?? new List<string>();
			RefreshMannequinOverlays(atom, picker);
			RefreshMannequinControlPoints(picker);  // ... On Refresh
			
			var selectedController = SuperController.singleton.GetSelectedController();
			var selectController = (selectedController.containingAtom.name != atom.name) ? atom.mainController : selectedController;
			//SuperController.singleton.SelectController(selectController);

			var selecteddJoint = picker.JointPoints.FirstOrDefault(jp => jp.controllerName == selectController.name);
			SelectMannequinJoint(selecteddJoint, picker);
			picker.AtomSelector.selectedOption.buttonText.text = picker.SelectedAtomName;
		}

		private void RefreshMannequinOverlays(Atom atom, DynamicMannequinPicker picker)
		{
			//if (picker.MannequinOverlay != null) Destroy(picker.MannequinOverlay);
			Texture2D texture;
			float width = 200;
			float height = 200;
			float left = 120;
			float top = 200;
			float miniWidth = 80;
			float miniHeight = 80;
			float miniLeft = 80;
			float miniTop = 220;
			if (atom.type == "Person")
			{
				texture = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/Person1.png");
				width = 256;
				height = 510;
				left = 118;
				top = 50;
				miniWidth = 55;
				miniHeight = 110;
				miniLeft = 80;
				miniTop = 215;
			}
			else if (atom.type == "AnimationPattern" || atom.type == "AnimationStep")
			{
				texture = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/ActionMannequin.png");
			}
			else if (atom.type == "CollisionTrigger")
			{
				texture = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/TriggerMannequin.png");
			}
			else if (atom.type == "WindowCamera" || atom.type == "[CameraRig]" || atom.type == "[CameraRig]")
			{
				texture = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/CameraMannequin.png");
			}
			else if (atom.type == "InvisibleLight")
			{
				texture = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/LightMannequin3.png");
			}
			else
			{
				texture = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/CubeMannequin.png");
			}
			var imageComponent = picker.MannequinOverlay.GetComponent<Image>();
			var sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
			imageComponent.sprite = sprite;
			var rectTransform = picker.MannequinOverlay.GetComponents<RectTransform>().First();
			rectTransform.sizeDelta = new Vector2(width, height);
			var localLeft = left;
			var localTop = top;
			picker.MannequinOverlay.transform.localPosition = new Vector3(localLeft, localTop, picker.MiniOverlay.transform.localPosition.z);

			var miniImageComponent = picker.MiniOverlay.GetComponent<Image>();
			var miniSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
			miniImageComponent.sprite = miniSprite;
			rectTransform = picker.MiniOverlay.GetComponents<RectTransform>().First();
			rectTransform.sizeDelta = new Vector2(miniWidth, miniHeight);
			localLeft = miniLeft;
			localTop = miniTop;
			picker.MiniOverlay.transform.localPosition = new Vector3(localLeft, localTop, picker.MiniOverlay.transform.localPosition.z);
		}

		private void CloseMannequinPicker(DynamicMannequinPicker picker)
		{
			picker.AtomSelector.ClearDropdownList(_context);
			picker.PointSelector.ClearDropdownList(_context);
			picker.PointActionSelector.ClearDropdownList(_context);
			picker.SlaveLinkedAtomSelector.ClearDropdownList(_context);
			picker.AddFeatureSelector.ClearDropdownList(_context);

			_context.RemoveButton(picker.CloseButton);
			_context.RemoveButton(picker.MinimizeButton);
			_context.RemoveButton(picker.RefreshButton);

			_context.RemoveButton(picker.ButtonSelectionHalo);

			_context.RemoveButton(picker.ControllerMiniLabel);
			_context.RemoveButton(picker.AtomMiniLabel);
			_context.RemoveButton(picker.MiniOverlay);

			MVRScript.Destroy(picker.MannequinOverlay);
			MVRScript.Destroy(picker.BackPanel);
			MVRScript.Destroy(picker.Window);

		}
		//private void MannequinSelectAtom(string atomName)
		//{
		//	_dynamicMannequinSelectedPerson = atomName;
		//	var atom = SuperController.singleton.GetAtomByUid(atomName);
		//	SuperController.singleton.SelectController(atom.mainController, true);
		//}

		//private void RefreshMannequinButtons()
		//{
		//	try
		//	{
		//		var atom = SuperController.singleton.GetSelectedAtom();
		//		foreach (var button in _dynamicMannequinButtons)
		//		{
		//			var controllerName = button.tag;
		//			var controller = atom.GetComponents<FreeControllerV3>().SingleOrDefault(c => c.name == controllerName);
		//			//var color = if (controller.controlMode == FreeControllerV3.ControlMode.Off)
		//			var color = (controller.controlsOn) ? Color.gray : Color.red;
		//			if (controller.controlsOn && controller.linkToRB != null) color = Color.blue;
		//			button.buttonColor = color;
		//		}
		//	}
		//	catch (Exception e)
		//	{
		//		SuperController.LogError(e.ToString());
		//	}


		private void RefreshMannequinControlPoints(DynamicMannequinPicker picker)
		{

			if (picker.Minimized) return;
			var atom = _context.GetAtomById(picker.SelectedAtomName);
			if (atom == null) return;
			foreach (var jointPoint in picker.JointPoints)
			{
				_context.RemoveButton(jointPoint.positionButton);
				_context.RemoveButton(jointPoint.rotationButton);
			}
			picker.JointPoints = new List<DynamicJointPoint>();
			if (atom.type != "Person")
			{
				picker.JointPoints.Add(AddMannequinPointer(110, -210, "control", atom, picker));
				return;
			}
			picker.JointPoints.Add(AddMannequinPointer(25, -270, "control", atom, picker));
			JSONStorable geometry = atom.GetStorableByID("geometry");

			DAZCharacterSelector character = geometry as DAZCharacterSelector;

			picker.JointPoints.Add(AddMannequinPointer(108, -270, "headControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(108, -220, "neckControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(108, -170, "chestControl", atom, picker));

			if (character.gender == DAZCharacterSelector.Gender.Female)
			{
				picker.JointPoints.Add(AddMannequinPointer(80, -150, "rNippleControl", atom, picker));
				picker.JointPoints.Add(AddMannequinPointer(135, -150, "lNippleControl", atom, picker));
			}

			picker.JointPoints.Add(AddMannequinPointer(108, -140, "abdomen2Control", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(108, -110, "abdomenControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(108, -80, "hipControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(108, -50, "pelvisControl", atom, picker));

			if (character.gender == DAZCharacterSelector.Gender.Male)
			{
				picker.JointPoints.Add(AddMannequinPointer(108, -20, "testesControl", atom, picker));
				picker.JointPoints.Add(AddMannequinPointer(108, 10, "penisBaseControl", atom, picker));
				picker.JointPoints.Add(AddMannequinPointer(108, 40, "penisMidControl", atom, picker));
				picker.JointPoints.Add(AddMannequinPointer(108, 70, "penisTipControl", atom, picker));
			}

			picker.JointPoints.Add(AddMannequinPointer(70, -210, "rShoulderControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(148, -210, "lShoulderControl", atom, picker));

			picker.JointPoints.Add(AddMannequinPointer(40, -180, "rArmControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(178, -180, "lArmControl", atom, picker));

			picker.JointPoints.Add(AddMannequinPointer(33, -110, "rElbowControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(183, -110, "lElbowControl", atom, picker));

			picker.JointPoints.Add(AddMannequinPointer(33, -50, "rHandControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(183, -50, "lHandControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(80, -10, "rThighControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(135, -10, "lThighControl", atom, picker));

			picker.JointPoints.Add(AddMannequinPointer(80, 70, "rKneeControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(135, 70, "lKneeControl", atom, picker));

			picker.JointPoints.Add(AddMannequinPointer(80, 140, "rFootControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(135, 140, "lFootControl", atom, picker));

			picker.JointPoints.Add(AddMannequinPointer(80, 170, "rToeControl", atom, picker));
			picker.JointPoints.Add(AddMannequinPointer(135, 170, "lToeControl", atom, picker));
		}

		private void SelectMannequinJoint(DynamicJointPoint joint, DynamicMannequinPicker picker)
		{
			try
			{
				if (joint == null) return;
				var selectedAtom = picker.SelectedAtomName;
				
				picker.SelectedJoint = joint;
				picker.ButtonSelectionHalo.transform.localPosition = picker.SelectedJoint.positionButton.transform.localPosition;
				picker.SelectedControllerName = joint.controllerName;
				picker.ControllerMiniLabel.buttonText.text = joint.controllerName;
				picker.PointSelector.selectedOption.buttonText.text = joint.controllerName;
				ExecuteAppropriateActionBasedOnSelectedPointAction(picker.SelectedPointAction, picker, joint.controllerName, selectedAtom);

				var links = GetLinkForController(joint.controllerName, selectedAtom);
				List<string> slaveLinks = new List<string>();
				List<string> masterLinks = new List<string>();
				foreach (var link in links)
				{
					bool isMaster = (link.SlaveAtom == joint.atomName && link.SlaveController == joint.controllerName);
					var finalLink = isMaster
						? link.MasterAtom + ":" + link.MasterController
						: link.SlaveAtom + ":" + link.SlaveController;
					if (isMaster) masterLinks.Add(finalLink); else slaveLinks.Add(finalLink);
				}
				picker.SlaveLinkedAtomSelector.SetItems(slaveLinks);
				picker.MasterLinkedAtomSelector.SetItems(masterLinks);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private DynamicJointPoint AddMannequinPointer(int x, int y, string controllerName, Atom atom, DynamicMannequinPicker picker)
		{
			var newDynamicJoint = new DynamicJointPoint();
			newDynamicJoint.controllerName = controllerName;
			newDynamicJoint.atomName = atom.name;
			UnityAction<string> userSelectsPointCallback = (inputControllerName) =>
			{
				SelectMannequinJoint(newDynamicJoint, picker);
			};
			var positionIcon = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/Spot.png");
			newDynamicJoint.positionButton = _parentWindow.CreateButton(picker.Window, "", 10, 10, x + 5, y + 5, new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1, 1f, 1, 1f), Color.clear, positionIcon);
			newDynamicJoint.positionButton.tag = controllerName;
			var rotationIcon = _context._imageLoaderService.GetFutureImageFromFileOrCached(  _context.GetPluginPath() + "/Resources/PointRotation2.png");
			newDynamicJoint.rotationButton = _parentWindow.CreateButton(picker.Window, "", 20, 20, x, y, new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1, 1f, 1, 1f), Color.clear, rotationIcon);
			newDynamicJoint.rotationButton.tag = controllerName;
			//newDynamicJoint.positionButton.button.onClick.AddListener(() => userSelectsPointCallback(controllerName));
			newDynamicJoint.rotationButton.button.onClick.AddListener(() => userSelectsPointCallback(controllerName));
			var controller = _context.GetControllerForAtom(controllerName, atom); //atom.GetComponentsInChildren<FreeControllerV3>().SingleOrDefault(c => c.name == controllerName);

			_context.SetTooltipForDynamicButton(newDynamicJoint.rotationButton, () =>
			{
				return controllerName + "\n(position: " + controller.currentPositionState + ", rotation: " + controller.currentRotationState + ")";
			});

			//_context.AddDragging(newDynamicJoint.rotationButton.gameObject, controller.gameObject);
			_context.UpdatePointerState(newDynamicJoint, controllerName, atom);
			return newDynamicJoint;
		}

		private List<ControllerLink> GetLinkForController(string searchingForController, string atomName)
		{
			List<ControllerLink> links = new List<ControllerLink>();
			var sceneJson = SuperController.singleton.GetSaveJSON();
			JSONArray atomsArray = sceneJson["atoms"].AsArray;
			//var atomsArray = GetSceneAtoms();
			for (int a = 0; a < atomsArray.Count; a++)
			{
				//JSONClass atomJson = atomsArray[a].GetJSON();
				JSONClass atomJson = atomsArray[a].AsObject;
				JSONArray atomStorables = atomJson["storables"]?.AsArray;
				if (atomStorables == null) continue;
				for (int s = 0; s < atomStorables.Count; s++)
				{
					JSONClass storable = atomStorables[s].AsObject;
					var mannequinAtom = atomName;
					var mannequinController = searchingForController;
					var scanningAtom = atomJson["id"].Value;
					var scanningController = storable["id"].Value;

					// Grab physics link...
					if (!string.IsNullOrEmpty(storable["linkTo"]?.Value))
					{
						string linkValue = storable["linkTo"].Value;
						var newLink = GetLinkObjectOrDefault(linkValue, atomName, searchingForController, scanningAtom, scanningController);
						if (newLink != null) links.Add(newLink);
					}
					// Grab animation links...
					if (scanningController == "AnimatedObject" && !string.IsNullOrEmpty(storable["receiver"]?.Value))
					{
						string linkValue = storable["receiver"].Value;
						if (IsParentAnimation(linkValue, mannequinAtom, mannequinController, scanningAtom, scanningController))
						{
							var newLink = GetAnimationLinkObjectOrDefault(mannequinAtom, mannequinController, scanningAtom, scanningController);
							links.Add(newLink);
							// Add pattern steps...
							var animationPattern = atomStorables.Childs.FirstOrDefault(c => c["id"].Value == "AnimationPattern");
							var stepNames = animationPattern["steps"].AsArray.Childs.Select(c => c.Value).ToList();
							var stepLinks = stepNames.Select(stepName => new ControllerLink()
							{
								MasterAtom = stepName,
								MasterController = "control",
								SlaveAtom = mannequinAtom,
								SlaveController = mannequinController
							}).ToList();
							links.AddRange(stepLinks);
						}
					}

				}
			}
			return links;
		}

		private List<ControllerLink> GetScenePhysicsLinks()
		{
			List<ControllerLink> links = new List<ControllerLink>();
			var sceneJson = SuperController.singleton.GetSaveJSON();
			JSONArray atomsArray = sceneJson["atoms"].AsArray;
			for (int a = 0; a < atomsArray.Count; a++)
			{
				JSONClass atomJson = atomsArray[a].AsObject;
				JSONArray atomStorables = atomJson["storables"]?.AsArray;
				if (atomStorables == null) continue;
				for (int s = 0; s < atomStorables.Count; s++)
				{
					JSONClass storable = atomStorables[s].AsObject;
					var scanningAtom = atomJson["id"].Value;
					var scanningController = storable["id"].Value;

					// Grab physics link...
					if (!string.IsNullOrEmpty(storable["linkTo"]?.Value))
					{	
						string masterLink = storable["linkTo"].Value;
						var linkToAtom = masterLink.Split(':').First();
						var linkToController = masterLink.Split(':').Last();
						var newLink = new ControllerLink(){
								SlaveAtom = scanningAtom, 
								SlaveController = scanningController, 
								MasterAtom = linkToAtom, 
								MasterController = linkToController
						};
						links.Add(newLink);
					}
				}
			}
			return links;
		}

		private bool IsParentAnimation(string linkValue, string focusedAtom, string focusedController, string atomWithLink, string controllerWithLink)
		{
			var linkedAtom = linkValue.Split(':').First();
			var linkedController = linkValue.Split(':').Last();
			return linkedAtom == focusedAtom && linkedController == focusedController;
		}
		private ControllerLink GetAnimationLinkObjectOrDefault(string focusedAtom, string focusedController, string atomWithLink, string controllerWithLink)
		{
			//...this is the animation object
			var newLink = new ControllerLink();
			newLink.SlaveAtom = focusedAtom;
			newLink.SlaveController = focusedController;
			newLink.MasterAtom = atomWithLink;
			newLink.MasterController = "control";
			return newLink;
		}

		private ControllerLink GetLinkObjectOrDefault(string linkValue, string focusedAtom, string focusedController, string atomWithLink, string controllerWithLink)
		{
			var linkedAtom = linkValue.Split(':').First();
			var linkedController = linkValue.Split(':').Last();

			// If this is an outgoing link, i.e. current atoms controller is controlled by another controller... 
			if (atomWithLink == focusedAtom && controllerWithLink == focusedController)
			{
				var newLink = new ControllerLink();
				newLink.SlaveAtom = focusedAtom;
				newLink.SlaveController = focusedController;
				newLink.MasterAtom = linkedAtom;
				newLink.MasterController = linkedController;
				return newLink;
			}

			// If this is an incoming link, i.e. current atom controls another controller..
			if (linkedAtom == focusedAtom && linkedController == focusedController)
			{
				var newLink = new ControllerLink();
				newLink.SlaveAtom = atomWithLink;
				newLink.SlaveController = controllerWithLink;
				newLink.MasterAtom = focusedAtom;
				newLink.MasterController = focusedController;
				return newLink;
			}

			return null;
		}

		private void ExecuteAppropriateActionBasedOnSelectedPointAction(string pointAction, DynamicMannequinPicker picker, string controllerName, string atomName)
		{
			switch (pointAction)
			{
				case POINT_ACTION_NONE:
					break;
				case POINT_ACTION_SELECT_CONTROLLER:
					_context.SelectController(controllerName, atomName);
					break;
				case POINT_ACTION_NEXT_MODE:
					_context.SelectNextControllerPositionMode(controllerName, atomName);
					_context.SelectNextControllerRotationMode(controllerName, atomName);
					RefreshMannequinControlPoints(picker); // ... On point click
					_context.SelectController(controllerName, atomName);
					break;
				case POINT_ACTION_NEXT_POSITION_MODE:
					_context.SelectNextControllerPositionMode(controllerName, atomName);
					RefreshMannequinControlPoints(picker); // ... On point click
					_context.SelectController(controllerName, atomName);
					break;
				case POINT_ACTION_NEXT_ROTATION_MODE:
					_context.SelectNextControllerRotationMode(controllerName, atomName);
					RefreshMannequinControlPoints(picker); // ... On point click
					_context.SelectController(controllerName, atomName);
					break;
				case POINT_ACTION_ON_OFF:
					string newState = _context.TogglePositionOnOff(controllerName, atomName);
					_context.ToggleRotationOnOff(controllerName, atomName, newState);
					RefreshMannequinControlPoints(picker); // ... On point click
					_context.SelectController(controllerName, atomName);
					break;
				case POINT_ACTION_POSITION_ON_OFF:
					_context.TogglePositionOnOff(controllerName, atomName);
					RefreshMannequinControlPoints(picker); // ... On point click
					_context.SelectController(controllerName, atomName);
					break;
				case POINT_ACTION_ROTATION_ON_OFF:
					_context.ToggleRotationOnOff(controllerName, atomName);
					RefreshMannequinControlPoints(picker); // ... On point click
					_context.SelectController(controllerName, atomName);
					break;
				default:
					break;//...POINT_ACTION_NONE
			}


		}

	}
}
