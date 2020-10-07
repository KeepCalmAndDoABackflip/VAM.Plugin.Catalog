using Battlehub.UIControls;
using juniperD.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR;

namespace juniperD.StatefullServices
{

	public class CatalogUiHelper
	{
		public Canvas canvas;
		public bool AlwaysFaceMe { get; set; } = false;
		public bool Visible { get; set; } = true;
		CatalogPlugin _context;

		public CatalogUiHelper(CatalogPlugin context, int width = 600, int height = 800, Color? color = null)
		{
			this._context = context;
			GameObject canvasObject = new GameObject();
			canvas = canvasObject.AddComponent<Canvas>();
			GraphicRaycaster rC = canvasObject.AddComponent<GraphicRaycaster>();
			//rC.blockingObjects = GraphicRaycaster.BlockingObjects.All;

			canvas.renderMode = RenderMode.WorldSpace;

			// Flip canvas so that its facing the user and not the character...
			//canvasObject.transform.rotation = Quaternion.Euler(0,180,0);

			SuperController.singleton.AddCanvas(canvas);
			//relative scale of canvas to VAM person
			float scaleX = 0.001f;
			float scaleY = 0.001f;
			float scaleZ = 0.001f;
			canvas.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

			//affect worldspace resolution
			CanvasScaler cs = canvasObject.AddComponent<CanvasScaler>();
			cs.scaleFactor = 100.0f;
			cs.dynamicPixelsPerUnit = 1f;

			//visible RectTransform
			Image i = canvasObject.AddComponent<Image>();
			i.color = color ?? new Color(0, 0, 1, 0.2f);
			i.rectTransform.sizeDelta = new Vector2(width, height);
		}

		public void Update()
		{

			if (AlwaysFaceMe)
			{
				if (XRSettings.enabled == false)
				{
					Transform cameraT = SuperController.singleton.lookCamera.transform;
					Vector3 endPos = cameraT.position + cameraT.forward * 10000000.0f;
					canvas.transform.LookAt(endPos, cameraT.up);
				}
				else
				{
					//rotate so it always faces camera
					Vector3 v = Camera.main.transform.rotation.eulerAngles;
					canvas.transform.rotation = Quaternion.Euler(0, v.y, 0);
				}
			}
			canvas.enabled = SuperController.singleton.editModeToggle.isOn && Visible;

		}

		public static void AddBorderColorToTexture(Texture2D texture, Color borderColor, int borderWidth = 20)
		{
			// Left
			for (var x = 0; x < borderWidth; x++)
			{
				for (var y = 0; y < texture.height; y++)
				{
					texture.SetPixel(x, y, borderColor);
				}
			}
			// Top
			for (var x = 0; x < texture.width; x++)
			{
				for (var y = 0; y < borderWidth; y++)
				{
					texture.SetPixel(x, y, borderColor);
				}
			}
			// Right
			for (var x = texture.width - borderWidth; x < texture.width; x++)
			{
				for (var y = 0; y < texture.height; y++)
				{
					texture.SetPixel(x, y, borderColor);
				}
			}
			// Bottom
			for (var x = 0; x < texture.width; x++)
			{
				for (var y = texture.height - borderWidth; y < texture.height; y++)
				{
					texture.SetPixel(x, y, borderColor);
				}
			}
			texture.Apply();
		}

		public static GameObject CreatePanel(GameObject parent, float width, float height, int left, int top, Color backgroundColor, Color borderColor, Texture2D texture = null)
		{
			try
			{
				GameObject panel = new GameObject("Panel");
				panel.AddComponent<CanvasRenderer>();
				panel.transform.SetParent(parent.transform, false);
				Image imageObject = panel.AddComponent<Image>();

				imageObject.color = backgroundColor;
				RectTransform r = imageObject.rectTransform;
				r.sizeDelta = new Vector2(width, height);
				var target = parent.transform.GetComponent<RectTransform>();

				if (texture != null)
				{
					Sprite mySprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
					imageObject.sprite = mySprite;
					texture.Apply();
					//string referenceField = "_MainTex" + UnityEngine.Random.value;
					//Material mat = new Material(imageObject.material);
					//imageObject.material = mat;
					//imageObject.material.mainTexture = texture;

				}
				SetAnchors(parent, panel, "topleft", left, top);
				return panel;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public Border CreateBorders(GameObject parent, int width, int height, Color initialColor, int offsetX = 0, int offsetY = 0, int borderWidth = 0)
		{
			try
			{
				var newBorder = new Border();
				newBorder.texture = new Texture2D(borderWidth, borderWidth);
				newBorder.LeftBorder = CreateImagePanel(parent, newBorder.texture, borderWidth, height + borderWidth + offsetY, width / -2 - offsetX, 0);
				newBorder.RightBorder = CreateImagePanel(parent, newBorder.texture, borderWidth, height + borderWidth + offsetY, width / 2 + offsetX, 0);
				newBorder.TopBorder = CreateImagePanel(parent, newBorder.texture, width + borderWidth + offsetX, borderWidth, 0, height / -2 - offsetY);
				newBorder.BottomBorder = CreateImagePanel(parent, newBorder.texture, width + borderWidth + offsetX, borderWidth, 0, height / 2 + offsetY);
				AddBorderColorToTexture(newBorder.texture, initialColor, borderWidth);
				return newBorder;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public GameObject CreateImagePanel(GameObject parent, Texture2D texture, int width, int height, int offsetX = 0, int offsetY = 0)
		{
			try
			{
				//AddBorderColorToTexture(texture, Color.white);
				GameObject localPanel = new GameObject("Panel");
				localPanel.transform.SetParent(parent.transform, false);
				localPanel.transform.localPosition = new Vector3(offsetX, offsetY, 0.0f);
				//===== CREATE NEW IMAGE REFERENCE ==============================================
				Image imageObject = localPanel.AddComponent<Image>();
				string referenceField = "_MainTex" + UnityEngine.Random.value;
				Material mat = new Material(imageObject.material);
				imageObject.material = mat;
				imageObject.material.mainTexture = texture;
				//===============================================================================
				RectTransform rect = imageObject.rectTransform;
				rect.sizeDelta = new Vector2(width, height);
				return localPanel;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public void ShiftTextureLeft(Texture2D texture, int xAmount)
		{
			for (var i = 0; i < xAmount; i++)
			{
				Color[] floatingSlice = new Color[texture.height];
				for (var y = 0; y < texture.height; y++)
				{
					floatingSlice[y] = texture.GetPixel(0, y);
				}
				for (var x = 0; x < texture.width; x++)
				{
					var sourcePixelX = 1 + x;
					if (sourcePixelX >= texture.width) sourcePixelX = sourcePixelX - texture.width;
					for (var y = 0; y < texture.height; y++)
					{
						Color targetColor = (sourcePixelX == 0) ? floatingSlice[y] : texture.GetPixel(sourcePixelX, y);
						texture.SetPixel(x, y, targetColor);
					}
				}
				texture.Apply();
			}

		}
		//IEnumerator OffsetMaterial(Material mat, float amount){ 
		//	yield return new WaitForEndOfFrame();
		//	mat.SetTextureOffset("_MainTex", new Vector2(amount, 0));
		//	mat.mainTextureOffset = new Vector2(amount, 0);
		//}

		public UIDynamicButton CreateClickablePanel(GameObject parent, Color normalColor, Color highlightColor, int width, int height, int offsetX = 0, int offsetY = 0, Texture2D texture = null)
		{
			UIDynamicButton localPanel = CreateButton(parent, "", width, height, offsetX, offsetY, normalColor, highlightColor, Color.white, texture);
			RectTransform r = localPanel.GetComponent<RectTransform>();
			r.sizeDelta = new Vector2(width, height);

			SetAnchors(parent, localPanel.gameObject, "bottom", offsetX, offsetY);
			return localPanel;
		}

		// Panel object. Usefull as board to anchor other objects too
		public GameObject CreateUIPanel(GameObject parent, int width = 10, int height = 10, string location = "center", int offsetX = 0, int offsetY = 0, Color? color = null)
		{
			GameObject panel = new GameObject("Panel");
			panel.AddComponent<CanvasRenderer>();
			panel.transform.SetParent(parent.transform, false);

			Image i = panel.AddComponent<Image>();
			i.color = color ?? new Color(0, 0, 0, 0f);

			RectTransform r = i.rectTransform;
			r.sizeDelta = new Vector2(width, height);

			SetAnchors(parent, panel, location, offsetX, offsetY);

			return panel;
		}

		public UIDynamicButton CreateButton(GameObject parent, string name, float width, float height, int left, int top, Color color, Color hoverColor, Color textColor, Texture2D texture = null)
		{
			var button = CreateButton(parent, name, width, height, left, top, texture);
			button.textColor = textColor;
			var colors = button.button.colors;
			colors.highlightedColor = hoverColor;
			colors.normalColor = color;
			colors.pressedColor = color;
			button.button.colors = colors;
			return button;
		}

		public DynamicDropdownField CreateDynamicDropdown(GameObject parent, string name, List<string> items, float width, float height, int left, int top, Color color, Color hoverColor, Color textColor, int? fontSize = null, int infoBoxHeight = 0, UnityAction<string> onOptionSelect = null)
		{
			var dynamicDropdown = new DynamicDropdownField();

			dynamicDropdown.label = CreateButton(parent, name, width, 20, left, top, Color.clear, new Color(0.1f,0.1f,0.1f), textColor * 0.7f);
			dynamicDropdown.label.buttonText.fontSize = (int)((fontSize ?? 20) * 0.9);
			dynamicDropdown.label.buttonText.alignment = TextAnchor.LowerLeft;

			dynamicDropdown.selectedOption = CreateButton(parent, "", width, height, left, (int)(top + 20), color, hoverColor, textColor);
			dynamicDropdown.selectedOption.buttonText.fontSize = fontSize ?? 20;
			dynamicDropdown.items = items;

			if (infoBoxHeight > 0) { 
				dynamicDropdown.infoBox = CreateTextField(parent, "", width, infoBoxHeight, left, top + (int)(height) + 20, color + new Color(0.1f,0.1f,0.1f), textColor);
				dynamicDropdown.infoBox.UItext.fontSize = (int)(fontSize * 0.9);
				dynamicDropdown.infoBox.UItext.fontStyle = FontStyle.Italic;
				dynamicDropdown.infoBox.UItext.alignment = TextAnchor.UpperLeft;	
			}

			dynamicDropdown.selectedOption.button.onClick.AddListener(() =>
			{
				ShowDropDownItems(parent, dynamicDropdown, width, height, left, top, color, hoverColor,textColor,onOptionSelect);
			});

			dynamicDropdown.label.button.onClick.AddListener(() =>
			{
				dynamicDropdown.MinimizeDynamicDropdown(_context, !dynamicDropdown.Minimized);
			});

			dynamicDropdown.onRefresh = () =>
			{
				if (dynamicDropdown.infoBox != null) dynamicDropdown.infoBox.UItext.text = string.Join("\n•", dynamicDropdown.items.ToArray());
			};

			return dynamicDropdown;
		}

		public void ShowDropDownItems(GameObject parent, DynamicDropdownField dynamicDropdown, float width, float height, int left, int top, Color color, Color hoverColor, Color textColor, UnityAction<string> onOptionSelect = null, bool refreshOnly = false)
		{

			top = top +5;

			if (dynamicDropdown.isOpen)
			{
				ClearDropdownList(dynamicDropdown);
				dynamicDropdown.isOpen = false;
				return;
			}

			try
			{
				
				var panelHeight = dynamicDropdown.items.Count * height + 10;
				var panelTop = top + height * 2 - 5;
				dynamicDropdown.backpanel = CreatePanel(parent, width + 10, panelHeight, left - 5, (int)(panelTop), color + new Color(0.5f, 0.3f, 0.3f), Color.white);

				dynamicDropdown.options = new Dictionary<UIDynamicButton, string>();
				var itemTop = top + (height * 2);
				foreach (var option in dynamicDropdown.items)
				{
					//if (dynamicDropdown.selectedValue == option) continue;
					var optionButton = CreateButton(parent, option, width, height, left, (int)itemTop, color + new Color(0.1f, 0.1f, 0.1f), hoverColor + new Color(0.1f, 0.1f, 0.1f), textColor + new Color(0.1f, 0.1f, 0.1f));
					optionButton.buttonText.fontSize = dynamicDropdown.label.buttonText.fontSize;
					optionButton.button.onClick.AddListener(() =>
					{
						try
						{
							dynamicDropdown.selectedOption.buttonText.text = option;
							dynamicDropdown.selectedValue = option;
							ClearDropdownList(dynamicDropdown);
							dynamicDropdown.isOpen = false;
							onOptionSelect.Invoke(option);
						}
						catch (Exception e)
						{
							SuperController.LogError(e.ToString());
						}
					});
					dynamicDropdown.options.Add(optionButton, option);
					itemTop += height;
				}
				dynamicDropdown.isOpen = true;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public DynamicListBox CreateDynamicListField(GameObject parent, string name, List<string> items, float width, float height, int left, int top, Color color, Color hoverColor, Color textColor, int? fontSize = null, UnityAction<string> onItemSelect = null)
		{
			var dynamicListBox = new DynamicListBox();
			
			dynamicListBox.label = CreateButton(parent, name, width, height, left, top, Color.clear, Color.clear, textColor);
			dynamicListBox.label.buttonText.alignment = TextAnchor.LowerLeft;
			dynamicListBox.label.buttonText.fontSize = fontSize ?? 20;
			dynamicListBox.items = items;
			dynamicListBox.onRefresh = () =>
			{
				try
				{
					foreach (var prevOption in dynamicListBox.itemButtons)
					{
						if (prevOption.Key == null) return;
						_context.RemoveButton(prevOption.Key);
					}

					dynamicListBox.itemButtons = new Dictionary<UIDynamicButton, string>();
					var itemTop = top + (height);
					foreach (var item in dynamicListBox.items)
					{
						var optionButton = CreateButton(parent, item, width, height, left, (int)itemTop, color + new Color(0.1f, 0.1f, 0.1f), hoverColor + new Color(0.1f, 0.1f, 0.1f), textColor + new Color(0.1f, 0.1f, 0.1f));
						optionButton.buttonText.fontSize = dynamicListBox.label.buttonText.fontSize;
						optionButton.button.onClick.AddListener(() =>
						{
							try
							{
								onItemSelect.Invoke(item);
							}
							catch (Exception e)
							{
								SuperController.LogError(e.ToString());
							}
						});
						dynamicListBox.itemButtons.Add(optionButton, item);
						itemTop += height;
					}
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			return dynamicListBox;
		}

		public void ClearDropdownList(DynamicDropdownField dynamicDropdown)
		{
			foreach (var prevOption in dynamicDropdown.options)
			{
				if (prevOption.Key == null) return;
				_context.RemoveButton(prevOption.Key);
			}			
			GameObject.Destroy(dynamicDropdown.backpanel);
			dynamicDropdown.backpanel = null;
			//var backpanelRect = dynamicDropdown.backpanel.GetComponents<RectTransform>().First();
			//backpanelRect.sizeDelta = new Vector2(backpanelRect.rect.width, 0);
			//dynamicDropdown.options = new Dictionary<UIDynamicButton, string>();
		}

		//public UIDynamicPopup CreatePopupField(GameObject parent, string name, string[] options, float width, float height, int left, int top, Color backColor, Color textColor)
		//{
		//	Transform field = GameObject.Instantiate<Transform>(_context.manager.configurablePopupPrefab);

		//	field.transform.localPosition = new Vector3(left, top, 0.0f);
		//	field.SetParent(parent.transform, false);

		//	RectTransform rt = field.GetComponent<RectTransform>();
		//	rt.sizeDelta = new Vector2(width, height);

		//	RectTransform parentRectTransform = field.GetComponent<RectTransform>();
		//	SetAnchors(parent, field.gameObject, "topleft", left, top);

		//	UIDynamicPopup uiField = field.GetComponent<UIDynamicPopup>();
		//	uiField.label = name;
		//	uiField.name = name;
		//	options = new[] { "Test1", "Test2"};
		//	uiField.popup.numPopupValues = options.Length;
		//	for (var i = 0; i <  options.Length; i++)
		//	{
		//		uiField.popup.setPopupValue(i, options[i] );
		//		uiField.popup.setDisplayPopupValue(i, options[i]);
		//	}
		//	//uiField.popup.normalColor = backColor;
		//	//uiField.labelTextColor = textColor;
		//	//uiField.popupPanelHeight = 300;
		//	//uiField.labelWidth = 200;
		//	//uiField.popup.
		//	uiField.popup.normalBackgroundColor = backColor;
		//	uiField.popup.showSlider = true;
		//	uiField.popup.visible = true;
		//	uiField.popup.popupPanel.position = field.position;
		//	uiField.popup.normalColor = backColor;
		//	uiField.height = 200;
		//	uiField.popup.onOpenPopupHandlers = () => { 
		//	};

		//	return uiField;
		//}

		public UIDynamicTextField CreateTextField(GameObject parent, string name, float width, float height, int left, int top, Color backColor, Color textColor, Texture2D texture = null)
		{
			Transform textField = GameObject.Instantiate<Transform>(_context.manager.configurableTextFieldPrefab);

			textField.transform.localPosition = new Vector3(left, top, 0.0f);
			textField.SetParent(parent.transform, false);

			RectTransform rt = textField.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(width, height);

			RectTransform parentRectTransform = textField.GetComponent<RectTransform>();
			SetAnchors(parent, textField.gameObject, "topleft", left, top);

			UIDynamicTextField uiTextField = textField.GetComponent<UIDynamicTextField>();
			uiTextField.text = name;

			uiTextField.backgroundColor = backColor;
			uiTextField.textColor = textColor;

			return uiTextField;
		}

		public UIDynamicToggle CreateToggleField(GameObject parent, string name, float width, float height, int left, int top, Color backColor, Color textColor, Texture2D texture = null)
		{
			Transform fieldTransform = GameObject.Instantiate<Transform>(_context.manager.configurableTogglePrefab);

			fieldTransform.transform.localPosition = new Vector3(left, top, 0.0f);
			fieldTransform.SetParent(parent.transform, false);

			RectTransform rt = fieldTransform.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(width, height);

			RectTransform parentRectTransform = fieldTransform.GetComponent<RectTransform>();
			SetAnchors(parent, fieldTransform.gameObject, "topleft", left, top);

			UIDynamicToggle field = fieldTransform.GetComponent<UIDynamicToggle>();
			field.labelText.text = name;

			field.backgroundColor = backColor;
			field.textColor = textColor;

			return field;
		}

		public UIDynamicButton CreateButton(GameObject parent, string name, float width = 100, float height = 80, int left = 0, int top = 0, Texture2D texture = null)
		{
			Transform button = GameObject.Instantiate<Transform>(_context.manager.configurableButtonPrefab);

			button.transform.localPosition = new Vector3(left, top, 0.0f);
			button.SetParent(parent.transform, false);

			RectTransform rt = button.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(width, height);

			RectTransform parentRectTransform = button.GetComponent<RectTransform>();
			SetAnchors(parent, button.gameObject, "topleft", left, top);

			UIDynamicButton uiButton = button.GetComponent<UIDynamicButton>();
			uiButton.label = name;

			if (texture != null)
			{
				Sprite mySprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
				uiButton.buttonImage.sprite = mySprite;
			}
			return uiButton;
		}

		public void OnDestroy()
		{
			SuperController.singleton.RemoveCanvas(canvas);
			GameObject.Destroy(canvas.gameObject);
		}

		public static void SetAnchors(GameObject parent, GameObject child, string placement = "center", int offsetX = 0, int offsetY = 0)
		{
			var parentRect = parent.GetComponent<RectTransform>();
			var childRect = child.GetComponent<RectTransform>();
			SetAnchors(parentRect, childRect, placement, offsetX, offsetY);
		}


		// General helper functions
		// Helper to position child relative to target, if no target supplied then parent is used
		public static void SetAnchors(RectTransform parent, RectTransform child, string placement = "center", int offsetX = 0, int offsetY = 0, int knownWidth = -1)
		{
			if (parent == null)
			{
				parent = child.parent.GetComponent<RectTransform>();
			}

			switch (placement)
			{
				case "center":
					child.transform.localPosition = new Vector3(0, 0, 0);
					break;
				case "left":
					child.transform.localPosition = new Vector2(parent.transform.localPosition.x + offsetX, 0 + offsetY);
					break;
				case "right":
					if (knownWidth > -1)
						child.transform.localPosition = new Vector2(parent.transform.localPosition.x - knownWidth/2 + offsetX, 0);
					else
						child.transform.localPosition = new Vector2(parent.rect.xMax - child.rect.xMax + offsetX, 0);
					break;
				case "top":
					child.transform.localPosition = new Vector2(offsetX, parent.rect.yMax - child.rect.yMax + offsetY);
					break;
				case "bottom":
					child.transform.localPosition = new Vector2(offsetX, parent.rect.yMin - child.rect.yMin + offsetY);
					break;

				case "topleft":
					child.transform.localPosition = new Vector2(parent.rect.xMin - child.rect.xMin + offsetX, parent.rect.yMax - child.rect.yMax - offsetY);
					break;
				case "topright":
					child.transform.localPosition = new Vector2(parent.rect.xMax - child.rect.xMax + offsetX, parent.rect.yMax - child.rect.yMax - offsetY);
					break;

				case "bottomleft":
					child.transform.localPosition = new Vector2(parent.rect.xMin - child.rect.xMin + offsetX, parent.rect.yMin - child.rect.yMin + offsetY);
					break;
				case "bottomright":
					child.transform.localPosition = new Vector2(parent.rect.xMax - child.rect.xMax + offsetX, parent.rect.yMin - child.rect.yMin + offsetY);
					break;
				//Outside Edges
				case "leftout":
					child.transform.localPosition = new Vector2(parent.rect.xMin + child.rect.xMin + offsetX, 0 + offsetY);
					break;
				case "rightout":
					child.transform.localPosition = new Vector2(parent.rect.xMax + child.rect.xMax + offsetX, 0 - offsetY);
					break;
				case "topout":
					child.transform.localPosition = new Vector2(offsetX, parent.rect.yMax + child.rect.yMax + offsetX);
					break;
				case "bottomout":
					child.transform.localPosition = new Vector2(offsetX, parent.rect.yMin + child.rect.yMin + offsetX);
					break;
			}

		}

		public HorizontalLayoutGroup CreateHorizontalLayout(GameObject gameObject, float spacing = 0f, bool childControlWidth = true, bool childControlHeight = true, bool childForceExpandHeight = true, bool childForceExpandWidth = true)
		{
			HorizontalLayoutGroup hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
			hlg.spacing = spacing;
			hlg.childControlWidth = childControlWidth;
			hlg.childControlHeight = childControlHeight;
			hlg.childForceExpandHeight = childForceExpandHeight;
			hlg.childForceExpandWidth = childForceExpandWidth;
			return hlg;
		}
		public VerticalLayoutGroup CreateVerticalLayout(GameObject gameObject, float spacing = 0f, bool childControlWidth = true, bool childControlHeight = true, bool childForceExpandHeight = true, bool childForceExpandWidth = true)
		{
			VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
			vlg.spacing = spacing;
			vlg.childControlWidth = childControlWidth;
			vlg.childControlHeight = childControlHeight;
			vlg.childForceExpandHeight = childForceExpandHeight;
			vlg.childForceExpandWidth = childForceExpandWidth;
			return vlg;
		}

		private void SetLayerRecursively(GameObject go, int layer)
		{
			go.layer = layer;
			Transform t = go.transform;
			for (int i = 0; i < t.childCount; i++)
			{
				SetLayerRecursively(t.GetChild(i).gameObject, layer);
			}
		}

	}


}
