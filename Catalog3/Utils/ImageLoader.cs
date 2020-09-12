
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace juniperD.Utils
{
	public class ImageLoader
	{
		private Dictionary<string,Texture2D> _pathAndTextures = new Dictionary<string, Texture2D>();


		public Texture2D GetFutureImageFromFileOrCached(string filePath, int width = 32, int height = 32)
		{
			if (_pathAndTextures.ContainsKey(filePath)) {
				return _pathAndTextures[filePath];
			}
			Texture2D futureTexture = new Texture2D(width, height);
			_pathAndTextures.Add(filePath, futureTexture);
			UnityAction<Texture2D> imageLoadedCallback = (texture) => {
				futureTexture.Resize(texture.width, texture.height);
				futureTexture.SetPixels(texture.GetPixels());
				futureTexture.Apply();
			};
			LoadImage(filePath, imageLoadedCallback);
			return futureTexture;
		}

		public static Texture2D GetFutureImageFromFile(string filePath, int width = 32, int height = 32)
		{
			Texture2D futureTexture = new Texture2D(width, height);
			UnityAction<Texture2D> imageLoadedCallback = (texture) => {
				futureTexture.Resize(texture.width, texture.height);
				futureTexture.SetPixels(texture.GetPixels());
				futureTexture.Apply();
			};
			LoadImage(filePath, imageLoadedCallback);
			return futureTexture;
		}

		public static void LoadImage(string filePath, UnityAction<Texture2D> onImageLoaded)
		{
			ImageLoaderThreaded.QueuedImage queuedImage = new ImageLoaderThreaded.QueuedImage();
			queuedImage.imgPath = filePath;
			queuedImage.forceReload = true;
			queuedImage.skipCache = true;
			queuedImage.compress = true;
			queuedImage.createMipMaps = false;
			queuedImage.isNormalMap = false;
			queuedImage.linear = false;
			queuedImage.createAlphaFromGrayscale = false;
			queuedImage.createNormalFromBump = false;
			queuedImage.bumpStrength = 0f;
			queuedImage.isThumbnail = false;
			queuedImage.fillBackground = false;
			queuedImage.invert = false;
			queuedImage.callback = (ImageLoaderThreaded.QueuedImage image) =>
			{
				onImageLoaded.Invoke(image.tex);
			};
			ImageLoaderThreaded.singleton.QueueImage(queuedImage);
		}

		public static Texture2D TextureFromRawData(byte[] rawData, int width, int height, TextureFormat textureFormat)
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

		//public static Texture2D LoadImageFromFileDevOnly(string filePath, int width = 32, int height = 32)
		//{
		//	Texture2D texture;
		//	texture = TextureLoader.LoadTexture(filePath);
		//	if (texture == null) texture = new Texture2D(width, height);
		//	return texture;
		//}

		//public static void AssignImageFromFile(Texture2D textureToAssignTo, string filePath)
		//{
		//	UnityAction<Texture2D> imageLoadedCallback = (texture) => {
		//		textureToAssignTo.SetPixels(texture.GetPixels());
		//		textureToAssignTo.Apply();
		//	};
		//	LoadImage(filePath, imageLoadedCallback);
		//}

		//public static Texture2D LoadImageFromResources(Dictionary<string,Texture2D> resources, string filePath)
		//{
		//	if (!resources.ContainsKey(filePath))
		//	{
		//		SuperController.LogError("WARNING: Resource not loaded: " + filePath);
		//		return new Texture2D(32, 32);
		//	}
		//	return resources[filePath];
		//}

		//public static Texture2D LoadRgbaImageFromFile(string filePath, int width = 32, int height = 32)
		//{
		//	Texture2D texture = null;
		//	var filename = filePath.Split('/').Last();
		//	var bytes = FileManagerSecure.ReadAllBytes(filePath);
		//	var getWidthAndHeightFragmentFrmFilename = filename.
		//	texture = TextureFromRawData(bytes, )
		//	//var texture = TextureLoader.LoadTexture(filePath);
		//	if (texture == null) LoadTextureFromFile(filePath, width, height);
		//	if (texture == null) texture = new Texture2D(width, height);
		//	return texture;
		//}

		//public void ConvertPngToRgba(string pngPath)
		//{
		//	Texture2D image = Helpers.LoadImageFromResource(_resourceTextures, pngPath);
		//	var pathElements = pngPath.Split('/');
		//	var fileName = pathElements.Last();
		//	var fileDirectoryPath = string.Join("/", pathElements.Take(pathElements.Count() - 1).ToArray());
		//	var newFileName = $"{fileName}-{image.width}x{image.height}.{TextureFormat.RGBA32}";
		//	var newFilePath = $"{fileDirectoryPath}/{newFileName}";
		//	byte[] bytes = image.GetRawTextureData();
		//	FileManagerSecure.WriteAllBytes(newFilePath, bytes);
		//}

		//public static Texture2D LoadImage(string filePath)
		//{
		//	ImageLoaderThreaded.QueuedImage queuedImage = new ImageLoaderThreaded.QueuedImage();
		//	queuedImage.imgPath = filePath;
		//	queuedImage.forceReload = true;
		//	queuedImage.skipCache = true;
		//	queuedImage.compress = true;
		//	queuedImage.createMipMaps = false;
		//	queuedImage.isNormalMap = false;
		//	queuedImage.linear = false;
		//	queuedImage.createAlphaFromGrayscale = false;
		//	queuedImage.createNormalFromBump = false;
		//	queuedImage.bumpStrength = 0f;
		//	queuedImage.isThumbnail = false;
		//	queuedImage.fillBackground = false;
		//	queuedImage.invert = false;
		//	Texture2D texture = null;
		//	queuedImage.callback = (ImageLoaderThreaded.QueuedImage image) =>
		//	{
		//		texture = image.tex;
		//	};
		//	ImageLoaderThreaded.singleton.QueueImage(queuedImage);
		//	var startTime = DateTime.Now;
		//	while (texture == null)
		//	{
		//		if ((DateTime.Now - startTime).Seconds > 10)
		//		{
		//			SuperController.LogError("Timed out waiting for image to load");
		//		}
		//	}
		//	return texture;
		//}




		//async Texture2D LoadImageAsync(string filePath)
		//{
		//	ImageLoaderThreaded.QueuedImage queuedImage = new ImageLoaderThreaded.QueuedImage();
		//	queuedImage.imgPath = filePath;
		//	queuedImage.forceReload = true;
		//	queuedImage.skipCache = true;
		//	queuedImage.compress = true;
		//	queuedImage.createMipMaps = false;
		//	queuedImage.isNormalMap = false;
		//	queuedImage.linear = false;
		//	queuedImage.createAlphaFromGrayscale = false;
		//	queuedImage.createNormalFromBump = false;
		//	queuedImage.bumpStrength = 0f;
		//	queuedImage.isThumbnail = false;
		//	queuedImage.fillBackground = false;
		//	queuedImage.invert = false;
		//	queuedImage.callback = (ImageLoaderThreaded.QueuedImage qi) =>
		//	{
		//		onImageLoaded.Invoke(qi.tex);
		//		//Texture2D tex = qi.tex;
		//	};
		//	await Task.Create(Action<Task> () => {
		//		ImageLoaderThreaded.singleton.QueueImage(queuedImage);
		//	});
		//	UniTask<Texture2D> x = null;
		//}

	}
}
