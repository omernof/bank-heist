using UnityEditor;

// Downloaded props/vehicles ship 4K textures; cap them so the project stays
// light (1K is plenty for street props and items on a table).
public class PropImportRules : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Heist/Props/") && !assetPath.Contains("/Heist/Vehicles/") && !assetPath.Contains("/Heist/Trees/") && !assetPath.Contains("/Heist/Weapons/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;
        string l = assetPath.ToLower();
        if (l.Contains("normal") || l.EndsWith("_n.png") || l.Contains("_nrm") || l.Contains("_nor")) ti.textureType = TextureImporterType.NormalMap;
    }
}
