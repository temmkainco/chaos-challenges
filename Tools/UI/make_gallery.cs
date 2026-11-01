// Unity editor script: creates Resources/UiTheme.asset (fonts linked) if missing, and the dev gallery scene
// Scenes/Dev/UiGallery.unity (camera, EventSystem with the Input System UI module, the UiGallery builder).
const string THEME = "Assets/_Project/Resources/UiTheme.asset";
var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
if (theme == null)
{
    theme = UnityEngine.ScriptableObject.CreateInstance<UiTheme>();
    UnityEditor.AssetDatabase.CreateAsset(theme, THEME);
}
const string F = "Assets/_Project/Fonts/ParTV/";
theme.Display = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(F + "Rubik-Black SDF.asset");
theme.DisplayOutline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(F + "Rubik-Black Outline.mat");
theme.UiBold = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(F + "Nunito-ExtraBold SDF.asset");
theme.UiBlack = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(F + "Nunito-Black SDF.asset");
UnityEditor.EditorUtility.SetDirty(theme);
UnityEditor.AssetDatabase.SaveAssets();

if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Scenes/Dev")) UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Dev");
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
var cam = new UnityEngine.GameObject("Camera", typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = new UnityEngine.Color32(0x6F, 0xB3, 0xE8, 0xFF);   // a bright sky, like the 3D world behind the HUD
cam.tag = "MainCamera";
var es = new UnityEngine.GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
new UnityEngine.GameObject("UiGallery", typeof(UiGallery));
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/Dev/UiGallery.unity");
return $"theme fonts: {theme.Display?.name}, {theme.UiBold?.name}, {theme.UiBlack?.name}, outline {theme.DisplayOutline?.name}";
