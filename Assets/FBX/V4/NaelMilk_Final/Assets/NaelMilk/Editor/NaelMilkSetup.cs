using UnityEngine;
using UnityEditor;

public static class NaelMilkSetup
{
    const string Root = "Assets/NaelMilk";
    [MenuItem("Tools/Nael Milk/Create and Link Materials")]
    public static void Setup()
    {
        var importer = AssetImporter.GetAtPath(Root + "/NaelMilk.fbx") as ModelImporter;
        if (importer == null) { Debug.LogError("NaelMilk.fbx not found in " + Root); return; }
        if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader shader = pipeline == null ? Shader.Find("Standard") : pipeline.GetType().Name.Contains("HDRender") ? Shader.Find("HDRP/Lit") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogError("Compatible Lit shader not found."); return; }
        string[] names = { "Front", "Back", "Side" };
        foreach (string part in names) {
            string name = "Milk_" + part;
            string path = Root + "/Materials/" + name + ".mat";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/milk_" + part.ToLowerInvariant() + ".png");
            if (texture == null) { Debug.LogError("Missing texture: " + part); return; }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); material.name = name; AssetDatabase.CreateAsset(material, path); }
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }
        AssetDatabase.SaveAssets(); importer.SaveAndReimport();
        Debug.Log("Nael Milk materials created and linked.");
    }
}
