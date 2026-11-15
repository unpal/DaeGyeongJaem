using UnityEngine;
using UnityEditor;

public static class WatergunMaterialSetup
{
    private const string Folder = "Assets/Watergun";
    private static readonly string[] Names = { "Green", "DarkGreen", "Yellow", "Orange", "Blue", "Red" };
    private static readonly Color[] Colors = {
        new Color(0.151536f, 0.668113f, 0.095447f), new Color(0.081767f, 0.355392f, 0.091967f),
        new Color(0.981929f, 0.716329f, 0.191200f), new Color(1f, 0.358f, 0.1066f),
        new Color(0.097376f, 0.141016f, 0.509634f), new Color(0.814700f, 0.203848f, 0.114921f)
    };
    [MenuItem("Tools/Watergun/Create and Link Materials")]
    public static void CreateAndLink()
    {
        string modelPath = Folder + "/Watergun.fbx";
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) { Debug.LogError("Watergun.fbx must be at " + modelPath); return; }
        if (!AssetDatabase.IsValidFolder(Folder + "/Materials")) AssetDatabase.CreateFolder(Folder, "Materials");
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader shader;
        if (pipeline == null) shader = Shader.Find("Standard");
        else if (pipeline.GetType().Name.Contains("HDRender")) shader = Shader.Find("HDRP/Lit");
        else shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogError("Could not find a compatible Lit shader."); return; }
        for (int i = 0; i < Names.Length; i++)
        {
            string name = "Watergun_" + Names[i];
            string path = Folder + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                mat = new Material(shader); mat.name = name;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Colors[i]);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Colors[i]);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.5f);
                AssetDatabase.CreateAsset(mat, path);
            }
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        Debug.Log("Watergun material assets created and linked.");
    }
}