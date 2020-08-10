using System.Collections.Generic;

namespace juniperD
{

	public class PredefinedMorphSets
	{
		public Dictionary<string, List<string>> Sets = new Dictionary<string, List<string>>();

		public PredefinedMorphSets()
		{
			Sets.Add("Eyes", new List<string>() {
					"Eyes Almond Inner",
					"Eyes Almond Outer",
					"Eyes Angle",
					"Eyes Bags",
					"Eyes Cornea Bulge",
					"Eyes Depth",
					"Eyes Height",
					"Eyes Height Bottom",
					"Eyes Height Inner",
					"Eyes Height Upper",
					"Eyes Inner Shape",
					"Eyes Inner Corner Height",
					"Eyes Inner Corner Width",
					"Eyes Inner Depth",
					"Eyes Iris Correction",
					"Eyes Iris Size",
					"Eyes Outer Shape",
					"Eyes Puffy Lower",
					"Eyes Puffy Outer",
					"Eyes Puffy Shift",
					"Eyes Puffy Upper",
					"Eyes Puffy Upper Center",
					"Eyes Round",
					"Eyes Round Lower",
					"Eyes Round Upper",
					"Eyes Shift Lower",
					"Eyes Shift Upper",
					"Eyes Size",
					"Eyes Slant Outer",
					"Eyes Upper Shape",
					"Eyes Width",
					"Eyes Wrinkle"
				});


			Sets.Add("Mouth", new List<string>(){
					"Lower Mouth Puffy",
					"Mouth Corner Depth",
					"Mouth Corner Height",
					"Mouth Corner Width",
					"Mouth Curves",
					"Mouth Curves Arch",
					"Mouth Curves Arch Corner",
					"Mouth Curves Center Width",
					"Mouth Curves Corner",
					"Mouth Depth",
					"Mouth Height",
					"Mouth Marionette lines",
					"Mouth Mousey",
					"Mouth Side Crease B",
					"Mouth Size",
					"Mouth Width",
					"Mouth_UpLipDefine",
					"MouthArea_InOut",
					"MouthCenterXScale",
					"MouthCornerShape",
					"MouthCurves1",
					"MouthCurves2",
					"MouthCurves1",
					"MouthLarger1"
				});

			Sets.Add("Lips", new List<string>(){
					"CurvyLips",
					"Lips Bottom Full",
					"Lips Bottom Shape",
					"Lips Bottom Small",
					"Lips Bow Height",
					"Lips Bow Shape",
					"Lips Center Angle",
					"Lips Corner Pinch",
					"Lips Depth",
					"Lips Edge Define",
					"Lips Heart",
					"Lips Pucker (REN)",
					"Lips Square",
					"Lips Thin",
					"Lips Top Full",
					"LIps Top Width",
					"Lips Upper Bow",
					"Lips Upper Center Depth",
					"Lips Upper Curves Round",
					"Lips Upper Curves Corner",
					"LipsPuffOut",
					"MouthLowLipShape",
				});

			Sets.Add("Jaw", new List<string>(){
					"Jaw Angle",
					"Jaw Chin Shape",
					"Jaw Corner Height",
					"Jaw Corner Shape",
					"Jaw Corner Width",
					"Jaw Curve",
					"Jaw Define",
					"Jaw Height",
					"Jaw Line Depth",
					"Jaw Size",
					"Jaw Square",
					"JawCornerForward",
					"JawEarForward",
					"JawHeart",
					"JawlineRecede",
					"JawlineReduce",
					"JawlineRecede",
					"JawIn-Out"
				});

			Sets.Add("Chin", new List<string>(){
					"Chin Cleft",
					"Chin Crease",
					"Chin Crease B",
					"Chin Crease Smooth",
					"Chin Depth",
					"Chin Height",
					"Chin Round",
					"Chin Square",
					"Chin Width",
					"Chin Width 2",
					"Chin Width Left",
					"ChinCleft",
					"Chin Out",
					"ChinOutRound",
					"ChinShape",
					"ChinWider",
					"Jaw Chin Shape",
				});

			Sets.Add("Ears", new List<string>(){
					"Ears Angle Upper",
					"Ears Angle",
					"Ears Depth",
					"Ears Height",
					"Ears Size",
				});

			Sets.Add("Cheek", new List<string>(){
					"Cheeks Define",
					"Cheeks Depth",
					"Cheeks Depth Middle",
					"Cheeks Depth Upper",
					"Cheeks Dimple Crease",
					"Cheeks Flat",
					"Cheeks Height",
					"Cheeks Inner Height",
					"Cheeks Inner Puffy",
					"Cheeks Puffy Lower",
					"Cheeks Sink",
					"Cheeks Sink Left",
					"Cheeks Inner Lower",
					"Cheeks Upper Crease",
					"CheekShape3",
					"CheekShapeRounder",
					"CheekShapeRounder2",
					"CheeksOut",
					"CheeksPuffLower",
					"CheeksShape1",
					"CheeksSmooth",
					"Jennifer Cheeks"
				});

			Sets.Add("Head", new List<string>(){
					"Forehead Define",
					"Forehead Flat",
					"Forehead Height",
					"Forehead Round",
					"Forehead Slope",
					"ForeHead Top Width",
					"Forehead Width",
					"Forehead Wrinkle",
					"Head Length",
					"Head Scale",
					"Head Width",
				});

			Sets.Add("Brow", new List<string>(){
					"Brow Define",
					"Brow Depth",
					"Brow Height",
					"Brow Inner Width",
					"Brow Outer Depth",
					"Brow Outer Height",
					"Brow Outer Shift",
					"Brow Outer Width",
					"Brow Shape Inner",
					"Brow Shape Middle",
					"Brow Shape Outer",
					"Brow Width",
					"BrowAreaSmooth",
					"BrowAreaSmooth2",
					"Brows Arch",
					"Brows Size",
				});

			Sets.Add("Nostrils", new List<string>(){
					"NarrowNostrils",
					"Nostrils Bottom Rotate",
					"Nostrils Bottom Shape 2",
					"Nostrils Define Upper",
					"Nostrils Depth",
					"Nostrils Flare",
					"Nostrils Flesh Size",
					"Nostrils Height",
					"Nostrils Inner Height",
					"Nostrils Inner Width",
					"Nostrils Rotation",
					"Nostrils Shape Bottom",
					"Nostrils Shape Height",
					"Nostrils Shape Middle",
					"Nostrils Shape Top",
					"Nostrils Shape Top Angle",
					"Nostrils Thin",
					"Nostrils Width",
					"Nostrils Width Lower"
				});
		}
	}
}

	