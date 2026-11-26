using System.Collections.Generic;
using Fusion;
using Script.item;
using Script.item.items;
using Script.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Reproducible asset wiring; keeps authored scenes/prefabs reviewable in the editor.
public static class PrototypeUISetup
{
    [MenuItem("JITAL/Build Prototype UI (Windows)")]
    public static void BuildWindows()
    {
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/MainMenuScene.unity", "Assets/Scenes/PrototypeLobbyScene.unity", "Assets/Scenes/PrototypeRoundScene.unity" },
            locationPathName = "Builds/PrototypeUI/JITAL.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new UnityEditor.Build.BuildFailedException("Prototype Windows build failed.");
        Debug.Log($"JITAL Windows build succeeded: {result.summary.totalSize} bytes.");
    }

    [MenuItem("JITAL/Apply Prototype UI Wiring")]
    public static void Apply()
    {
        string[] paths = { "Assets/Prefab/WaterGun.prefab", "Assets/Prefab/JJB.prefab", "Assets/Prefab/PotatoChip.prefab", "Assets/Prefab/ReviveItem.prefab" };
        string[] names = { "WaterGun", "JJB", "DummyItem", "ReviveItem" };
        string[] icons = { "Assets/Sprites/UI/CrossHair.png", "Assets/Sprites/UI/JJB Icon.png", "Assets/Sprites/UI/Potato Icon.png", "Assets/Sprites/UI/Rev Icon.png" };
        var entries = new List<PrototypeItemCatalog.Entry>();
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject obj = PrefabUtility.LoadPrefabContents(paths[i]);
            if (obj.GetComponent<PrototypeWorldItem>() == null) obj.AddComponent<PrototypeWorldItem>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(icons[i]);
            if (obj.TryGetComponent<WaterGun>(out var gun)) gun.itemSprite = sprite;
            PrefabUtility.SaveAsPrefabAsset(obj, paths[i]);
            PrefabUtility.UnloadPrefabContents(obj);
            entries.Add(new PrototypeItemCatalog.Entry {
                name = names[i], prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]).GetComponent<NetworkObject>(), icon = sprite
            });
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        const string catalogPath = "Assets/Resources/PrototypeItemCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<PrototypeItemCatalog>(catalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<PrototypeItemCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
        catalog.entries = entries.ToArray(); EditorUtility.SetDirty(catalog);

        foreach (string sceneName in new[] { "PrototypeLobbyScene", "PrototypeRoundScene" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity");
            var layout = Object.FindFirstObjectByType<PrototypeSpawnLayout>();
            if (layout == null)
            {
                layout = new GameObject("Player Spawn Points").AddComponent<PrototypeSpawnLayout>();
                layout.playerSpawns = new Transform[7];
                for (int i = 0; i < 7; i++)
                {
                    var marker = new GameObject($"Player Spawn {i + 1}").transform;
                    marker.SetParent(layout.transform); marker.position = PrototypeSpawnPoints.Get(i);
                    layout.playerSpawns[i] = marker;
                }
            }
            if (sceneName == "PrototypeLobbyScene")
            {
                var ui = Object.FindFirstObjectByType<PrototypeLobbyUI>();
                foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                        if (button.onClick.GetPersistentMethodName(i) == "ShowRolePanel")
                        {
                            UnityEventTools.RemovePersistentListener(button.onClick, i);
                            UnityEventTools.AddPersistentListener(button.onClick, ui.BackToMainMenu);
                        }
                }
                foreach (TMP_Text label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (label.text == "JOIN ROOM") label.text = "게임 세션 참가";
                foreach (var recorder in Object.FindObjectsByType<Photon.Voice.Unity.Recorder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    recorder.TransmitEnabled = false;
            }
            else WireGuideIcons();

            // Bind authored sources before playback. Dynamically spawned voice sources are bound at runtime.
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                PrototypeAudioVolume binding = null;
                foreach (var candidate in source.GetComponents<PrototypeAudioVolume>())
                    if (candidate.Source == source || candidate.Source == null) { binding = candidate; break; }
                if (binding == null) binding = source.gameObject.AddComponent<PrototypeAudioVolume>();
                var speaker = Object.FindFirstObjectByType<PublicSpeaker>();
                binding.SetEditorSource(source, speaker != null && speaker.BGMSpeaker == source ? PrototypeAudioBus.Music : PrototypeAudioBus.Effects);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("JITAL prototype UI assets prepared.");
    }

    private static void WireGuideIcons()
    {
        var ui = Object.FindFirstObjectByType<InGameUIManager>();
        var serialized = new SerializedObject(ui);
        var root = (GameObject)serialized.FindProperty("itemKeyGuideRoot").objectReferenceValue;
        var text = (TMP_Text)serialized.FindProperty("itemKeyGuideText").objectReferenceValue;
        ((RectTransform)root.transform).sizeDelta = new Vector2(300, 150);
        text.rectTransform.offsetMin = new Vector2(54, 12);
        text.rectTransform.offsetMax = new Vector2(-12, -12);
        text.fontSize = 22; text.lineSpacing = 12;
        string[] files = { "CrossHair.png", "Back.png", "Copy.png" };
        for (int i = 0; i < files.Length; i++)
        {
            string name = $"Action Icon {i}";
            Transform existing = root.transform.Find(name);
            var rect = existing != null ? (RectTransform)existing : new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(28, -33 - i * 42);
            rect.sizeDelta = new Vector2(28, 28);
            var image = rect.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/" + files[i]);
            image.preserveAspect = true; image.raycastTarget = false;
        }
    }
}
