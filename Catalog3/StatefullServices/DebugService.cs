using juniperD.Models;
using juniperD.Services;
using juniperD.Utils;
using MVR.FileManagementSecure;
using PrefabEvolution;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using static SuperController;

namespace juniperD.StatefullServices
{
	public class DebugService
	{
		public CatalogPlugin _context;
		private bool _testToggle = false;
		private GameObject _cylinder;
		// Experimental...
		List<Vector3> _vertexCache = new List<Vector3>();

		public void Init(CatalogPlugin context)
		{
			_context = context;

			_context.CreateButton("DEBUG: rotate Atom using Matices").button.onClick.AddListener(() =>
			{
				try
				{
					var pivotController = SuperController.singleton.GetAtomByUid("Cube#3").mainController;

					var selectedController = SuperController.singleton.GetSelectedAtom().mainController;

					var positionToRotatAround = pivotController.transform.position;

					var newPosition = _context._mutationsService.RotateAroundXAxis(1f, positionToRotatAround);
					selectedController.transform.position = newPosition;

				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Copy anims 'Cube' 2 'Cube#2'").button.onClick.AddListener(() =>
			{
				try
				{
					var sourceAtom = "Cube";
					var targetAtom = "Cube#2";
					var animations = _context._mutationsService.GetSceneAnimations();
					foreach (var animation in animations.Where(a => a.SlaveAtom == sourceAtom).ToList())
					{
						_context._mutationsService.AddAnimationToController(animation, targetAtom, "control");
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Capture animations").button.onClick.AddListener(() =>
			{
				try
				{
					var links = _context._mutationsService.GetSceneAnimations();
					// Log output...
					string output = links
						.Select(l => 
							$"{l.Name} \n   for {l.SlaveAtom}:{l.SlaveController} " 
							+ $"\n   steps " + string.Join(",", 
									l.AnimationSteps.Select(s => ""
									+ $"\n   step: " + s.Name
									+ $"\n      position: {s.StepPosition.x},{s.StepPosition.y},{s.StepPosition.z}"
									+ $"\n      rotation: {s.StepRotation.x},{s.StepRotation.y},{s.StepRotation.z}"
									).ToArray()
								) 
							+ $"\n   slave atom position: " + $"{l.SlaveAtomPosition.x}, {l.SlaveAtomPosition.y}, {l.SlaveAtomPosition.z}" 
							+ $"\n   slave atom rotation: {l.SlaveAtomRotation.x}, {l.SlaveAtomRotation.y}, {l.SlaveAtomRotation.z}"
							+ $"\n   slave controller position: " + $"{l.SlaveControllerPosition.x}, {l.SlaveControllerPosition.y}, {l.SlaveControllerPosition.z}"
							+ $"\n   slave controller rotation: {l.SlaveControllerRotation.x}, {l.SlaveControllerRotation.y}, {l.SlaveControllerRotation.z}"
							)
						.Aggregate((a, b) => a + '\n' + b);
					_context.DebugLog(output);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Morph Value").button.onClick.AddListener(() =>
			{
				try
				{
					var morphs = _context._mutationsService.GetMorphsForSelectedPersonOrDefault();
					var m = morphs.FirstOrDefault(mo => mo.displayName == "AAsex_browhiAA1");
					_context.DebugLog($"Morph Value ({m.uid}): " + m.morphValue);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG Apply preset").button.onClick.AddListener(() =>
			{
				try 
				{ 
					SuperController.LogMessage("1");
					var selectedAtom = SuperController.singleton.GetSelectedAtom();

					var presetFile = "/Custom/Clothing/Female/Builtin/Sweet/SweetTank_Zombies make better boyfriends.vap";
					//-----------------------------------------------
					//JSONStorable posePreset = selectedAtom.GetStorableByID("PosePresets");
					//JSONStorableUrl presetPathJSON = posePreset.GetUrlJSONParam("presetBrowsePath");
					//presetPathJSON.val = SuperController.singleton.NormalizePath(presetFile);
					//posePreset.CallAction("LoadPreset");
					//-----------------------------------------------
					SuperController.LogMessage("2");
					JSONStorable clothingPreset = selectedAtom.GetStorableByID("ClothingPresets");

					//foreach (var param in clothingPreset.GetAllParamAndActionNames())
					//{
					//	SuperController.LogMessage("Param(all): " + param + ": " + clothingPreset.GetStringParamValue(param));
					//}

					foreach (var param in clothingPreset.GetStringParamNames())
					{
						SuperController.LogMessage("Param(str): " + param + ": " + clothingPreset.GetStringParamValue(param));
					}
					foreach (var param in clothingPreset.GetUrlParamNames())
					{
						SuperController.LogMessage("Param(url): " + param + ": " + clothingPreset.GetUrlParamValue(param));
					}
					SuperController.LogMessage("3");
					//SuperController.LogMessage("PRESETS " + clothingPreset.GetJSON().ToString());
					//JSONStorableUrl clothingPresetPath = clothingPreset.GetUrlJSONParam("presetBrowsePath");
					clothingPreset.GetUrlJSONParam("presetBrowsePath").val = SuperController.singleton.NormalizePath(presetFile);
					SuperController.LogMessage("4");
					clothingPreset.GetStringJSONParam("presetName").val = "Zombies make better boyfriends";
					SuperController.LogMessage("5");
					//clothingPreset.GetUrlJSONParam("StorePresetWithName").val = "Zombies make better boyfriends";
					//clothingPreset.GetUrlJSONParam("LoadPresetWithName").val = "Zombies make better boyfriends";

					//clothingPreset.SetUrlParamValue("presetBrowsePath", SuperController.singleton.NormalizePath(presetFile));
					//clothingPreset.SetUrlParamValue("presetName", "Zombies make better boyfriends");
					//clothingPreset.SetUrlParamValue("StorePresetWithName", "Zombies make better boyfriends");
					//clothingPreset.SetUrlParamValue("LoadPresetWithName", "Zombies make better boyfriends");

					//clothingPreset.GetPresetFilePathAction("LoadPresetWithName").actionCallback = (path) => {};
					//clothingPreset.CallAction("LoadPresetWithName", "Zombies make better boyfriends");
					//SuperController.LogMessage("appearancePresetPath " + SuperController.singleton.NormalizePath(presetFile));
					//clothingPresetPath.val = SuperController.singleton.NormalizePath(presetFile);
					clothingPreset.CallAction("LoadPreset");
					SuperController.LogMessage("6");

					JSONClass atomsJSON = SuperController.singleton.GetSaveJSON(selectedAtom);
					SuperController.LogMessage("7");
					JSONArray atomsArrayJSON = atomsJSON["atoms"].AsArray;
					SuperController.LogMessage("8");
					JSONClass atomJSON = atomsArrayJSON.Childs.First().AsObject;
					SuperController.LogMessage("9");
					JSONArray atomStorables = atomJSON["storables"]?.AsArray;
					//var atomStorables = selectedAtom.GetStorableIDs();
					SuperController.LogMessage("10");
					JSONNode presetJSON = SuperController.singleton.LoadJSON(presetFile);
					SuperController.LogMessage("11: " + presetJSON.ToString()?? "NULL");
					JSONArray presetStorables = presetJSON.AsObject["storables"]?.AsArray;
					SuperController.LogMessage("12");
					for (int p = 0; p< presetStorables.Count; p++)
					{
						for (int a = 0; a < atomStorables.Count; a++)
						{
							var presetStorableId = presetStorables[p]["id"] + "";
							var atomStorableId = atomStorables[a]["id"] + "";
							SuperController.LogMessage("Compare: " + presetStorableId + "=" + atomStorableId);
							_context._mainWindow.TextDebugPanelText.text += "\n" + presetStorableId + "=" + atomStorableId;
							if (presetStorableId == atomStorableId)
							{
								var storable = selectedAtom.GetStorableByID("presetStorableId");
								storable.RestoreFromJSON(presetStorables[p].AsObject);
								storable.LateRestoreFromJSON(presetStorables[p].AsObject);
								_context._mainWindow.TextDebugPanelText.text += "\n-----------------\n";
								_context._mainWindow.TextDebugPanelText.text += presetStorables[p].AsObject.ToString();
								//atomJSON["storables"][a] = presetStorables[p];
								break;
							}
							atomJSON["storables"].Add(presetStorables[p]);
						}
					}

					selectedAtom.PreRestore();
					selectedAtom.RestoreTransform(atomJSON);
					selectedAtom.Restore(atomJSON);
					selectedAtom.LateRestore(atomJSON);
					selectedAtom.PostRestore();
						//clothingPreset.isPresetRestore = true;
						//clothingPreset.PreRestore();
						//clothingPreset.PostRestore();
						//-----------------------------------------------
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG show info for clothing items").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					JSONStorable geometry = atom.GetStorableByID("geometry");
					DAZCharacterSelector character = geometry as DAZCharacterSelector;
					foreach (var item in character.clothingItems)
					{
						SuperController.LogMessage("Clothing item: " + item.uid);
						item.RefreshClothingItems();
						item.PostLoadJSONRestore();
						//foreach (var materialName in item.skin.materialNames)
						//{
						//	SuperController.LogMessage("    skin: " + materialName);
						//}
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG show presets from file").button.onClick.AddListener(() =>
			{
				try
				{
					_context._mainWindow.TextDebugPanelText.UItext.text = "";
					string[] files = FileManagerSecure.GetFiles("Custom\\Atom\\Person\\Clothing", "*.vap");
					foreach (var file in files)
					{
						_context._mainWindow.TextDebugPanelText.UItext.text += "\n" + file;
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG show presets").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					if (atom == null) _context.ShowPopupMessage("Please select atom");

					var atomStorables = atom.GetStorableIDs();
					_context._mainWindow.TextDebugPanelText.UItext.text = "";
					foreach (var storableId in atom.GetStorableIDs())
					{
						if (storableId.EndsWith("Preset"))
						{
							var json = atom.GetStorableByID(storableId).GetJSON();
							_context._mainWindow.TextDebugPanelText.UItext.text += "\n" +  json.ToString();
						}
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Log Atom").button.onClick.AddListener(() =>
			{
				SuperController.LogMessage("Test");

				try
				{
					var selectedAtom = SuperController.singleton.GetSelectedAtom();
					JSONStorable geometry = selectedAtom.GetStorableByID("geometry");
					DAZCharacterSelector character = geometry as DAZCharacterSelector;
					foreach (var charac in character.characters)
					{
						SuperController.LogMessage("Character: " + charac.displayName);
					}
					//var selectedAtom = SuperController.singleton.GetSelectedAtom();
					//JSONClass atomsJSON = SuperController.singleton.GetSaveJSON(selectedAtom);
					//JSONArray atomsArrayJSON = atomsJSON["atoms"].AsArray;
					//JSONClass atomJSON = atomsArrayJSON.Childs.First().AsObject;
					//SuperController.LogMessage("AtomJson: " + atomJSON.ToString());
					//_context._mainWindow.TextDebugPanel.text = atomJSON.ToString();
					//var path = $"{_context.GetSceneDirectoryPath()}/{selectedAtom.name}-SaveJSON.json";
					//SuperController.LogMessage("SavbePath: " + path);
					//SuperController.singleton.SaveStringIntoFile(path, atomJSON.ToString());
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG show serialized storables").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					var atomStorables = atom.GetStorableIDs();
					_context._mainWindow.TextDebugPanelText.UItext.text = "";
					foreach (var storableId in atom.GetStorableIDs())
					{
						var storableValue = atom.GetStorableByID(storableId)?.GetJSON()?.ToString() ?? "";
						if (storableValue.Length > 100)
							_context._mainWindow.TextDebugPanelText.UItext.text += "\n" + storableId + ": " + storableValue.Substring(0, 50);
						else
							_context._mainWindow.TextDebugPanelText.UItext.text += "\n" + storableId + ": " + storableValue;
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG List clothing items").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					JSONStorable geometry = atom.GetStorableByID("geometry");
					DAZCharacterSelector character = geometry as DAZCharacterSelector;
					foreach (var item in character.clothingItems)
					{
						SuperController.LogMessage("Clothing item: " + item.uid);
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Register new Storable").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					var newStorable = new JSONStorable();
					newStorable.containingAtom = atom;

					atom.RegisterBool(new JSONStorableBool("Test", true));
					//var newStorableBool = 
					//newStorable.overrideId = "bob";
					//newStorable.RegisterBool(newStorableBool);
					var jClass = new JSONClass();
					jClass["id"] = "bob";
					jClass["testprop"] = "testval";
					//newStorable.RestoreFromJSON(jClass);
					//newStorable.containingAtom = atom;
					//newStorable.name = "Bob";
					_context.StartCoroutine(AfterRestoring(atom, newStorable));
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Show Atom Storables").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					var atomStorables = atom.GetStorableIDs();
					SuperController.LogMessage("Has geometry: " + atomStorables.Any(s => s == "geometry"));
					foreach (var storableId in atom.GetStorableIDs())
					{
						SuperController.LogMessage("Storable: " + storableId);
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Show geometry Storable").button.onClick.AddListener(() =>
			{
				try
				{
					var atom = SuperController.singleton.GetSelectedAtom();
					var atomStorables = atom.GetStorableIDs();
					//SuperController.LogMessage("Has geometry: " + atomStorables.Any(s => s == "geometry"));
					foreach (var storableId in atom.GetStorableIDs().Where(s => s == "geometry"))
					{
						var storable = atom.GetStorableByID(storableId);
						SuperController.LogMessage("StorableId: " + storableId);
						var json = storable.GetJSON();
						SuperController.LogMessage("JSON: " + json.ToString());
						SuperController.LogMessage("character: " + storable.GetStringParamValue("character"));
						//SuperController.LogMessage("clothing: " + storable.GetClot("clothing"));
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Delete File").button.onClick.AddListener(() =>
			{
				try
				{
					
					FileManagerSecure.DeleteFile("Saved");
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Show Plugin name").button.onClick.AddListener(() =>
			{
				try
				{
					SuperController.LogMessage(_context.name);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Show Plugin Path").button.onClick.AddListener(() =>
			{
				try
				{
					SuperController.LogMessage(SuperController.singleton.currentLoadDir);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			
			_context.CreateButton("DEBUG Create icon panel").button.onClick.AddListener(() =>
			{
				try
				{
					var imagepath = _context.GetPluginPath() + "/Resources/Move2.png";
					UnityAction<Texture2D> imageLoadedCallback = (texture) => {
						SuperController.LogMessage("Image loaded");
						var uiHelper = new CatalogUiHelper(_context);
						uiHelper.CreateImagePanel(_context._mainWindow.ParentWindowContainer, texture, 32, 32, 50, 50);
					};
					ImageLoader.LoadImage(imagepath, imageLoadedCallback);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Create icon panel 2").button.onClick.AddListener(() =>
			{
				try
				{
					var imagepath = _context.GetPluginPath() + "/Resources/Move2.png";
					//var texture = Helpers.LoadImage(imagepath);
					var uiHelper = new CatalogUiHelper(_context);
					//uiHelper.CreateImagePanel(_context._mainWindow.ParentWindowContainer, texture, 32, 32, 50, 50);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			Texture2D _textureX = null;
			_context.CreateButton("DEBUG Shift texture left").button.onClick.AddListener(() =>
			{
				try
				{
					var uiHelper = new CatalogUiHelper(_context);
					uiHelper.ShiftTextureLeft(_textureX, 1);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Unpause comply").button.onClick.AddListener(() =>
			{
				try
				{
					var mainController = _context.containingAtom.mainController;
					//mainController.PauseComply();
					//var allJoints = _context.containingAtom.GetComponentsInChildren<ConfigurableJoint>();
					//foreach (var joint in allJoints)
					//{

					//	//joint.transform.position = joint.transform.position + new Vector3(0.5f, 0.5f, 0.5f);
					//}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			//private IEnumerator RestoreAtomPreset(string atomName, string targetAtomName)
			//{
			//	yield return new WaitForEndOfFrame();

			//	//Atom atom = SuperController.singleton.GetAtomByUid(atomName);
			//	//JSONClass atomsJSON = SuperController.singleton.GetSaveJSON(atom);
			//	//JSONArray atomsArrayJSON = atomsJSON["atoms"].AsArray;
			//	//JSONClass atomJSON = (JSONClass)atomsArrayJSON[0];

			//	//if (_parentLinkToTarget && _targetName.StartsWith("Last"))
			//	//{
			//	//	string targetNode = _targetNode;
			//	//	if (_targetNode == "Lips" || _targetNode == "Mouth") targetNode = "head";
			//	//	else if (_targetNode == "Labia" || _targetNode == "Vagina") targetNode = "pelvis";
			//	//	Atom targetAtom = SuperController.singleton.GetAtomByUid(targetAtomName);
			//	//	atom.mainController.SetLinkToAtom(targetAtomName);
			//	//	Rigidbody linkRB = targetAtom.rigidbodies.First(rb => rb.name == targetNode);
			//	//	atom.mainController.linkToRB = linkRB;
			//	//	atom.mainController.currentPositionState = FreeControllerV3.PositionState.ParentLink;
			//	//	atom.mainController.currentRotationState = FreeControllerV3.RotationState.ParentLink;
			//	//}
			//	//int maxPlugins = 100;
			//	//List<string> test = new List<string>();
			//	//test.Add("Temp");
			//	//PatreonFeatures.GridLayoutFeature(test);
			//	//if (!Constants._patreonContentEnabled || test == null || test.Count != 7)
			//	//{
			//	//	maxPlugins = 1;
			//	//}

			//	//if (_atomType == "Person")
			//	//{
			//	//	JSONStorable js = atom.GetStorableByID("PosePresets");
			//	//	JSONStorableUrl presetPathJSON = js.GetUrlJSONParam("presetBrowsePath");
			//	//	if (_selectedPreset3File != "")
			//	//	{
			//	//		presetPathJSON.val = "";
			//	//		presetPathJSON.val = SuperController.singleton.NormalizePath(_selectedPreset3File);
			//	//		js.CallAction("LoadPreset");
			//	//	}
			//	//	js = atom.GetStorableByID("AppearancePresets");
			//	//	presetPathJSON = js.GetUrlJSONParam("presetBrowsePath");
			//	//	presetPathJSON.val = "";
			//	//	if (_selectedPreset1File != "")
			//	//	{
			//	//		presetPathJSON.val = SuperController.singleton.NormalizePath(_selectedPreset1File);
			//	//		js.CallAction("LoadPreset");
			//	//	}
			//	//}

			//	//LoadPlugins(atom, maxPlugins);
			//	//_mvrScript._atomSpawningActive = false;
			//	//SuperController.singleton.helpHUDText.text = "";
			//}

			_context.CreateButton("DEBUG show paths").button.onClick.AddListener(() =>
			{
				try
				{
					SuperController.LogMessage("----------------");
					SuperController.LogMessage("GetSceneDirectoryPath: " + _context.GetSceneDirectoryPath());
					SuperController.LogMessage("----------------");
					SuperController.LogMessage("dataPath: " + Application.dataPath);
					SuperController.LogMessage("----------------");
					SuperController.LogMessage("persistentDataPath: " + Application.persistentDataPath);
					SuperController.LogMessage("----------------");
					SuperController.LogMessage("streamingAssetsPath: " + Application.streamingAssetsPath);
					SuperController.LogMessage("----------------");
					SuperController.LogMessage("currentLoadDir: " + SuperController.singleton.currentLoadDir);
					SuperController.LogMessage("----------------");
					//SuperController.LogMessage("NormalizeLoadPath: " + _context.containingAtom.masterController.pos);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Capture animation").button.onClick.AddListener(() =>
			{
				try
				{
					//var animation = SuperController.singleton.GetAllAnimationSteps();
					//var box_FreeControllerV3 = _context.containingAtom.GetComponentInChildren<FreeControllerV3>();
					//_context.containingAtom.animationPatterns

					SuperController.LogMessage($"containingAtom Animation step count: " + _context.containingAtom.animationPatterns.Count());
					SuperController.LogMessage($"containingAtom Animation pattern count: " + _context.containingAtom.animationSteps.Count());

					SuperController.LogMessage($"SuperController Animation step count: " + SuperController.singleton.GetAllAnimationSteps().Count());
					SuperController.LogMessage($"SuperController Animation pattern count: " + SuperController.singleton.GetAllAnimationPatterns().Count());

					//foreach (var step in animation)
					//{
					//	SuperController.LogMessage($"Step: " + step.point.position.x);
					//}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG Log Components Tree").button.onClick.AddListener(() =>
			{
				try
				{
					//var components = SuperController.singleton.GetComponentsInChildren<Component>();
					//var componentTreeLines = components.SelectMany(c => GetComponentsTree(c));
					var componentTreeLines = GetComponentsTree(_context.containingAtom);
					var fileData = componentTreeLines.Aggregate((a, b) => $"{a}\n{b}");
					var path = $"{_context.GetSceneDirectoryPath()}/ComponentTree.txt";
					SuperController.singleton.SaveStringIntoFile(path, fileData);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});


			_context.CreateButton("DEBUG Try change preset").button.onClick.AddListener(() =>
			{

				//SuperController.LogMessage("dataPath: " + Application.dataPath);
				//SuperController.LogMessage("persistentDataPath: " + Application.persistentDataPath);
				//Atom atom = new Atom();
				//var presetNames = atom.GetPresetFilePathActionNames();
				//foreach (var preset in presetNames)
				//{
				//	SuperController.LogMessage("preset: " + preset);
				//}
				//SuperController.singleton.AddAtom()
				//SuperController.LogMessage("Atom Name: " + containingAtom.name);
				var storables = _context.containingAtom.GetStorableIDs();
				foreach (var storable in storables.Where(id => id == "SweetTankPreset").ToList())
				{
					var storableX = _context.containingAtom.GetStorableByID(storable);
					SuperController.LogMessage(">> storableX: " + storableX);
					foreach (var paramName in storableX.GetStringParamNames())
					{
						var paramValue = storableX.GetStringParamValue(paramName);
						var paramType = storableX.GetParamOrActionType(paramName);
						SuperController.LogMessage(">> paramValue: " + paramValue);
						SuperController.LogMessage(">> paramType: " + paramType);
					};
					//SuperController.LogMessage(">> storable: " + storable);
					//SuperController.LogMessage(" >> GetParamOrActionType: " + storableX.GetParamOrActionType("presetName"));
					//SuperController.LogMessage(" >> GetPresetFilePathAction: " + storableX.GetPresetFilePathAction("presetName"));
					//storableX.GetPresetFilePathActionNames().ToList().ForEach(i => SuperController.LogMessage(" >> GetPresetFilePathActionName: " + i));
					//storableX.GetStringParamNames().ToList().ForEach(i => SuperController.LogMessage(" >> GetStringParamName: " + i));
				}
				//atom.RestoreFromStore1();
			});

			_context.CreateButton("DEBUG  Try change preset 2").button.onClick.AddListener(() =>
			{
				JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
				DAZCharacterSelector character = geometry as DAZCharacterSelector;

				_context.containingAtom.GetStorableByID("SweetTankPreset").SetStringParamValue("presetName", "Red White");
				_context.containingAtom.GetStorableByID("TankTopMaterial").SetStringParamValue("Main Texture", "Cow");

				//containingAtom.Late
				var clothingItems = character.clothingItems.Where(i => i.active).ToList();//character.GetClothingItem()
				foreach (var item in clothingItems)
				{
					item.RefreshClothingItems();
					//character.SetActiveClothingItem(item, true);
				}
				//containingAtom.GetStorableByID("SweetTankPreset").SaveToStore1();
				//containingAtom.GetStorableByID("TankTopMaterial").SaveToStore1();
				//containingAtom.GetStorableByID("SweetTankPreset").SetStringParamValue("presetName", "Zombies make better boyfriends");
				//containingAtom.GetStorableByID("TankTopMaterial").SetStringParamValue("Main Texture", "Zombies");

				//containingAtom.PostRestore();
				//containingAtom.LateRestore();
				//containingAtom.RestoreFromStore1();//.GetStorableByID("SweetTankPreset"));
				//containingAtom.GetStorableByID("TankTopMaterial").PostRestore();
				SuperController.LogMessage(">> Changed item " + _context.containingAtom.GetStorableByID("SweetTankPreset").GetStringParamValue("presetName"));
				//containingAtom.RestoreFromLast(containingAtom.GetStorableByID("SweetTankPreset"));
			});

			_context.CreateButton("DEBUG Log Components Tree").button.onClick.AddListener(() =>
			{
				try
				{
					//var components = SuperController.singleton.GetComponentsInChildren<Component>();
					//var componentTreeLines = components.SelectMany(c => GetComponentsTree(c));
					var componentTreeLines = GetComponentsTree(_context.containingAtom);
					var fileData = componentTreeLines.Aggregate((a, b) => $"{a}\n{b}");
					var path = $"{_context.GetSceneDirectoryPath()}/ComponentTree.txt";
					SuperController.singleton.SaveStringIntoFile(path, fileData);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			});

			_context.CreateButton("DEBUG 2 Explosion limiter").button.onClick.AddListener(() =>
			{
				foreach (ForceReceiver fr in _context.containingAtom.forceReceivers)
				{
					//SuperController.LogMessage($"fr: " + fr.name);
					Rigidbody rb = fr.GetComponent<Rigidbody>();
					//var mesh = rb.GetComponentsInChildren<MeshFilter>();
					//if (mesh.Count() > 0) SuperController.LogMessage($"got mesh: " + mesh.Count());
					//if (mesh != null) SuperController.LogMessage($"got mesh: " + mesh.name);
					if (rb != null)
					{
						Vector3 newVelocity = rb.velocity;
						for (int i = 0; i < 2; i++)
						{
							//if (Mathf.Abs(rb.velocity[i]) > 7f)
							newVelocity[i] = 1;
							rb.velocity = newVelocity;
						}
					}
				}
				return;
			});

			//_context.CreateButton("DEBUG: Set Vertex Cache").button.onClick.AddListener(() =>
			//{
			//	_vertexCache = GetVerticesMethod1();
			//});

			_context.CreateButton("DEBUG: Check Vertices for changes").button.onClick.AddListener(() =>
			{
				if (_vertexCache.Count == 0)
				{
					_vertexCache = GetVerticesMethod1();
					SuperController.LogMessage($"Initialized cache: {_vertexCache.Count()}");
					return;
				}
				var newCache = GetVerticesMethod1();
				var changed = ReportDifferences(_vertexCache, newCache);
				SuperController.LogMessage($"Checked: cache: {newCache.Count()} against new: {_vertexCache.Count()}, changed: {changed}");
				//_vertexCache = newCache;
			});

			_context.CreateButton("DEBUG: Test Capture Vertex check").button.onClick.AddListener(() =>
			{
				//_firstEntryIsCurrentLook.val = false;
				var vertexFetchers = new List<Func<List<Vector3>>>();

				///========== VERTEX FECTCHERS ===========================================================================
				/// Each one of these function fetches a set of vertices in a different way or from a different part of the game
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZSkinWrap> components = containingAtom.GetComponentsInChildren<DAZSkinWrap>();
				//	SuperController.LogMessage($"DAZSkinWrap component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.Mesh;
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedMesh> components = containingAtom.GetComponentsInChildren<DAZMergedMesh>();
				//	SuperController.LogMessage($"DAZMergedMesh.baseMaterialVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component;
				//		var vertices = subComponent.baseMaterialVertices.Where(v => v.Length > 2).Select(v => new Vector3(v[0], v[1], v[2])).ToList();
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedMesh> components = containingAtom.GetComponentsInChildren<DAZMergedMesh>();
				//	SuperController.LogMessage($"DAZMergedMesh.basePolyList component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.basePolyList;
				//		List<Vector3> vertices = subComponent.Where(basePoly => basePoly.vertices.Length > 2).Select(basePoly => new Vector3(basePoly.vertices[0], basePoly.vertices[1], basePoly.vertices[2])).ToList();
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedMesh> components = containingAtom.GetComponentsInChildren<DAZMergedMesh>();
				//	SuperController.LogMessage($"DAZMergedMesh.baseVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component;
				//		var vertices = subComponent.baseVertices;
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});

				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedMesh> components = containingAtom.GetComponentsInChildren<DAZMergedMesh>();
				//	SuperController.LogMessage($"DAZMergedMesh.graftMesh.visibleMorphedUVVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.graftMesh;
				//		var vertices = subComponent.visibleMorphedUVVertices;
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});

				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedMesh> components = containingAtom.GetComponentsInChildren<DAZMergedMesh>();
				//	SuperController.LogMessage($"DAZMergedMesh.baseMesh component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.baseMesh;
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});

				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedMesh> components = containingAtom.GetComponentsInChildren<DAZMergedMesh>();
				//	SuperController.LogMessage($"DAZMergedMesh.UVVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component;
				//		var vertices = subComponent.UVVertices;
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});

				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZSkinV2> components = containingAtom.GetComponentsInChildren<DAZSkinV2>();
				//	SuperController.LogMessage($"DAZSkinV2.GetVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.Mesh;
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedSkinV2> components = containingAtom.GetComponentsInChildren<DAZMergedSkinV2>();
				//	SuperController.LogMessage($"DAZMergedSkinV2.GetVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.Mesh;
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZMergedSkinV2> components = containingAtom.GetComponentsInChildren<DAZMergedSkinV2>();
				//	SuperController.LogMessage($"DAZMergedSkinV2.GetVertices component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.Mesh;
				//		List<Vector3> vertices = new List<Vector3>(); //= subComponent.vertices;
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				//....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<MeshFilter> components = containingAtom.GetComponentsInChildren<MeshFilter>();
				//	SuperController.LogMessage($"MeshFilter component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.mesh;
				//		var vertices = subComponent.vertices;
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<MeshFilter> components = containingAtom.GetComponentsInChildren<MeshFilter>();
				//	SuperController.LogMessage($"MeshFilter component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.mesh;
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				//....................................................................................................
				vertexFetchers.Add(GetVerticesMethod1);
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZPhysicsMesh> components = containingAtom.GetComponentsInChildren<DAZPhysicsMesh>();
				//	SuperController.LogMessage($"DAZPhysicsMesh component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component.editorMeshForFocus;
				//		if (subComponent == null) SuperController.LogMessage($"DAZPhysicsMesh subcomponent is null");
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<Mesh> components = containingAtom.GetComponentsInChildren<Mesh>();
				//	SuperController.LogMessage($"Mesh component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		var subComponent = component;
				//		List<Vector3> vertices = new List<Vector3>();
				//		subComponent.GetVertices(vertices);
				//		for (int i = 0; i < vertices.Count(); i++)
				//		{
				//			vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//		}
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZBone> components = containingAtom.GetComponentsInChildren<DAZBone>();
				//	SuperController.LogMessage($"DAZBone component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		vertexCache.Add(component.baseJointRotation);
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................
				//vertexFetchers.Add(() =>
				//{
				//	IEnumerable<DAZBone> components = containingAtom.GetComponentsInChildren<DAZBone>();
				//	SuperController.LogMessage($"DAZBone component count: " + components.Count());
				//	var vertexCache = new List<Vector3>();
				//	foreach (var component in components.ToList())
				//	{
				//		vertexCache.Add(component.GetAngles());
				//	}
				//	return vertexCache;
				//});
				////....................................................................................................

				///========== VERTEX FECTCHERS ===========================================================================

				Action beforeMutation = () =>
				{
					SuperController.LogMessage($"==============================");
					_vertexCache = _context._currentCaptureRequest.VertexFetcher();
					IEnumerable<Mesh> components = _context.containingAtom.GetComponentsInChildren<Mesh>();
					foreach (var component in components)
					{
						component.UploadMeshData(true);
					}
				};
				Action afterMutation = () =>
				{
					IEnumerable<Mesh> components = _context.containingAtom.GetComponentsInChildren<Mesh>();
					foreach (var component in components)
					{
						component.UploadMeshData(true);
					}
					var vertexCacheAfter = _context._currentCaptureRequest.VertexFetcher();
					var changed = ReportDifferences(_vertexCache, vertexCacheAfter);
					SuperController.LogMessage($"Checked: cache: {vertexCacheAfter.Count()} against new: {_vertexCache.Count()}, changed: {changed}");
					_vertexCache = vertexCacheAfter;
				};

				foreach (var vertexFetcher in vertexFetchers)
				{
					//_currentCaptureRequest = new CaptureRequest()
					//{
					//	VertexFetcher = vertexFetcher
					//};
					//beforeMutation();
					//RandomizeMesh();
					//afterMutation();

					_context.RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE, beforeMutation, afterMutation, vertexFetcher);
				}

			});


			_context.CreateButton("DEBUG: Move body").button.onClick.AddListener(() =>
			{
				try
				{
					IEnumerable<Rigidbody> rigidBody = _context.containingAtom.GetComponentsInChildren<Rigidbody>();
					var vertexCache = new List<Vector3>();
					foreach (var rb in rigidBody.ToList())
					{
						if (!rb.name.ToLower().Contains("head")) continue;
						var randomVector = UnityEngine.Random.insideUnitSphere;
						//System.Random randomizer = new System.Random();
						//float randomX = randomizer.Next(-5, 5) / 10;
						//	//float randomY = randomizer.Next(-500, 500) / 100;
						//	//float randomZ = randomizer.Next(-500, 500) / 100;
						rb.position = randomVector;//new Vector3(randomX, rb.position.y, rb.position.z);
						vertexCache.Add(rb.position);
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Affect Box Free Controller").button.onClick.AddListener(() =>
			{
				try
				{
					var box_FreeControllerV3 = _context.containingAtom.GetComponentInChildren<FreeControllerV3>();

					//var box_FreeControllerV3 = _context.containingAtom.GetComponentInChildren<ConfigurableJoint>();
					//if (box_FreeControllerV3 == null) SuperController.LogMessage("cube free controller is null");
					// ...using ConfigurableJoint
					//box_FreeControllerV3.anchor = new Vector3(0.5f, 0.5f, 0.5f); // does nothing
					//box_FreeControllerV3.transform.Translate(new Vector3(0.5f, 0.5f, 0.5f)); // makes box jump and then snap back to joint

					//box_FreeControllerV3.currentRotationState = FreeControllerV3.RotationState.Off;
					//box_FreeControllerV3.guihidden = true;//...hides the HUD
					//box_FreeControllerV3.hidden = true; ///...does nothing
					//box_FreeControllerV3.ResetControl(); ///...Sends object back to 0
					//box_FreeControllerV3.ResetAppliedForces(); ///...does nothing
					//box_FreeControllerV3.MoveControl(new Vector3(0.5f, 0.5f, 0.5f)); /// ...moves object to position
					//box_FreeControllerV3.RotateAxis(FreeControllerV3.RotateAxisnames.X, 20f); /// ...rotates object by amount
					//box_FreeControllerV3.MoveControlRelatve(new Vector3(0.5f, 0.5f, 0.5f)); /// ...moves object relative to current position
					//box_FreeControllerV3.MoveAxis(FreeControllerV3.MoveAxisnames.X, 20f); /// ...moves object along an axis
					//box_FreeControllerV3.ControlAxis1(20);/// ...moves object along outer axis
					//box_FreeControllerV3.RotateControl(new Vector3(0.5f, 0.5f, 0.5f)); /// ...rotates object to orientation
					//box_FreeControllerV3.RotateWorldX(20, true); /// ...rotates object by amount				
					//box_FreeControllerV3.transform.Translate(new Vector3(0.5f, 0.5f, 0.5f)); /// ...moves object by amount

					//var newFreecontroller = GameObject.Instantiate<FreeControllerV3>(box_FreeControllerV3, _context.containingAtom.transform);
					//newFreecontroller.transform.Translate(UnityEngine.Random.insideUnitSphere);
					//box_FreeControllerV3.MoveLinkConnectorTowards(_context.transform, 10);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Print Object properties").button.onClick.AddListener(() =>
			{
				try
				{

					//var Cube_transform = _context.containingAtom.GetComponent<Transform>();
					//var Cube_evolvePrefab = Cube_transform.GetComponent<EvolvePrefab>();
					//var reParentObject_transform = Cube_evolvePrefab.GetComponent<Transform>();
					//var control_transform = reParentObject_transform.GetComponent<Transform>();
					//var box_FreeControllerV3 = _context.containingAtom.GetComponentInChildren<FreeControllerV3>();
					//if (box_FreeControllerV3 == null) SuperController.LogMessage("box_FreeControllerV3 is null");
					//var control_Rigidbody = box_FreeControllerV3[0].GetComponent<Rigidbody>();
					//var control_SphereCollider = control_Rigidbody.GetComponent<SphereCollider>();
					//var control_MotionAnimationControl = control_SphereCollider.GetComponent<MotionAnimationControl>();

					//var x = new Atom();
					GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
					_cylinder = cylinder;
					//cylinder.transform.parent = x.transform;

					var _Cube_transform = cylinder.GetComponent<Transform>();
					if (_Cube_transform == null) SuperController.LogMessage("_Cube_transform is null");
					var _Cube_evolvePrefab = _Cube_transform.gameObject.AddComponent<EvolvePrefab>();
					if (_Cube_evolvePrefab == null) SuperController.LogMessage("_Cube_evolvePrefab is null");
					var _reParentObject_transform = _Cube_evolvePrefab.GetComponent<Transform>();
					if (_reParentObject_transform == null) SuperController.LogMessage("_reParentObject_transform is null");


					var _control_transform = _reParentObject_transform.gameObject.GetComponent<Transform>();
					_control_transform.name = "control";



					if (_control_transform == null) SuperController.LogMessage("_control_transform is null");
					var cylinder_FreeControllerV3 = _control_transform.gameObject.AddComponent<FreeControllerV3>();
					if (cylinder_FreeControllerV3 == null) SuperController.LogMessage("_control_FreeControllerV3 is null");
					var _control_Rigidbody = cylinder_FreeControllerV3.gameObject.AddComponent<Rigidbody>();
					if (_control_Rigidbody == null) SuperController.LogMessage("_control_Rigidbody is null");
					var _control_SphereCollider = _control_Rigidbody.gameObject.AddComponent<SphereCollider>();
					if (_control_SphereCollider == null) SuperController.LogMessage("_control_SphereCollider is null");

					var _control_ForceReceiver = _control_Rigidbody.gameObject.AddComponent<ForceReceiver>();
					if (_control_ForceReceiver == null) SuperController.LogMessage("_control_ForceReceiver is null");

					var _control_ConfigurableJoint = _control_Rigidbody.gameObject.AddComponent<ConfigurableJoint>();
					if (_control_ConfigurableJoint == null) SuperController.LogMessage("_control_ConfigurableJoint is null");

					var _control_MotionAnimationControl = _control_SphereCollider.gameObject.AddComponent<MotionAnimationControl>();
					if (_control_MotionAnimationControl == null) SuperController.LogMessage("_control_MotionAnimationControl is null");

					//var cylinderMesh = cylinder.GetComponent<Mesh>();
					_control_Rigidbody.useGravity = false;
					cylinder_FreeControllerV3.name = "control";
					cylinder_FreeControllerV3.InitUI();
					//_control_FreeControllerV3.UITransform = cylinder.transform;
					cylinder_FreeControllerV3.enabled = true;
					cylinder_FreeControllerV3.physicsEnabled = true;
					//cylinder_FreeControllerV3.linkToRB = _control_Rigidbody;
					//_control_FreeControllerV3.guihidden = false;
					cylinder_FreeControllerV3.selected = true;
					//cylinder_FreeControllerV3.onPositionMesh = cylinderMesh;
					//cylinder_FreeControllerV3.moveModeOverlayMesh = cylinderMesh;

					_context.StartCoroutine(SendOffCylinder());

					var box_FreeControllerV3 = _context.containingAtom.GetComponentsInChildren<FreeControllerV3>(true);

					var newFreecontroller = GameObject.Instantiate<FreeControllerV3>(box_FreeControllerV3[0], _context.containingAtom.transform);
					box_FreeControllerV3 = new[] { newFreecontroller };

					//var box_FreeControllerV3 = _context.containingAtom.GetComponentsInChildren<FreeControllerV3>();

					SuperController.LogMessage("FreeControllerV3 count: " + box_FreeControllerV3.Count());

					SuperController.LogMessage("box_FreeControllerV3: ");
					SuperController.LogMessage("canGrabPosition: " + box_FreeControllerV3[0].canGrabPosition);
					SuperController.LogMessage("canGrabPosition: " + cylinder_FreeControllerV3.canGrabPosition);

					SuperController.LogMessage("canGrabRotation: " + box_FreeControllerV3[0].canGrabRotation);
					SuperController.LogMessage("canGrabRotation: " + cylinder_FreeControllerV3.canGrabRotation);

					SuperController.LogMessage("collisionEnabled: " + box_FreeControllerV3[0].collisionEnabled);
					SuperController.LogMessage("collisionEnabled: " + cylinder_FreeControllerV3.collisionEnabled);

					SuperController.LogMessage("controlMode: " + box_FreeControllerV3[0].controlMode);
					SuperController.LogMessage("controlMode: " + cylinder_FreeControllerV3.controlMode);

					SuperController.LogMessage("controlsCollisionEnabled: " + box_FreeControllerV3[0].controlsCollisionEnabled);
					SuperController.LogMessage("controlsCollisionEnabled: " + cylinder_FreeControllerV3.controlsCollisionEnabled);

					SuperController.LogMessage("controlsOn: " + box_FreeControllerV3[0].controlsOn);
					SuperController.LogMessage("controlsOn: " + cylinder_FreeControllerV3.controlsOn);

					SuperController.LogMessage("currentPositionState: " + box_FreeControllerV3[0].currentPositionState);
					SuperController.LogMessage("currentPositionState: " + cylinder_FreeControllerV3.currentPositionState);

					SuperController.LogMessage("distanceHolder: " + box_FreeControllerV3[0].distanceHolder);
					SuperController.LogMessage("distanceHolder: " + cylinder_FreeControllerV3.distanceHolder);

					SuperController.LogMessage("DrawForwardAxis: " + box_FreeControllerV3[0].DrawForwardAxis);
					SuperController.LogMessage("DrawForwardAxis: " + cylinder_FreeControllerV3.DrawForwardAxis);

					SuperController.LogMessage("drawMesh: " + box_FreeControllerV3[0].drawMesh);
					SuperController.LogMessage("drawMesh: " + cylinder_FreeControllerV3.drawMesh);

					SuperController.LogMessage("drawMeshWhenDeselected: " + box_FreeControllerV3[0].drawMeshWhenDeselected);
					SuperController.LogMessage("drawMeshWhenDeselected: " + cylinder_FreeControllerV3.drawMeshWhenDeselected);

					SuperController.LogMessage("drawSnapshot: " + box_FreeControllerV3[0].drawSnapshot);
					SuperController.LogMessage("drawSnapshot: " + cylinder_FreeControllerV3.drawSnapshot);

					SuperController.LogMessage("DrawUpAxis: " + box_FreeControllerV3[0].DrawUpAxis);
					SuperController.LogMessage("DrawUpAxis: " + cylinder_FreeControllerV3.DrawUpAxis);

					SuperController.LogMessage("enabled: " + box_FreeControllerV3[0].enabled);
					SuperController.LogMessage("enabled: " + cylinder_FreeControllerV3.enabled);

					SuperController.LogMessage("enableSelectRoot: " + box_FreeControllerV3[0].enableSelectRoot);
					SuperController.LogMessage("enableSelectRoot: " + cylinder_FreeControllerV3.enableSelectRoot);

					SuperController.LogMessage("exclude: " + box_FreeControllerV3[0].exclude);
					SuperController.LogMessage("exclude: " + cylinder_FreeControllerV3.exclude);

					//SuperController.LogMessage("focusPoint: " + box_FreeControllerV3[0].focusPoint.position.x + "," + box_FreeControllerV3[0].focusPoint.position.y + "," + box_FreeControllerV3[0].focusPoint.position.z);
					//SuperController.LogMessage("focusPoint: " + _control_FreeControllerV3.focusPoint.position.x + "," + _control_FreeControllerV3.focusPoint.position.y + "," + _control_FreeControllerV3.focusPoint.position.z);

					SuperController.LogMessage("forceFactor: " + box_FreeControllerV3[0].forceFactor);
					SuperController.LogMessage("forceFactor: " + cylinder_FreeControllerV3.forceFactor);

					SuperController.LogMessage("globalCollisionEnabled: " + box_FreeControllerV3[0].globalCollisionEnabled);
					SuperController.LogMessage("globalCollisionEnabled: " + cylinder_FreeControllerV3.globalCollisionEnabled);

					SuperController.LogMessage("GUIalwaysVisibleWhenSelected: " + box_FreeControllerV3[0].GUIalwaysVisibleWhenSelected);
					SuperController.LogMessage("GUIalwaysVisibleWhenSelected: " + cylinder_FreeControllerV3.GUIalwaysVisibleWhenSelected);

					SuperController.LogMessage("guihidden: " + box_FreeControllerV3[0].guihidden);
					SuperController.LogMessage("guihidden: " + cylinder_FreeControllerV3.guihidden);

					SuperController.LogMessage("HasParamsOrActions: " + box_FreeControllerV3[0].HasParamsOrActions());
					SuperController.LogMessage("HasParamsOrActions: " + cylinder_FreeControllerV3.HasParamsOrActions());

					SuperController.LogMessage("hidden: " + box_FreeControllerV3[0].hidden);
					SuperController.LogMessage("hidden: " + cylinder_FreeControllerV3.hidden);

					SuperController.LogMessage("hideFlags: " + box_FreeControllerV3[0].hideFlags);
					SuperController.LogMessage("hideFlags: " + cylinder_FreeControllerV3.hideFlags);

					SuperController.LogMessage("highlighted: " + box_FreeControllerV3[0].highlighted);
					SuperController.LogMessage("highlighted: " + cylinder_FreeControllerV3.highlighted);

					SuperController.LogMessage("holdColor: " + box_FreeControllerV3[0].holdColor);
					SuperController.LogMessage("holdColor: " + cylinder_FreeControllerV3.holdColor);

					SuperController.LogMessage("holdPositionMesh: " + box_FreeControllerV3[0].holdPositionMesh);
					SuperController.LogMessage("holdPositionMesh: " + cylinder_FreeControllerV3.holdPositionMesh);

					SuperController.LogMessage("interactableInPlayMode: " + box_FreeControllerV3[0].interactableInPlayMode);
					SuperController.LogMessage("interactableInPlayMode: " + cylinder_FreeControllerV3.interactableInPlayMode);

					SuperController.LogMessage("isActiveAndEnabled: " + box_FreeControllerV3[0].isActiveAndEnabled);
					SuperController.LogMessage("isActiveAndEnabled: " + cylinder_FreeControllerV3.isActiveAndEnabled);

					SuperController.LogMessage("IsInvoking: " + box_FreeControllerV3[0].IsInvoking());
					SuperController.LogMessage("IsInvoking: " + cylinder_FreeControllerV3.IsInvoking());

					SuperController.LogMessage("isPositionOn: " + box_FreeControllerV3[0].isPositionOn);
					SuperController.LogMessage("isPositionOn: " + cylinder_FreeControllerV3.isPositionOn);

					SuperController.LogMessage("isPresetRestore: " + box_FreeControllerV3[0].isPresetRestore);
					SuperController.LogMessage("isPresetRestore: " + cylinder_FreeControllerV3.isPresetRestore);

					SuperController.LogMessage("isRotationOn: " + box_FreeControllerV3[0].isRotationOn);
					SuperController.LogMessage("isRotationOn: " + cylinder_FreeControllerV3.isRotationOn);

					SuperController.LogMessage("jointRotationDriveDamper: " + box_FreeControllerV3[0].jointRotationDriveDamper);
					SuperController.LogMessage("jointRotationDriveDamper: " + cylinder_FreeControllerV3.jointRotationDriveDamper);

					SuperController.LogMessage("linkToRB: " + box_FreeControllerV3[0].linkToRB);
					SuperController.LogMessage("linkToRB: " + cylinder_FreeControllerV3.linkToRB);

					SuperController.LogMessage("meshScale: " + box_FreeControllerV3[0].meshScale);
					SuperController.LogMessage("meshScale: " + cylinder_FreeControllerV3.meshScale);

					SuperController.LogMessage("moveFactor: " + box_FreeControllerV3[0].moveFactor);
					SuperController.LogMessage("moveFactor: " + cylinder_FreeControllerV3.moveFactor);

					SuperController.LogMessage("name: " + box_FreeControllerV3[0].name);
					SuperController.LogMessage("name: " + cylinder_FreeControllerV3.name);

					SuperController.LogMessage("needsStore: " + box_FreeControllerV3[0].needsStore);
					SuperController.LogMessage("needsStore: " + cylinder_FreeControllerV3.needsStore);

					SuperController.LogMessage("on: " + box_FreeControllerV3[0].on);
					SuperController.LogMessage("on: " + cylinder_FreeControllerV3.on);

					SuperController.LogMessage("overrideId: " + box_FreeControllerV3[0].overrideId);
					SuperController.LogMessage("overrideId: " + cylinder_FreeControllerV3.overrideId);

					SuperController.LogMessage("physicsEnabled: " + box_FreeControllerV3[0].physicsEnabled);
					SuperController.LogMessage("physicsEnabled: " + cylinder_FreeControllerV3.physicsEnabled);

					SuperController.LogMessage("positionGridMode: " + box_FreeControllerV3[0].positionGridMode);
					SuperController.LogMessage("positionGridMode: " + cylinder_FreeControllerV3.positionGridMode);

					SuperController.LogMessage("positionGrid: " + box_FreeControllerV3[0].positionGrid);
					SuperController.LogMessage("positionGrid: " + cylinder_FreeControllerV3.positionGrid);

					SuperController.LogMessage("possessable: " + box_FreeControllerV3[0].possessable);
					SuperController.LogMessage("possessable: " + cylinder_FreeControllerV3.possessable);

					SuperController.LogMessage("possessed: " + box_FreeControllerV3[0].possessed);
					SuperController.LogMessage("possessed: " + cylinder_FreeControllerV3.possessed);

					SuperController.LogMessage("rotationGridMode: " + box_FreeControllerV3[0].rotationGridMode);
					SuperController.LogMessage("rotationGridMode: " + cylinder_FreeControllerV3.rotationGridMode);

					SuperController.LogMessage("scale: " + box_FreeControllerV3[0].scale);
					SuperController.LogMessage("scale: " + cylinder_FreeControllerV3.scale);

					SuperController.LogMessage("selected: " + box_FreeControllerV3[0].selected);
					SuperController.LogMessage("selected: " + cylinder_FreeControllerV3.selected);

					SuperController.LogMessage("startingPosition: " + box_FreeControllerV3[0].startingPosition.x);
					SuperController.LogMessage("startingPosition: " + cylinder_FreeControllerV3.startingPosition.x);

					SuperController.LogMessage("startingPositionState: " + box_FreeControllerV3[0].startingPositionState);
					SuperController.LogMessage("startingPositionState: " + cylinder_FreeControllerV3.startingPositionState);

					SuperController.LogMessage("stateCanBeModified: " + box_FreeControllerV3[0].stateCanBeModified);
					SuperController.LogMessage("stateCanBeModified: " + cylinder_FreeControllerV3.stateCanBeModified);

					SuperController.LogMessage("storePositionRotationAsLocal: " + box_FreeControllerV3[0].storePositionRotationAsLocal);
					SuperController.LogMessage("storePositionRotationAsLocal: " + cylinder_FreeControllerV3.storePositionRotationAsLocal);

					SuperController.LogMessage(".tag: " + box_FreeControllerV3[0].tag);
					SuperController.LogMessage(".tag: " + cylinder_FreeControllerV3.tag);

					SuperController.LogMessage("torqueFactor: " + box_FreeControllerV3[0].torqueFactor);
					SuperController.LogMessage("torqueFactor: " + cylinder_FreeControllerV3.torqueFactor);

					SuperController.LogMessage("unhighlightedScale: " + box_FreeControllerV3[0].unhighlightedScale);
					SuperController.LogMessage("unhighlightedScale: " + cylinder_FreeControllerV3.unhighlightedScale);

					SuperController.LogMessage("useContainedMeshRenderers: " + box_FreeControllerV3[0].useContainedMeshRenderers);
					SuperController.LogMessage("useContainedMeshRenderers: " + cylinder_FreeControllerV3.useContainedMeshRenderers);

					SuperController.LogMessage("useForceWhenOff: " + box_FreeControllerV3[0].useForceWhenOff);
					SuperController.LogMessage("useForceWhenOff: " + cylinder_FreeControllerV3.useForceWhenOff);

					SuperController.LogMessage("useGravityOnRBWhenOff: " + box_FreeControllerV3[0].useGravityOnRBWhenOff);
					SuperController.LogMessage("useGravityOnRBWhenOff: " + cylinder_FreeControllerV3.useGravityOnRBWhenOff);

					SuperController.LogMessage("useGUILayout: " + box_FreeControllerV3[0].useGUILayout);
					SuperController.LogMessage("useGUILayout: " + cylinder_FreeControllerV3.useGUILayout);

					SuperController.LogMessage("xLocalLock: " + box_FreeControllerV3[0].xLocalLock);
					SuperController.LogMessage("xLocalLock: " + cylinder_FreeControllerV3.xLocalLock);

					SuperController.LogMessage("xLock: " + box_FreeControllerV3[0].xLock);
					SuperController.LogMessage("xLock: " + cylinder_FreeControllerV3.xLock);

					SuperController.LogMessage("xPositionText: " + box_FreeControllerV3[0].xPositionText);
					SuperController.LogMessage("xPositionText: " + cylinder_FreeControllerV3.xPositionText);

					SuperController.LogMessage("xRotLock: " + box_FreeControllerV3[0].xRotLock);
					SuperController.LogMessage("xRotLock: " + cylinder_FreeControllerV3.xRotLock);

					SuperController.LogMessage("_RBLockPositionMaxForce: " + box_FreeControllerV3[0]._RBLockPositionMaxForce);
					SuperController.LogMessage("_RBLockPositionMaxForce: " + cylinder_FreeControllerV3._RBLockPositionMaxForce);




					//SuperController.LogMessage("name: " + box_FreeControllerV3[0].name);
					//SuperController.LogMessage("name: " + _control_FreeControllerV3.name);
					//SuperController.LogMessage("localScale: " + box_FreeControllerV3[0].localScale);
					//SuperController.LogMessage("localScale: " + _control_FreeControllerV3.localScale);
					//SuperController.LogMessage("hideFlags: " + box_FreeControllerV3[0].hideFlags);
					//SuperController.LogMessage("hideFlags: " + _control_FreeControllerV3.hideFlags);
					//SuperController.LogMessage("hierarchyCapacity: " + box_FreeControllerV3[0].hierarchyCapacity);
					//SuperController.LogMessage("hierarchyCapacity: " + _control_FreeControllerV3.hierarchyCapacity);
					//SuperController.LogMessage("hierarchyCount: " + box_FreeControllerV3[0].hierarchyCount);
					//SuperController.LogMessage("hierarchyCount: " + _control_FreeControllerV3.hierarchyCount);
					//SuperController.LogMessage("forward: " + box_FreeControllerV3[0].forward);
					//SuperController.LogMessage("forward: " + _control_FreeControllerV3.forward);
					//SuperController.LogMessage("right: " + box_FreeControllerV3[0].right);
					//SuperController.LogMessage("right: " + _control_FreeControllerV3.right);
					//SuperController.LogMessage("up: " + box_FreeControllerV3[0].up);
					//SuperController.LogMessage("up: " + _control_FreeControllerV3.up);
					//SuperController.LogMessage("root: " + box_FreeControllerV3[0].root);
					//SuperController.LogMessage("root: " + _control_FreeControllerV3.root);

					//SuperController.LogMessage("_control_Rigidbody: ");
					//SuperController.LogMessage("position: " + control_Rigidbody.position);
					//SuperController.LogMessage("position: " + _control_Rigidbody.position);
					//SuperController.LogMessage("isKinematic: " + control_Rigidbody.isKinematic);
					//SuperController.LogMessage("isKinematic: " + _control_Rigidbody.isKinematic);
					//SuperController.LogMessage("velocity: " + control_Rigidbody.velocity.x);
					//SuperController.LogMessage("velocity: " + _control_Rigidbody.velocity.x);
					//SuperController.LogMessage("collisionDetectionMode: " + control_Rigidbody.collisionDetectionMode);
					//SuperController.LogMessage("collisionDetectionMode: " + _control_Rigidbody.collisionDetectionMode);
					//SuperController.LogMessage("constraints: " + control_Rigidbody.constraints);
					//SuperController.LogMessage("constraints: " + _control_Rigidbody.constraints);
					//SuperController.LogMessage("detectCollisions: " + control_Rigidbody.detectCollisions);
					//SuperController.LogMessage("detectCollisions: " + _control_Rigidbody.detectCollisions);
					//SuperController.LogMessage("freezeRotation: " + control_Rigidbody.freezeRotation);
					//SuperController.LogMessage("freezeRotation: " + _control_Rigidbody.freezeRotation);
					//SuperController.LogMessage("hideFlags: " + control_Rigidbody.hideFlags);
					//SuperController.LogMessage("hideFlags: " + _control_Rigidbody.hideFlags);
					//SuperController.LogMessage("useGravity: " + control_Rigidbody.useGravity);
					//SuperController.LogMessage("useGravity: " + _control_Rigidbody.useGravity);
					//SuperController.LogMessage("IsSleeping: " + control_Rigidbody.IsSleeping());
					//SuperController.LogMessage("IsSleeping: " + _control_Rigidbody.IsSleeping());

				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Reset Pivot").button.onClick.AddListener(() =>
			{
				try
				{
					//var control = _context.containingAtom.GetComponentsInChildren<FreeControllerV3>().First(c => c.name == "control");
					var joints = _context.containingAtom.GetComponentsInChildren<ConfigurableJoint>();
					foreach (var joint in joints)
					{
						joint.enableCollision = false;
						joint.xMotion = ConfigurableJointMotion.Free;
						//joint.angularYZDrive.mode = JointDriveMode.None;
					}

					//SuperController.LogMessage("controls count: " + control.name);
					//control.currentRotationState = FreeControllerV3.RotationState.Off;
					//control.currentPositionState = FreeControllerV3.PositionState.Off;

					//control.controlMode = FreeControllerV3.ControlMode.Off;
					//control.positionGridMode = FreeControllerV3.GridMode.None;
					//control.rotationGridMode = FreeControllerV3.GridMode.None;
					//control.ResetControl(); ///...Sends object back to 0
					//control.RotateAxis(FreeControllerV3.RotateAxisnames.X, 20f);
					//control.XRotation0();

					//control.currentRotationState = FreeControllerV3.RotationState.On;
					//control.currentPositionState = FreeControllerV3.PositionState.On;

				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});

			_context.CreateButton("DEBUG: Create Object").button.onClick.AddListener(() =>
			{
				try
				{
					//var asset = SuperController.singleton.atomAssets.FirstOrDefault(a => a.assetName == "Cube");
					//var assetBundleName = asset.assetBundleName;
					//SuperController.LogMessage("atomAsset assetName: " + asset.assetName);
					//SuperController.LogMessage("atomAsset assetBundleName: " + asset.assetBundleName);

					//foreach (var item in SuperController.singleton.atomAssets.Where(a => a.assetName == "Cube"))
					//{

					//	//SuperController.LogMessage("assetBundleName " + item.assetBundleName);
					//	SuperController.LogMessage("assetName " + item.assetName);
					//}


					//var freeControllers = _context.containingAtom.GetComponentsInChildren<Atom>();
					//foreach (var freeController in freeControllers)
					//{
					//	SuperController.LogMessage("FreeController: " + freeController.name);
					//	var newFreecontroller = GameObject.Instantiate<Atom>(freeController, _context.containingAtom.transform);
					//	//newFreecontroller.enabled = true;
					//	newFreecontroller.transform.Translate(UnityEngine.Random.insideUnitSphere);
					//}

					//AtomAsset cubeAtom = SuperController.singleton.atomAssets.FirstOrDefault();
					//Atom x = SuperController.singleton.GetAtoms().First();
					//SuperController.singleton.selectAtomPopup.currentValue = "Cube";

					//SuperController.LogMessage("selectAtomPopup.currentValue: " + SuperController.singleton.atomPrefabPopup.currentValue);
					//SuperController.LogMessage("selectAtomPopup.currentValueNoCallback: " + SuperController.singleton.atomPrefabPopup.currentValueNoCallback);
					//SuperController.LogMessage("selectAtomPopup.displayPopupValues: " + SuperController.singleton.atomPrefabPopup.displayPopupValues);
					//SuperController.LogMessage("selectAtomPopup.label: " + SuperController.singleton.atomPrefabPopup.label);
					//SuperController.LogMessage("selectAtomPopup.labelText: " + SuperController.singleton.atomPrefabPopup.labelText);
					//SuperController.LogMessage("selectAtomPopup.numPopupValues: " + SuperController.singleton.atomPrefabPopup.numPopupValues);
					//SuperController.LogMessage("selectAtomPopup.popupValues: " + SuperController.singleton.atomPrefabPopup.popupValues);
					//SuperController.LogMessage("selectAtomPopup.atomPrefabs: " + SuperController.singleton.atomPrefabs.Count());


					//SuperController.singleton.selectAtomPopup.currentValue = "ISCapsule";
					//SuperController.singleton.atomPrefabPopup.setDisplayPopupValue(1, "Cube");
					//SuperController.singleton.selectAtomPopup.SetPreviousValue();
					//SuperController.singleton.atomPrefabPopup.setPopupValue(1, "Cube");
					//SuperController.singleton.atomPrefabPopup.SetNextValue();

					//SuperController.singleton.atomPrefabPopup.Toggle();

					SuperController.LogMessage("atomAssets.Count: " + SuperController.singleton.atomAssets.Count());
					foreach (var atomAsset in SuperController.singleton.atomAssets)
					{
						SuperController.LogMessage("atomAsset: " + atomAsset.category + "\\" + atomAsset.assetName);

					}
					AtomAsset cubeAtomAsset = SuperController.singleton.atomAssets.First(a => a.category == "Shapes" && a.assetName == "Cube");
					//SuperController.singleton.LoadAtomFromBundleAsync(cubeAtomAsset);

					//SuperController.singleton.AddAtomByType(cubeAtomAsset.GetType());

					//SuperController.LogMessage("atomPrefabs.Count: " + SuperController.singleton.atomPrefabs.Count());
					//SuperController.LogMessage("AtomPopup.numPopupValues: " + SuperController.singleton.atomPrefabPopup.numPopupValues);
					//SuperController.singleton.AddAtomByPopupValue();


					//atomTransform.Translate(UnityEngine.Random.insideUnitSphere);

					//foreach (var item in SuperController.singleton.GetAtoms())
					//{
					//	//SuperController.LogMessage("assetBundleName " + item.assetBundleName);
					//	SuperController.LogMessage("GetAtoms " + item.name);
					//}


					//foreach (var item in GameObject.FindGameObjectsWithTag("Cube"))
					//{
					//	SuperController.LogMessage("Cube " + item.name);
					//}

					//SuperController.LogMessage("atomAssetsFile " + SuperController.singleton.atomAssetsFile);

					//foreach (var item in SuperController.singleton.atomPrefabs)
					//{
					//	SuperController.LogMessage("atomPrefabs.name " + item.name);
					//}

					//foreach (var item in SuperController.singleton.indirectAtomAssets)
					//{
					//	SuperController.LogMessage("indirectAtomAsset " + item.assetName);

					//}

					//foreach (var item in SuperController.singleton.indirectAtomAssets)
					//{
					//	SuperController.LogMessage("atomPrefabs.name " + item.name);
					//}

					//GameObject prefab = GameObject.Find("Cube");
					//if (prefab == null ) {
					//	SuperController.LogMessage("prefab is null");
					//}
					//else {
					//	SuperController.LogMessage("IsPrefab: " + prefab.IsPrefab());
					//	var newAtom = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity);
					//	if (newAtom == null) { 
					//		SuperController.LogMessage("atom is null");
					//	}
					//	else
					//	{
					//		SuperController.LogMessage("atom: " + newAtom.name);
					//		SuperController.LogMessage("activeSelf: " + newAtom.activeSelf);
					//		newAtom.SetActive(true);
					//	}
					//}

					//var prefabs = SuperController.singleton.atomPrefabs;
					//SuperController.LogMessage("prefab count: " + prefabs.Length);
					//foreach (var prefab in prefabs)
					//{
					//	SuperController.LogMessage("prefab: " + prefab.name);
					//}

					////var gameObject = SuperController.singleton.GetCachedPrefab(asset.assetBundleName, asset.assetName);
					////if (gameObject == null) {
					////	SuperController.LogMessage("gameobject is null");
					////	return;
					////}
					//SuperController.LogMessage("gameobject: " + _context.gameObject.name);
					////var atom = new Atom();
					////atom.type = "Cube";
					////atom.name = "Cube";
					////atom.
					////SuperController.LogMessage("Atom Count: " + assets.Count());
					//GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
					//Transform transform = cylinder.GetComponent<Transform>();
					//var evolvePrefab = cylinder.AddComponent<EvolvePrefab>();
					////Transform transform2 = evolvePrefab.gameObject.GetComponent<Transform>();
					//evolvePrefab.useGUILayout = true;
					//var presetManager = cylinder.AddComponent<PresetManager>();
					//var presetManagerControl = presetManager.gameObject.AddComponent<PresetManagerControl>();
					//FreeControllerV3 freeController = cylinder.AddComponent<FreeControllerV3>();
					//freeController = new FreeControllerV3();
					//freeController.ShowGUI();
					//Rigidbody gameObjectsRigidBody = cylinder.AddComponent<Rigidbody>();
					////transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
					//cylinder.transform.position = new Vector3(-0.5f, 1, 0);
					//cylinder.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
					////transform.position = new Vector3(-2, 1, 0);
					////transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
					////transform2.position = new Vector3(-2, 1, 0);
					////transform2.localScale = new Vector3(0.5f, 0.5f, 0.5f);
					//gameObjectsRigidBody.useGravity = false;
					////gameObjectsRigidBody.isKinematic = false;
					////freeController.enabled = true;
					////freeController.ShowGUI();
					//MotionAnimationControl animationControl = cylinder.AddComponent<MotionAnimationControl>();
					//SphereCollider sphereColiider = cylinder.AddComponent<SphereCollider>();
					//ForceReceiver forceReceiver = cylinder.AddComponent<ForceReceiver>();
					//ConfigurableJoint configurableJoint = cylinder.AddComponent<ConfigurableJoint>();
					//PhysicsMaterialControl physicsMaterialControl = cylinder.AddComponent<PhysicsMaterialControl>();
					//CollisionTrigger collisionTrigger = cylinder.AddComponent<CollisionTrigger>();
					////cylinder.AddComponent<MoveAndRotateAsHUDAnchor>();
					////SuperController.singleton.AddAtomByType("Cube");
					////for (int i = 0; i < assets.Length; i++)
					////{
					////	SuperController.LogMessage("Atom " + assets[i].type);
					////}
					////SuperController.singleton.AddAtom(atom);
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});


			/// Works on a cube object, but not a Person...
			_context.CreateButton("DEBUG: Affect HUD Anchor").button.onClick.AddListener(() =>
			{
				try
				{
					//var collection = _context.containingAtom.GetComponentsInChildren<ConfigurableJoint>();
					//SuperController.LogMessage("hud anchors: " + collection.Count());

					var box_HudAnchor = _context.containingAtom.GetComponentInChildren<ConfigurableJoint>();
					if (box_HudAnchor == null) SuperController.LogMessage("cube free controller is null");
					//box_HudAnchor.transform.Translate(new Vector3(20f, 20f, 20f));
					//box_HudAnchor.
					//box_HudAnchor.useGUILayout = false;

					//SuperController.LogMessage("box_HudAnchor.isActiveAndEnabled: " + box_HudAnchor.isActiveAndEnabled);

				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});


			/// Works on a cube object, but not a Person...
			_context.CreateButton("DEBUG: Modify cube vertices").button.onClick.AddListener(() =>
			{
				try
				{
					RandomizeMesh();
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
					throw (e);
				}
			});


			_context.CreateButton("List active morphs").button.onClick.AddListener(() =>
			{
				_context._mutationsService.CaptureActiveMorphsForCurrentPerson().Take(1).ToList().ForEach(m => SuperController.LogMessage($"Active Morph: {m.Id}: {m.Value}"));
			});

			_context.CreateButton("List base morphs").button.onClick.AddListener(() =>
			{
				_context._mutationsService.GetCurrentMorphBaseValues().ForEach(m => SuperController.LogMessage($"Morph Base: {m.Id}: {m.Value}"));
			});

			//_context.CreateButton("Set active morphs").button.onClick.AddListener(() =>
			//{
			//	_context._mutationsService.SetMorphBaseValuesForCurrentPerson();
			//});


		} // end of Init()

		IEnumerator AfterRestoring(Atom atom, JSONStorable newStorable)
		{
			yield return new WaitForSeconds(1);
			try
			{
				SuperController.LogMessage("Storable is null: " + (newStorable == null));
				//newStorable.exclude = false;
				//newStorable.enabled = true;
				//atom.UnregisterAdditionalStorable(newStorable);
				var success = atom.RegisterAdditionalStorable(newStorable);
				SuperController.LogMessage("Registered storable: " + success);
				//atom.RegisterBool(newStorable);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		IEnumerator SendOffCylinder()
		{
			float speed = 1;
			while (_cylinder.transform.position.x < 2)
			{
				yield return null;
				_cylinder.transform.Translate(_cylinder.transform.up * speed * Time.deltaTime);
			}
		}

		private static int ReportDifferences(List<Vector3> cache1, List<Vector3> cache2)
		{
			float totalX = 0, totalY = 0, totalZ = 0;
			var changed = 0;
			for (int i = 0; i < cache1.Count(); i++)
			{
				if (cache2.Count() < i) break;
				var oldVertex = cache1[i];
				var newVertex = cache2[i];
				if (oldVertex.x != newVertex.x || oldVertex.y != newVertex.y || oldVertex.z != newVertex.z)
				{
					totalX += (newVertex.x - oldVertex.x);
					totalY += (newVertex.y - oldVertex.y);
					totalZ += (newVertex.z - oldVertex.z);
					changed++;
					//SuperController.LogMessage($"vertex updated-{i}: {newVertex.x},{newVertex.y},{newVertex.z}");
				}
			}
			SuperController.LogMessage($"Ave variance: X: {totalX / changed}, Y: {totalY / changed}, Z: {totalZ / changed}");
			return changed;
		}

		private void RandomizeMesh()
		{
			IEnumerable<MeshFilter> meshFilters = _context.containingAtom.GetComponentsInChildren<MeshFilter>();
			foreach (var meshFilter in meshFilters.ToList())
			{
				var mesh = meshFilter.mesh;
				var newVertices = new Vector3[mesh.vertices.Length];
				for (int i = 0; i < mesh.vertices.Length; i++)
				{
					var randomVector = UnityEngine.Random.insideUnitSphere;
					newVertices[i] = randomVector;
				}
				mesh.vertices = newVertices;
			}
		}

		List<string> GetComponentsTree(Component comp, int maxLevel = 0, int level = 0, List<int> searched = null)
		{
			var lines = new List<string>();
			if (maxLevel > 0 && level >= maxLevel) return lines;
			if (comp == null) return lines;
			if (searched == null) searched = new List<int>();
			var instanceId = comp.GetInstanceID();
			if (searched.Contains(instanceId)) return lines;
			searched.Add(instanceId);
			var treeList = new string('.', level) + comp.name + ": " + comp.GetType();
			lines.Add(treeList);
			var childComponents = comp.GetComponentsInChildren<Component>();
			var decendantLines = childComponents.SelectMany(c => GetComponentsTree(c, maxLevel, level + 1, searched)).ToList();
			lines.AddRange(decendantLines);
			return lines;
		}

		private List<Vector3> GetVerticesMethod1()
		{
			IEnumerable<DAZPhysicsMesh> components = _context.containingAtom.GetComponentsInChildren<DAZPhysicsMesh>();
			SuperController.LogMessage($"DAZPhysicsMesh.hardVerticesGroups component count: " + components.Count());
			var vertexCache = new List<Vector3>();

			foreach (var component in components.ToList())
			{
				var subComponent = component;
				if (subComponent == null) SuperController.LogMessage($"DAZPhysicsMesh subcomponent is null");
				//var vertices = subComponent.currentHardVerticesGroup.Where(g => g > 2).Select(g => new Vector3(g.vertices[0], g.vertices[1], g.vertices[3])).ToList();
				//SuperController.LogMessage($"softVerticesGroups count: " + subComponent.softVerticesGroups.Count);
				foreach (var grp in subComponent.softVerticesGroups)
				{
					//SuperController.LogMessage($" softVerticesSets count: " + grp.softVerticesSets.Count);
					foreach (var softVerticesSet in grp.softVerticesSets)
					{
						//SuperController.LogMessage($"   anchorVertex: " + softVerticesSet.currentPosition);
						//vertexCache.Add(softVerticesSet.currentKinematicPosition);
						//vertexCache.Add(softVerticesSet.currentPosition);
						vertexCache.Add(softVerticesSet.initialTargetPosition); // * (SOME)
																																		//softVerticesSet.initialTargetPosition  =new Vector3(softVerticesSet.initialTargetPosition.x + 0.4f, softVerticesSet.initialTargetPosition.y, softVerticesSet.initialTargetPosition.z);
																																		//vertexCache.Add(softVerticesSet.jointTargetPosition); // * (all)
																																		//vertexCache.Add(softVerticesSet.lastJointTargetPosition); // * (all)
																																		//vertexCache.Add(softVerticesSet.lastKinematicPosition); // *(all)
																																		//vertexCache.Add(softVerticesSet.lastKinematicPositionThreaded);
																																		//vertexCache.Add(softVerticesSet.lastPosition); // *(all)
																																		//vertexCache.Add(softVerticesSet.lastPositionThreaded);
																																		//vertexCache.Add(softVerticesSet.kinematicTransform.position); // *(all)
					}
				}

				//SuperController.LogMessage($"currentHardVerticesGroup count: " + subComponent.currentSoftVerticesGroup.vertices.Count());

				//for (int i = 0; i < vertices.Count(); i++)
				//{

				//	vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
				//}
			}
			return vertexCache;
		}




		private void PerformRandomMorph(string morphName)
		{
			JSONStorable geometry = _context.containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			GenerateDAZMorphsControlUI morphControl = character.morphsControlUI;
			DAZMorph item = morphControl.GetMorphs().First(h => h.displayName == morphName);
			System.Random randomizer = new System.Random();
			float randomVal = randomizer.Next(0, 100) / 100;
			item.SetValue(randomVal);
		}


		private List<int[][]> GetVertexCacheIntArray()
		{
			try
			{
				IEnumerable<DAZMesh> components = _context.containingAtom.GetComponentsInChildren<DAZMesh>();
				List<int[][]> vertexCache = new List<int[][]>();
				foreach (var component in components.ToList())
				{
					var subComponent = component.baseMaterialVertices;
					if (subComponent == null) { SuperController.LogMessage($"subComponent is null"); throw new Exception("subComponent is null"); }
					if (subComponent.Length == 0) { SuperController.LogMessage($"subComponent list is empty"); throw new Exception("subComponent list is null"); }
					SuperController.LogMessage($"component.baseMaterialVertices " + component.baseMaterialVertices.Count());
					var newVertexCache = new int[component.baseMaterialVertices.Length][];
					for (int x = 0; x < component.baseMaterialVertices.Length; x++)
					{
						newVertexCache[x] = new int[component.baseMaterialVertices[x].Length];
						for (int y = 0; y < component.baseMaterialVertices[x].Length; y++)
						{
							newVertexCache[x][y] = component.baseMaterialVertices[x][y];
						}
					}
					vertexCache.Add(newVertexCache);
				}
				return vertexCache;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw (e);
			}
		}

		private List<Vector3> GetVertexCacheForVector3()
		{
			try
			{
				//SuperController.LogMessage($"Start");
				//IEnumerable<Component> components = containingAtom.gameObject.GetComponentsInChildren(typeof(MeshFilter), true);
				//var rigidBodies = containingAtom.linkableRigidbodies;
				//foreach (var rigidBody in rigidBodies.Take(10).ToList())
				//{
				//	SuperController.LogMessage($"rigidBody: " + rigidBody.name);
				//}
				//var head = containingAtom.freeControllers.FirstOrDefault(b => b.name == "headControl");
				//var headMesh = head.gameObject.GetComponent<MeshFilter>();
				//Mesh viewedModel = headMesh.mesh;
				//var vertices = new List<Vector3>();
				//viewedModel.GetVertices(vertices);
				//SuperController.LogMessage($"vertex count: " + vertices.Count);
				//foreach (var vertex in vertices.Take(10).ToList())
				//{
				//	SuperController.LogMessage($"vertex: {vertex.x},{vertex.y},{vertex.z}");
				//}

				//SuperController.LogMessage($"rigidbodies count: " + containingAtom.rigidbodies.Count());
				//foreach (var body in containingAtom.rigidbodies.Where(b => b.name == "headControl"))
				//{
				//	SuperController.LogMessage($"rigidbodies count: " + body.name);
				//	MeshFilter comps = body.GetComponentInChildren<MeshFilter>();
				//	SuperController.LogMessage($"comps count: " + comps.mesh.vertices.Count());
				//}
				IEnumerable<DAZMesh> components = _context.containingAtom.GetComponentsInChildren<DAZMesh>();
				//IEnumerable<Rigidbody> rigidBody = containingAtom.GetComponentsInChildren<Rigidbody>();
				SuperController.LogMessage($"component count: " + components.Count());
				var vertexCache = new List<Vector3>();
				//float max = 0;
				//float min = 0;
				foreach (var component in components.ToList())
				{

					//vertexCache.Add(point);
					//SuperController.LogMessage($"Rigidbody name: " + rb.name);
					//Mesh mesh = meshFilter.mesh;
					//bone.SetBoneXOffset(bone.worldPosition);
					//SuperController.LogMessage($"vertex: {rb.position.x},{rb.position.y},{rb.position.z}");
					//vertexCache.Add(rb.position);
					//if (bone.meshData == null) continue;

					/// MeshFilter.mesh.....................
					//var subComponent = component.mesh;
					//var vertices = new List<Vector3>();
					//subComponent.GetVertices(vertices);
					///......................................

					/// DAZMesh.....................
					//var subComponent = component.baseMesh;
					//var vertices = subComponent.vertices;
					/// ...no mesh updates detected
					//==========
					//var subComponent = component.meshData;
					//var vertices = subComponent.UVVertices;
					/// ...subComponent is null
					//==========
					//var subComponent = component.baseMaterialVertices;
					//if (subComponent == null) { SuperController.LogMessage($"subComponent is null"); throw new Exception("subComponent is null"); }
					//if (subComponent.Length == 0) { SuperController.LogMessage($"subComponent list is empty"); throw new Exception("subComponent list is null"); }
					//SuperController.LogMessage($"component.baseMaterialVertices " + component.baseMaterialVertices.Count());
					///...requires a different kind of cache
					//==========
					var subComponent = component;
					var vertices = subComponent.baseVertices;
					/// ...no mesh updates detected
					//==========
					//var subComponent = component;
					//var vertices = subComponent.morphedUVVertices;
					/// ...no mesh updates detected
					//==========
					//var subComponent = component;
					//var vertices = subComponent.rawMorphedUVVertices;
					/// ...no mesh updates detected
					//==========
					//var subComponent = component.uvMappedMesh;
					//var vertices = subComponent.vertices;
					/// ...no mesh updates detected
					//==========
					//var subComponent = component.uvMappedMesh;
					//var vertices = new List<Vector3>();
					//subComponent.GetVertices(vertices);
					/// ...no mesh updates detected
					//==========
					//var subComponent = component;
					//var vertices = subComponent.visibleMorphedUVVertices;
					/// ...no mesh updates detected
					//==========
					//var subComponent = component.baseMesh;
					//var vertices = new List<Vector3>();
					//subComponent.GetVertices(vertices);
					/// ...no mesh updates detected
					//==========
					///......................................

					for (int i = 0; i < vertices.Length; i++)
					{
						vertexCache.Add(new Vector3(vertices[i].x, vertices[i].y, vertices[i].z));
					}

					//for (int i = 0; i < item.baseVertices.Length; i++)
					//{
					//	SuperController.LogMessage($"vertex: {mesh.baseVertices[i].x},{mesh.baseVertices[i].y},{mesh.baseVertices[i].z}");
					//	vertexCache.Add(mesh.baseVertices[i]);

					//	//System.Random randomizer = new System.Random();
					//	//var randomVector = UnityEngine.Random.insideUnitSphere;
					//	//float randomX = randomizer.Next(-500, 500);
					//	//float randomY = randomizer.Next(-500, 500) / 100;
					//	//float randomZ = randomizer.Next(-500, 500) / 100;
					//	//max = Math.Max(max, vertices[i].x);
					//	//min = Math.Min(min, vertices[i].x);
					//	//newVertices.Add(randomVector);
					//	//vertices[i] = randomVector;
					//}
					//mesh.vertices = newVertices.ToArray();
					//mesh.SetVertices(newVertices);
					//mesh.RecalculateBounds();
					//mesh.RecalculateNormals();
					//mesh.RecalculateTangents();
					//vertexCache.AddRange(vertices);
				}
				return vertexCache;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw (e);
			}
		}


	}//...public class DebugService
}//...namespace CataloggerPlugin.StatefullServices
