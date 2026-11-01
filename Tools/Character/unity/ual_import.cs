// Imports the Quaternius UAL FBXs (temporarily, under Assets/_UALTemp) as Humanoid and copies the chosen clips out as
// .anim files in Assets/_Project/Animations/Character. Loop clips loop; everything is in place (root motion baked).
const string TEMP = "Assets/_UALTemp";
const string OUT = "Assets/_Project/Animations/Character";
// Folder holding the two unzipped Standard packs (ual1/x/... and ual2/x/...); downloads are not kept in the repo.
var src = System.Environment.GetEnvironmentVariable("UAL_DIR") ?? "C:/Temp/UAL/";
if (!UnityEditor.AssetDatabase.IsValidFolder(TEMP)) UnityEditor.AssetDatabase.CreateFolder("Assets", "_UALTemp");
if (!UnityEditor.AssetDatabase.IsValidFolder(OUT)) UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Animations", "Character");
var files = new[] {
    (src + "ual1/x/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx", "UAL1.fbx"),
    (src + "ual1/x/Universal Animation Library[Standard]/Unity/UAL1_Standard_RM.fbx", "UAL1_RM.fbx"),
    (src + "ual2/x/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx", "UAL2.fbx"),
};
foreach (var a in UnityEditor.AssetDatabase.FindAssets("t:AnimationClip", new[] { OUT })) UnityEditor.AssetDatabase.DeleteAsset(UnityEditor.AssetDatabase.GUIDToAssetPath(a));
foreach (var (from, to) in files) System.IO.File.Copy(from, System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), TEMP, to), true);
UnityEditor.AssetDatabase.Refresh();
// Clip name in the file -> (our name, loops)
var wanted = new System.Collections.Generic.Dictionary<string, (string name, bool loop)>
{
    ["Idle_Loop"] = ("Idle", true), ["Walk_Loop"] = ("Walk", true), ["Jog_Fwd_Loop"] = ("Jog", true), ["Sprint_Loop"] = ("Sprint", true),
    ["Jump_Start"] = ("JumpStart", false), ["Jump_Loop"] = ("Fall", true), ["Jump_Land"] = ("Land", false),
    ["Hit_Chest"] = ("HitChest", false), ["Hit_Head"] = ("HitHead", false), ["Push_Loop"] = ("PushLoop", true),
    ["Spell_Simple_Shoot"] = ("SpellShoot", false), ["Dance_Loop"] = ("Dance", true), ["Sitting_Idle_Loop"] = ("SitIdle", true),
    ["Driving_Loop"] = ("Drive", true), ["Swim_Idle_Loop"] = ("SwimIdle", true), ["Roll"] = ("Roll", false),
    ["Hit_Knockback"] = ("HitKnockback", false), ["OverhandThrow"] = ("Throw", false), ["Walk_Carry_Loop"] = ("WalkCarry", true),
    ["Idle_FoldArms_Loop"] = ("IdleFoldArms", true), ["Yes"] = ("Yes", false), ["LayToIdle"] = ("GetUp", false),
};
var log = new System.Text.StringBuilder();
foreach (var name in new[] { "UAL1.fbx", "UAL2.fbx", "UAL1_RM.fbx" })
{
    var path = TEMP + "/" + name;
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
    importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
    importer.materialImportMode = UnityEditor.ModelImporterMaterialImportMode.None;
    importer.importAnimation = true;
    importer.SaveAndReimport();
    importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    bool rm = name.Contains("_RM");
    var clips = importer.defaultClipAnimations;
    var keep = new System.Collections.Generic.List<UnityEditor.ModelImporterClipAnimation>();
    foreach (var c in clips)
    {
        var take = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1);
        if (!wanted.TryGetValue(take, out var w)) continue;
        if (rm && !(take is "Walk_Loop" or "Jog_Fwd_Loop" or "Sprint_Loop")) continue;
        c.name = rm ? w.name + "_RM" : w.name;
        c.loopTime = w.loop; c.loopPose = w.loop;
        // The pack faces -Z and travels exactly along it: keep that orientation, turned to face +Z (the body-orientation
        // option averages the swinging torso and skews the jog about 30 degrees off its path).
        c.lockRootRotation = !rm; c.keepOriginalOrientation = true; c.rotationOffset = 180f;
        c.lockRootHeightY = true; c.keepOriginalPositionY = false; c.heightFromFeet = true;
        c.lockRootPositionXZ = !rm; c.keepOriginalPositionXZ = true;
        keep.Add(c);
    }
    importer.clipAnimations = keep.ToArray();
    importer.SaveAndReimport();
    var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Avatar>(path);
    log.Append($"{name}: avatar valid={avatar?.isValid} clips={keep.Count}; ");
    if (rm) continue;
    foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        if (obj is UnityEngine.AnimationClip clip && !clip.name.StartsWith("__preview"))
        {
            var copy = new UnityEngine.AnimationClip();
            UnityEditor.EditorUtility.CopySerialized(clip, copy);
            UnityEditor.AssetDatabase.CreateAsset(copy, $"{OUT}/{clip.name}.anim");
            log.Append($"{clip.name}({clip.length:0.00}s,{(clip.isLooping ? "loop" : "once")}) ");
        }
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
