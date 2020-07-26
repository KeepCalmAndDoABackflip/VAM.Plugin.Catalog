using PluginBuilder.Models;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CataloggerPlugin.StatefullServices
{

	public class UIHelper
	{
		string VERSION = "0.1";
		public Canvas canvas;
		public bool AlwaysFaceMe {get; set; } = false;
		public bool Visible { get; set; } = true;
		MVRScript plugin;

		public UIHelper(MVRScript plugin, int width = 600, int height = 800, Color? color = null)
		{
			this.plugin = plugin;
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

		public static GameObject CreatePanel(GameObject parent, int x, int y, int width, int height, Color backgroundColor, Color borderColor)
		{
			try
			{
				GameObject panel = new GameObject("Panel");
				panel.AddComponent<CanvasRenderer>();
				panel.transform.SetParent(parent.transform, false);
				Image i = panel.AddComponent<Image>();
				i.color = backgroundColor;
				RectTransform r = i.rectTransform;
				r.sizeDelta = new Vector2(width, height);
				var target = parent.transform.GetComponent<RectTransform>();
				SetAnchors(parent, panel, "topleft", x, y);
				return panel;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public Border CreateBorders(GameObject parent, int width, int height, int offsetX = 0, int offsetY = 0, int borderWidth = 0)
		{
			try
			{
				var newBorder = new Border();
				newBorder.texture = new Texture2D(borderWidth, borderWidth);
				newBorder.LeftBorder = CreateImagePanel(parent, newBorder.texture, borderWidth, height + borderWidth + offsetY, width/-2 - offsetX, 0);
				newBorder.RightBorder = CreateImagePanel(parent, newBorder.texture, borderWidth, height + borderWidth + offsetY, width/2 + offsetX, 0);
				newBorder.TopBorder = CreateImagePanel(parent, newBorder.texture, width + borderWidth + offsetX, borderWidth, 0, height/-2 - offsetY);
				newBorder.BottomBorder = CreateImagePanel(parent, newBorder.texture, width + borderWidth + offsetX, borderWidth, 0, height/2 + offsetY);
				AddBorderColorToTexture(newBorder.texture, Color.white, borderWidth);
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
			try {
				//AddBorderColorToTexture(texture, Color.white);
				GameObject localPanel = new GameObject("Panel");
				localPanel.transform.SetParent(parent.transform, false);
				localPanel.transform.localPosition = new Vector3(offsetX, offsetY, 0.0f);
				//===== CREATE NEW IMAGE REFERENCE ==============================================
				Image imageObject = localPanel.AddComponent<Image>();
				string ReferenceField = "_MainTex" + UnityEngine.Random.value;
				Material mat = new Material(imageObject.material);
				mat.SetTexture(ReferenceField, texture);
				imageObject.material = mat;
				imageObject.material.mainTexture = texture;
				//===============================================================================

				RectTransform rect = imageObject.rectTransform;
				rect.sizeDelta = new Vector2(width, height);
				return localPanel;
			}
			catch(Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		public UIDynamicButton CreateClickablePanel(GameObject parent, Color normalColor, Color highlightColor, int width, int height, int offsetX = 0, int offsetY = 0, string imagePath = null)
		{

			if (imagePath != null)
			{
				var imageTexture = TextureLoader.LoadTexture(imagePath);
				CreateImagePanel(parent, imageTexture, width, height, 0, 0);
			}

			UIDynamicButton localPanel = CreateButton(parent, "", width, height, offsetX, offsetY, normalColor, highlightColor, Color.white);
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
			//panel.AddComponent<ConfigurableJoint>();
			//panel.AddComponent<FreeControllerV3>();
			//panel.AddComponent<Rigidbody>();

			Image i = panel.AddComponent<Image>();
			i.color = color ?? new Color(0, 0, 0, 0f);

			RectTransform r = i.rectTransform;
			r.sizeDelta = new Vector2(width, height);

			SetAnchors(parent, panel, location, offsetX, offsetY);

			return panel;
		}

		public UIDynamicButton CreateButton(GameObject parent, string name, float width, float height, int left, int top, Color color, Color hoverColor, Color textColor)
		{
			var button = CreateButton(parent, name, width, height, left, top);
			button.textColor = textColor;
			var colors = button.button.colors;
			colors.highlightedColor = hoverColor;
			colors.normalColor = color;
			colors.pressedColor = color;
			button.button.colors = colors;
			return button;
		}

		public UIDynamicButton CreateButton(GameObject parent, string name, float width = 100, float height = 80, int left = 0, int top = 0)
		{
			Transform button = GameObject.Instantiate<Transform>(plugin.manager.configurableButtonPrefab);

			button.transform.localPosition = new Vector3(left, top, 0.0f);
			button.SetParent(parent.transform, false);

			RectTransform rt = button.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(width, height);

			RectTransform parentRectTransform = button.GetComponent<RectTransform>();
			SetAnchors(parent, button.gameObject, "topleft", left, top);

			UIDynamicButton uiButton = button.GetComponent<UIDynamicButton>();
			uiButton.label = name;

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
		public static void SetAnchors(RectTransform parent, RectTransform child, string placement = "center", int offsetX = 0, int offsetY = 0)
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
					child.transform.localPosition = new Vector3(parent.rect.xMin - child.rect.xMin + offsetX, 0 + offsetY);
					break;
				case "right":
					child.transform.localPosition = new Vector3(parent.rect.xMax - child.rect.xMax + offsetX, 0 - offsetY);
					break;
				case "top":
					child.transform.localPosition = new Vector3(0, parent.rect.yMax - child.rect.yMax + offsetX, 0 - offsetY);
					break;
				case "bottom":
					child.transform.localPosition = new Vector3(0, parent.rect.yMin - child.rect.yMin + offsetX, 0 + offsetY);
					break;

				case "topleft":
					child.transform.localPosition = new Vector3(parent.rect.xMin - child.rect.xMin + offsetX, parent.rect.yMax - child.rect.yMax - offsetY);
					break;
				case "topright":
					child.transform.localPosition = new Vector3(parent.rect.xMax - child.rect.xMax + offsetX, parent.rect.yMax - child.rect.yMax - offsetY);
					break;

				case "bottomleft":
					child.transform.localPosition = new Vector3(parent.rect.xMin - child.rect.xMin + offsetX, parent.rect.yMin - child.rect.yMin + offsetY);
					break;
				case "bottomright":
					child.transform.localPosition = new Vector3(parent.rect.xMax - child.rect.xMax + offsetX, parent.rect.yMin - child.rect.yMin + offsetY);
					break;
				//Outside Edges
				case "leftout":
					child.transform.localPosition = new Vector3(parent.rect.xMin + child.rect.xMin + offsetX, 0 + offsetY);
					break;
				case "rightout":
					child.transform.localPosition = new Vector3(parent.rect.xMax + child.rect.xMax + offsetX, 0 - offsetY);
					break;
				case "topout":
					child.transform.localPosition = new Vector3(0, parent.rect.yMax + child.rect.yMax + offsetX, 0 - offsetY);
					break;
				case "bottomout":
					child.transform.localPosition = new Vector3(0, parent.rect.yMin + child.rect.yMin + offsetX, 0 + offsetY);
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
