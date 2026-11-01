// Unity editor script (run with `unity command script eval_file`): builds the ParTV TextMesh Pro font assets from the
// TTFs in Assets/_Project/Fonts/ParTV with a static atlas covering Latin, Latin-1, Latin Extended-A, Cyrillic and the
// UI's symbols (static, so the assets don't change while the game runs), plus an outline material for display text
// over the 3D world.
const string DIR = "Assets/_Project/Fonts/ParTV";
var chars = new System.Text.StringBuilder();
for (int c = 0x20; c <= 0x7E; c++) chars.Append((char)c);
for (int c = 0xA0; c <= 0x17F; c++) chars.Append((char)c);      // Latin-1, Latin Extended-A
for (int c = 0x400; c <= 0x45F; c++) chars.Append((char)c);     // Cyrillic (Russian, Ukrainian, Belarusian, ...)
chars.Append("ҐґЄєІіЇї—–‘’“”„«»…•·×÷±≤≥№€₴₽");   // arrows, ticks, stars and hearts are icon sprites
var log = new System.Text.StringBuilder();
// Sampling size and atlas chosen so ~430 glyphs fit one static atlas (the assets are stored as text: keep them small).
foreach (var (file, size, w, h) in new[] { ("Rubik-Black", 60, 2048, 1024), ("Nunito-ExtraBold", 40, 1024, 1024), ("Nunito-Black", 40, 1024, 1024) })
{
    var font = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Font>($"{DIR}/{file}.ttf");
    var path = $"{DIR}/{file} SDF.asset";
    UnityEditor.AssetDatabase.DeleteAsset(path);
    var asset = TMPro.TMP_FontAsset.CreateFontAsset(font, size, size / 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, w, h,
        TMPro.AtlasPopulationMode.Dynamic, false);
    asset.name = file + " SDF";
    bool ok = asset.TryAddCharacters(chars.ToString(), out string missing);
    asset.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
    UnityEditor.AssetDatabase.CreateAsset(asset, path);
    // The atlas texture(s) and material live inside the font asset.
    foreach (var tex in asset.atlasTextures) { tex.name = file + " Atlas"; UnityEditor.AssetDatabase.AddObjectToAsset(tex, asset); }
    asset.material.name = file + " Material";
    UnityEditor.AssetDatabase.AddObjectToAsset(asset.material, asset);
    if (file == "Rubik-Black")
    {
        var outline = new UnityEngine.Material(asset.material) { name = "Rubik-Black Outline" };
        outline.EnableKeyword("OUTLINE_ON");
        outline.SetFloat("_OutlineWidth", 0.32f);
        outline.SetColor("_OutlineColor", new UnityEngine.Color32(0x14, 0x0B, 0x2E, 255));
        outline.SetFloat("_FaceDilate", 0.15f);
        UnityEditor.AssetDatabase.DeleteAsset($"{DIR}/Rubik-Black Outline.mat");
        UnityEditor.AssetDatabase.CreateAsset(outline, $"{DIR}/Rubik-Black Outline.mat");
    }
    UnityEditor.EditorUtility.SetDirty(asset);
    log.Append($"{file}: glyphs={asset.characterTable.Count} atlases={asset.atlasTextures.Length} missing={(string.IsNullOrEmpty(missing) ? "none" : missing.Length + " chars: " + missing)}; ");
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
