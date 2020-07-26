
using CataloggerPlugin.Models;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CataloggerPlugin.Services
{
	class JsonSerializer
	{

		//public static Mutation DeserializeFromFile(string fileName)
		//{
		//	var sc = SuperController.singleton;
		//	var jsonData = sc.ReadFileIntoString(fileName);
		//	var mutationsJsonObject = JSON.Parse(jsonData).AsObject;
		//	var mutationsDynamicObject = DynamicFromJsonClass(mutationsJsonObject);

		//	//var morphsDynamic = mutationsDynamicObject["MorphSet"] as Dictionary<string, dynamic>;
		//	//var morphsSet = morphsDynamic.ToDictionary(i => i.Key, i => i.Value as string);
		//	var newMutation = new Mutation();
		//	//newMutation.MorphSet = morphsSet;

		//	return newMutation;
		//}

		//public static void SerializeToFile(string fileName, Dictionary<string, string> MorphSet)
		//{
		//	var mophsNode = SerializeDictionary(MorphSet);
		//	var mutationNode = new SimpleJSON.JSONClass();
		//	mutationNode.Add("MorphSet", mophsNode);
		//	mutationNode.SaveToFile(fileName);
		//}

		//public static JSONClass SerializeDictionary(Dictionary<string, string> dict)
		//{
		//	var jn = new JSONClass();
		//	foreach (var keyvaluePair in dict)
		//	{
		//		jn.Add(keyvaluePair.Key, SerializeValue(keyvaluePair.Value));
		//	}
		//	return jn;
		//}

		//public static JSONNode SerializeValue(string value)
		//{
		//	return new JSONNode() { Value = value };
		//}

		//public static object DynamicFromJsonClass(JSONClass jsonClass)
		//{
		//	if (IsObject(jsonClass))
		//	{
		//		Dictionary<string, object> newObject = new Dictionary<string, object>();
		//		for (var i = 0; i < jsonClass.Keys.Count(); i++)
		//		{
		//			string key = jsonClass.Keys.ElementAt(i);
		//			object value = DynamicFromJsonClass(jsonClass.Childs.ElementAt(i).AsObject);
		//			newObject.Add(key, value);
		//		}
		//		return newObject;
		//	}

		//	if (IsArray(jsonClass))
		//	{
		//		List<object> newArray = new List<object>();
		//		for (var i = 0; i < jsonClass.Keys.Count(); i++)
		//		{
		//			object value = DynamicFromJsonClass(jsonClass.Childs.ElementAt(i).AsObject);
		//			newArray.Add(value);
		//		}
		//		return newArray;
		//	}

		//	return jsonClass.Value;
		//}

		//private static bool IsArray(JSONClass inClass)
		//{
		//	return inClass.Childs.Count() > 0 && inClass.Keys.Count() == 0;
		//}

		//private static bool IsObject(JSONClass inClass)
		//{
		//	return inClass.Keys.Count() > 0 && inClass.Childs.Count() == inClass.Keys.Count();
		//}

		//public static Dictionary<string, string> DictionaryFromJsonClassOfKeyValuePairs(JSONClass jsonClass)
		//{
		//	var objectDictionary = new Dictionary<string, string>();
		//	for (var i = 0; i < jsonClass.Keys.Count(); i++)
		//	{
		//		string key = jsonClass.Keys.ElementAt(i);
		//		JSONNode node = jsonClass.Childs.ElementAt(i);
		//		objectDictionary.Add(key, node.Value);
		//	}
		//	return objectDictionary;
		//}

		//public static Dictionary<string, JSONClass> DictionaryFromJsonClass(JSONClass mutationsJsonObject)
		//{
		//	var objectDictionary = new Dictionary<string, JSONClass>();
		//	for (var i = 0; i < mutationsJsonObject.Keys.Count(); i++)
		//	{
		//		string key = mutationsJsonObject.Keys.ElementAt(i);
		//		JSONClass subClass = mutationsJsonObject.Childs.ElementAt(i).AsObject;

		//		objectDictionary.Add(key, subClass);
		//	}
		//	return objectDictionary;
		//}



	}
}
