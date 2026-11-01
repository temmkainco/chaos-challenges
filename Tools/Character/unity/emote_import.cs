// Imports the Motion Cast FREE01 emotes (Godot Asset Store, "Royalty Free"; the "no root" FBXs) as Humanoid with an
// explicit bone map, trims each to the emote's length from its liveliest part, and copies the clips out as .anim files
// into Assets/_Project/Animations/Character/Emotes. Run with `unity command script eval_file`; set MC_DIR to the
// folder holding the unzipped "no root" FBXs.
const string TEMP = "Assets/_MCTemp";
const string OUT = "Assets/_Project/Animations/Character/Emotes";
var src = System.Environment.GetEnvironmentVariable("MC_DIR") ?? "C:/Temp/MotionCast/no root/";
if (!UnityEditor.AssetDatabase.IsValidFolder(TEMP)) UnityEditor.AssetDatabase.CreateFolder("Assets", "_MCTemp");
if (!UnityEditor.AssetDatabase.IsValidFolder(OUT)) UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Animations/Character", "Emotes");
// Our name, source file, start second (each slice lasts EmotePlayer.DURATION).
var emotes = new (string name, string file, float start)[]
{
    // Starts picked by scoring every window of the source (hands high and wide for Cheer, the hand up for Wave,
    // hands together for Clap, hands at the face for Cry); Wave from where the hand is overhead (the scorer's pick, 3.6 s,
    // only reaches head height on our short arms).
    ("Wave", "5-Guy who waves-no root.fbx", 1.5f), ("Laugh", "3-Laughing guy-no root.fbx", 1.6f),
    ("Cheer", "1-Happy guy-no root.fbx", 6.3f), ("Cry", "2-Crying-no root.fbx", 11.9f), ("Clap", "8-Applause-no root.fbx", 9.17f),
};
const float LENGTH = 2.6f;
// The rig's names: "LShoulder" is the upper arm, "Finger0" the thumb.
var map = new System.Collections.Generic.List<(string human, string bone)>
{
    ("Hips", "Hip"), ("Spine", "LowerSpine"), ("Chest", "MiddleSpine"), ("UpperChest", "Chest"), ("Neck", "Neck"), ("Head", "Head"),
};
foreach (var (side, s) in new[] { ("Left", "L"), ("Right", "R") })
{
    string f = side == "Left" ? "LeftFinger" : "RightFinger";
    map.Add((side + "Shoulder", s + "Clavicle")); map.Add((side + "UpperArm", s + "Shoulder")); map.Add((side + "LowerArm", s + "Forearm")); map.Add((side + "Hand", s + "Hand"));
    map.Add((side + "UpperLeg", s + "Thigh")); map.Add((side + "LowerLeg", s + "Shin")); map.Add((side + "Foot", s + "Foot")); map.Add((side + "Toes", s + "Toe"));
    var fingers = new[] { "Thumb", "Index", "Middle", "Ring", "Little" };
    for (int k = 0; k < 5; k++)
    {
        map.Add(($"{side} {fingers[k]} Proximal", $"{f}{k}"));
        map.Add(($"{side} {fingers[k]} Intermediate", $"{f}{k}1"));
        map.Add(($"{side} {fingers[k]} Distal", $"{f}{k}2"));
    }
}
var log = new System.Text.StringBuilder();
foreach (var (name, file, start) in emotes)
{
    var path = $"{TEMP}/{name}.fbx";
    System.IO.File.Copy(src + file, System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path), true);
    UnityEditor.AssetDatabase.ImportAsset(path);
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
    importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
    importer.materialImportMode = UnityEditor.ModelImporterMaterialImportMode.None;
    var hd = importer.humanDescription;
    hd.human = map.ConvertAll(m => { var hb = new UnityEngine.HumanBone { humanName = m.human, boneName = m.bone }; hb.limit.useDefaultValues = true; return hb; }).ToArray();
    importer.humanDescription = hd;
    importer.SaveAndReimport();
    importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    var take = importer.importedTakeInfos[0];
    var clip = importer.defaultClipAnimations[0];
    clip.name = name;
    clip.firstFrame = (take.startTime + start) * take.sampleRate;
    clip.lastFrame = System.Math.Min(take.stopTime, take.startTime + start + LENGTH) * take.sampleRate;
    clip.loopTime = false;
    clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
    clip.lockRootHeightY = true; clip.keepOriginalPositionY = false; clip.heightFromFeet = true;
    clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = true;
    importer.clipAnimations = new[] { clip };
    importer.SaveAndReimport();
    var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Avatar>(path);
    foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        if (obj is UnityEngine.AnimationClip c && c.name == name)
        {
            var copy = new UnityEngine.AnimationClip();
            UnityEditor.EditorUtility.CopySerialized(c, copy);
            // Our head is huge: the waving hand went through it. Swing that arm out to the side and straighten it.
            if (name == "Wave")
                foreach (var (muscle, offset) in new[] { ("Right Arm Down-Up", -0.25f), ("Right Forearm Stretch", 0.3f) })
                {
                    var binding = System.Array.Find(UnityEditor.AnimationUtility.GetCurveBindings(copy), b => b.propertyName == muscle);
                    var curve = UnityEditor.AnimationUtility.GetEditorCurve(copy, binding);
                    if (curve == null) { log.Append($"no curve {muscle}; "); continue; }
                    var keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++) keys[k].value = UnityEngine.Mathf.Clamp(keys[k].value + offset, -1f, 1f);
                    curve.keys = keys;
                    UnityEditor.AnimationUtility.SetEditorCurve(copy, binding, curve);
                }
            UnityEditor.AssetDatabase.DeleteAsset($"{OUT}/{name}.anim");
            UnityEditor.AssetDatabase.CreateAsset(copy, $"{OUT}/{name}.anim");
            log.Append($"{name}: avatar valid={avatar.isValid} fps={take.sampleRate} take={take.stopTime - take.startTime:0.0}s clip={c.length:0.00}s; ");
        }
}
UnityEditor.AssetDatabase.DeleteAsset(TEMP);
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
