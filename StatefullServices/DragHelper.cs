using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

namespace CataloggerPlugin.StatefullServices
{
	public class DragHelper
	{

		public Vector3 CurrentPosition { get; internal set;}
		public bool IsIn3DSpace { get; set; } = true;
		public float XMultiplier { get; set; } = 1;
		public float YMultiplier { get; set; } = 1;
		public Vector2? LimitX = null; // limit: left, right
		public Vector2? LimitY = null; // limit: up, down
		public bool AllowDragX = true;
		public bool AllowDragY = true;
		public GameObject ObjectToDrag;

		bool _isDraggingObject = false;
		bool _dragX;
		bool _dragY;
		bool _dragStarted = false;
		bool _vrMode = XRDevice.isPresent;
		
		Vector3 _controllerInitialPosition = Vector3.zero;
		Vector3 _draggedObjectInitialPosition = Vector3.zero;
		GameObject _objectToDrag = null;
		//hold all created transforms for non canvas parented objects
		Action _finishedDraggingCallback = null;
		Func<float, float, bool> _whileDragCallback = null;

		private void StartDragging(GameObject objectToDrag, bool dragX = true, bool dragY = true, bool in3DSpace = true)
		{
			_objectToDrag = objectToDrag;
			_isDraggingObject = true;
			_dragX = dragX;
			_dragY = dragY;
			IsIn3DSpace = in3DSpace;
		}

		public void AddMouseDraggingToObject(GameObject triggerObject, GameObject dragObject, bool dragX = true, bool dragY = true, Action beforeDrag = null, Action finishedDragCallback = null, Func<float, float, bool> whileDrag_newX_newY = null)
		{
			_finishedDraggingCallback = finishedDragCallback;
			_whileDragCallback = whileDrag_newX_newY;
			AllowDragX = dragX;
			AllowDragY = dragY;
			ObjectToDrag = dragObject;
			var existingDownTrigger = triggerObject.GetComponents<EventTrigger>().FirstOrDefault(t => t.name == "TriggerOnMouseDown");
			EventTrigger triggerDown = existingDownTrigger ?? triggerObject.AddComponent<EventTrigger>();
			triggerDown.name = "TriggerOnMouseDown";
			var pointerDown = triggerDown.triggers.FirstOrDefault(t => t.eventID == EventTriggerType.BeginDrag) ?? new EventTrigger.Entry();
			pointerDown.eventID = EventTriggerType.BeginDrag;
			pointerDown.callback.RemoveAllListeners();
			pointerDown.callback.AddListener((e) =>
			{
				if (beforeDrag != null) beforeDrag.Invoke();
				StartDragging(ObjectToDrag, AllowDragX, AllowDragY, IsIn3DSpace);
			});
			triggerDown.triggers.RemoveAll(t => t.eventID == EventTriggerType.BeginDrag);
			triggerDown.triggers.Add(pointerDown);

			var existingUpTrigger = triggerObject.GetComponents<EventTrigger>().FirstOrDefault(t => t.name == "TriggerMouseUp");
			EventTrigger triggerUp = existingUpTrigger ?? triggerObject.AddComponent<EventTrigger>();
			triggerUp.name = "TriggerMouseUp";
			var pointerUp = triggerUp.triggers.FirstOrDefault(t => t.eventID == EventTriggerType.EndDrag) ?? new EventTrigger.Entry();
			pointerUp.eventID = EventTriggerType.EndDrag;
			pointerUp.callback.RemoveAllListeners();
			pointerUp.callback.AddListener((e) =>
			{
				StopDragging();
				if (finishedDragCallback != null) _finishedDraggingCallback.Invoke();
			});
			triggerUp.triggers.RemoveAll(t =>t.eventID == EventTriggerType.EndDrag);
			triggerUp.triggers.Add(pointerUp);
		}

		private void StopDragging()
		{
			_isDraggingObject = false;
		}

		private Vector3 GetDragLocationForElement(float startX, float startY, float startZ)
		{

			if (_dragStarted == false)
			{
				_dragStarted = true;
				_controllerInitialPosition = _vrMode 
					? new Vector3(-InputTracking.GetLocalPosition(XRNode.RightHand).x, InputTracking.GetLocalPosition(XRNode.RightHand).y, -InputTracking.GetLocalPosition(XRNode.RightHand).z)
					: new Vector3(Input.mousePosition.x, Input.mousePosition.y, startZ);
				_draggedObjectInitialPosition = new Vector3(startX, startY, startZ);
				return new Vector3(startX, startY, startZ);
			}
			else
			{
				var newControllerPosition = _vrMode
					? new Vector3(-InputTracking.GetLocalPosition(XRNode.RightHand).x, InputTracking.GetLocalPosition(XRNode.RightHand).y, -InputTracking.GetLocalPosition(XRNode.RightHand).z)
					: new Vector3(Input.mousePosition.x, Input.mousePosition.y, startZ);
				var moveLenX = newControllerPosition.x - _controllerInitialPosition.x;
				var moveLenY = newControllerPosition.y - _controllerInitialPosition.y;
				var moveLenZ = newControllerPosition.z - _controllerInitialPosition.z;

				float newCanvasPosX;
				float newCanvasPosY;
				float newCanvasPosZ = startZ;
				if (_vrMode)
				{
					float dragRatioX = moveLenX * 2 * XMultiplier;
					float dragRatioY = moveLenY * 2 * YMultiplier;
					newCanvasPosX = _dragX ? _draggedObjectInitialPosition.x + dragRatioX : _draggedObjectInitialPosition.x; //canvas.transform.position.z * 70); // ... *-1 because x is inverted
					newCanvasPosY = _dragY ? _draggedObjectInitialPosition.y + dragRatioY : _draggedObjectInitialPosition.y; //canvas.transform.position.z * 70) * -1;
					return new Vector3(newCanvasPosX, newCanvasPosY, startZ);
				}
				else //...Desktop mode
				{
					float dragRatioX = moveLenX / Screen.width * -4 * XMultiplier;
					float dragRatioY = moveLenY / Screen.height * 4 * YMultiplier;
					newCanvasPosX = _dragX ? _draggedObjectInitialPosition.x + (dragRatioX) : _draggedObjectInitialPosition.x; //canvas.transform.position.z * 70); // ... *-1 because x is inverted
					newCanvasPosY = _dragY ? _draggedObjectInitialPosition.y + (dragRatioY) : _draggedObjectInitialPosition.y; //canvas.transform.position.z * 70) * -1;
				}

				return new Vector3(newCanvasPosX, newCanvasPosY, newCanvasPosZ);
			}
		}


		public void Update()
		{
			if (_isDraggingObject)
			{
				
				Vector3 newPos = GetDragLocationForElement(_objectToDrag.transform.localPosition.x, _objectToDrag.transform.localPosition.y, _objectToDrag.transform.localPosition.z);
				if (LimitX != null) {
					Vector2 limitX = LimitX ?? Vector2.zero;
					if (newPos.x < limitX.x) newPos.x = limitX.x;
					if (newPos.x > limitX.y) newPos.x = limitX.y; // LimitX.x is X1 and LimitX.y is X2
				}
				if (LimitY != null) {
					Vector2 limitY = LimitY ?? Vector2.zero;
					if (newPos.y > limitY.x) newPos.y = limitY.x; // LimitY.x is Y1 and LimitY.y is Y2
					if (newPos.y < limitY.y) newPos.y = limitY.y; 
				}
				CurrentPosition = newPos;
				_objectToDrag.transform.localPosition = new Vector3(newPos.x, newPos.y, newPos.z);
				if (_whileDragCallback != null) _whileDragCallback.Invoke(newPos.x, newPos.y);
			}
			else
			{
				_dragStarted = false;
			}
		}
	}


}
