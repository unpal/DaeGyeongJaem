using Fusion;
using NUnit.Framework;
using Script.item;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Assert = NUnit.Framework.Assert;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.TestTools;

public class PrototypeUIFeatureTests
{
    [Test]
    public void MenuBlocksMovementAndActionsButPreservesNetworkOrientation()
    {
        bool wasVisible = Cursor.visible;
        var player = new GameObject("Merged movement input test");
        try
        {
            var movement = player.AddComponent<Un>();
            var rotation = Quaternion.Euler(0f, 123f, 0f);
            movement.ResetForNextRound(Vector3.zero, rotation);
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(Un).GetField("_moveInput", fields).SetValue(movement, Vector2.one);
            typeof(Un).GetField("_jumpPressed", fields).SetValue(movement, true);
            typeof(Un).GetField("_attackPressed", fields).SetValue(movement, true);

            Cursor.visible = false;
            var active = movement.GetNetworkInput();
            Assert.That(active.Move, Is.EqualTo(Vector2.one));
            Assert.That(active.Buttons.IsSet(PlayerButtons.Jump), Is.True);
            Assert.That(active.Buttons.IsSet(PlayerButtons.Attack), Is.True);

            Cursor.visible = true;
            var blocked = movement.GetNetworkInput();
            Assert.That(blocked.BodyYaw, Is.EqualTo(123f).Within(0.001f));
            Assert.That(Vector3.Distance(blocked.AimDirection, rotation * Vector3.forward), Is.LessThan(0.001f));
            Assert.That(blocked.Move, Is.EqualTo(Vector2.zero));
            Assert.That(blocked.Look, Is.EqualTo(Vector2.zero));
            Assert.That(blocked.SelectedSlot, Is.EqualTo(-1));
            foreach (PlayerButtons button in System.Enum.GetValues(typeof(PlayerButtons)))
                Assert.That(blocked.Buttons.IsSet(button), Is.False, button.ToString());
        }
        finally
        {
            Object.DestroyImmediate(player);
            Cursor.visible = wasVisible;
        }
    }

    [Test]
    public void PlayerPrefabHasClimbingConfigAndCompleteRagdollReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab");
        Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
        var movement = prefab.GetComponent<Un>();
        Assert.That(movement, Is.Not.Null);
        var config = new SerializedObject(movement).FindProperty("config").objectReferenceValue;
        Assert.That(config, Is.EqualTo(Resources.Load<ClimbingConfig>("ClimbingConfig")));
        Assert.That(config, Is.Not.Null);
        var ragdoll = prefab.GetComponent<PlayerRagdoll>();
        Assert.That(ragdoll, Is.Not.Null);
        Assert.That(ragdoll.HasValidRig, Is.True);
        Assert.That(ragdoll.HeadBone, Is.Not.Null);
        Assert.That(ragdoll.TargetAnimator, Is.Not.Null);
        foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, node.name);
    }

    [Test]
    public void MainMenuSettingsButtonBuildsAllRequiredControlsAndAppliesValues()
    {
        const string key = "JITAL.Settings.v1";
        bool hadSavedSettings = PlayerPrefs.HasKey(key);
        string savedSettings = PlayerPrefs.GetString(key);
        PrototypeSettings.Initialize();
        string original = JsonUtility.ToJson(PrototypeSettings.Current);
        var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenuScene.unity");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            menu.BuildMenu();
            foreach (string button in new[] { "PlayButton", "JoinButton", "SettingsButton", "QuitButton" })
                Assert.That(GameObject.Find(button)?.GetComponent<Button>(), Is.Not.Null, button);
            GameObject.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(PrototypeSettingsPanel.IsOpen, Is.True);
            var panel = Object.FindFirstObjectByType<PrototypeSettingsPanel>();
            Assert.That(panel.GetComponentsInChildren<Slider>().Length, Is.EqualTo(5));
            Assert.That(panel.GetComponentsInChildren<TMP_Dropdown>().Length, Is.EqualTo(3));
            foreach (var slider in panel.GetComponentsInChildren<Slider>())
            {
                if (slider.name == "마우스 감도") slider.value = 0.42f;
                if (slider.name == "목소리") slider.value = 0.23f;
            }
            Assert.That(PrototypeSettings.Current.sensitivity, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(PrototypeSettings.Current.voice, Is.EqualTo(0.23f).Within(0.001f));
        }
        finally
        {
            var panel = Object.FindFirstObjectByType<PrototypeSettingsPanel>();
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
            JsonUtility.FromJsonOverwrite(original, PrototypeSettings.Current);
            PrototypeSettings.Apply();
            if (hadSavedSettings) PlayerPrefs.SetString(key, savedSettings); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
    }

    [Test]
    public void AudioCategoriesRemainIndependentWithoutCompoundingGain()
    {
        PrototypeSettings.Initialize();
        string original = JsonUtility.ToJson(PrototypeSettings.Current);
        var obj = new GameObject("Audio category test");
        try
        {
            var effects = obj.AddComponent<AudioSource>(); effects.volume = 0.8f;
            var music = obj.AddComponent<AudioSource>(); music.volume = 0.6f;
            PrototypeSettings.Current.effects = 0.5f; PrototypeSettings.Current.music = 0.25f;
            PrototypeAudioVolume.Bind(effects, PrototypeAudioBus.Effects);
            PrototypeAudioVolume.Bind(music, PrototypeAudioBus.Music);
            PrototypeAudioVolume.Bind(effects, PrototypeAudioBus.Effects);
            Assert.That(effects.volume, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(music.volume, Is.EqualTo(0.15f).Within(0.001f));
            PrototypeSettings.Current.effects = 0;
            PrototypeAudioVolume.Bind(effects, PrototypeAudioBus.Effects);
            PrototypeSettings.Current.effects = 1;
            PrototypeAudioVolume.Bind(effects, PrototypeAudioBus.Effects);
            Assert.That(effects.volume, Is.EqualTo(0.8f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(obj);
            JsonUtility.FromJsonOverwrite(original, PrototypeSettings.Current); PrototypeSettings.Apply();
        }
    }

    [TestCase("ABC234", true)]
    [TestCase(" abc234 ", true)]
    [TestCase("", false)]
    [TestCase("ABC", false)]
    [TestCase("ABC23!", false)]
    [TestCase("가나다라마바", false)]
    public void RoomCodeValidationHandlesTypedAndPastedInput(string code, bool valid)
    {
        Assert.That(PrototypeLobbyBootstrap.IsValidRoomCode(code), Is.EqualTo(valid));
    }

    [Test]
    public void JoinErrorsDistinguishBadCodesFromFullOrClosedRooms()
    {
        Assert.That(PrototypeLobbyBootstrap.JoinError(ShutdownReason.GameNotFound), Is.EqualTo("잘못된 방 코드입니다"));
        Assert.That(PrototypeLobbyBootstrap.JoinError(ShutdownReason.GameIsFull), Does.Contain("가득"));
        Assert.That(PrototypeLobbyBootstrap.JoinError(ShutdownReason.GameClosed), Does.Contain("시작"));
    }

    [Test]
    public void SettingsRoundTripPreservesAllFourCategories()
    {
        var source = new PrototypeSettingsData {
            sensitivity = 0.35f, micMode = PrototypeMicMode.PushToMute,
            master = 0.8f, effects = 0.2f, music = 0.4f, voice = 0.6f,
            width = 1920, height = 1080, screenMode = FullScreenMode.FullScreenWindow
        };
        var restored = JsonUtility.FromJson<PrototypeSettingsData>(JsonUtility.ToJson(source));
        restored.Validate();
        Assert.That(restored.sensitivity, Is.EqualTo(source.sensitivity));
        Assert.That(restored.micMode, Is.EqualTo(source.micMode));
        Assert.That(new[] { restored.master, restored.effects, restored.music, restored.voice },
            Is.EqualTo(new[] { 0.8f, 0.2f, 0.4f, 0.6f }));
        Assert.That(new Vector2Int(restored.width, restored.height), Is.EqualTo(new Vector2Int(1920, 1080)));
        Assert.That(restored.screenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
    }

    [Test]
    public void InvalidSavedSettingsCannotApplyNonFiniteOrUnsupportedValues()
    {
        var data = new PrototypeSettingsData {
            sensitivity = float.NaN, master = -1, effects = float.PositiveInfinity,
            music = 2, voice = -3, micMode = (PrototypeMicMode)99,
            width = -100, height = 0, screenMode = (FullScreenMode)99
        };
        data.Validate();
        Assert.That(data.sensitivity, Is.EqualTo(0.1f));
        Assert.That(data.master, Is.Zero);
        Assert.That(data.effects, Is.EqualTo(1));
        Assert.That(data.music, Is.EqualTo(1));
        Assert.That(data.voice, Is.Zero);
        Assert.That(data.width, Is.GreaterThanOrEqualTo(640));
        Assert.That(data.height, Is.GreaterThanOrEqualTo(480));
        Assert.That(data.micMode, Is.EqualTo(PrototypeMicMode.PushToTalk));
        Assert.That(data.screenMode, Is.EqualTo(FullScreenMode.Windowed));
    }

    [TestCase(PrototypeMicMode.PushToTalk, false, true, false)]
    [TestCase(PrototypeMicMode.PushToTalk, true, true, true)]
    [TestCase(PrototypeMicMode.PushToMute, false, true, true)]
    [TestCase(PrototypeMicMode.PushToMute, true, true, false)]
    [TestCase(PrototypeMicMode.PushToMute, false, false, false)]
    [TestCase(PrototypeMicMode.PushToTalk, true, false, false)]
    public void VoiceModesAndFocusControlTransmission(PrototypeMicMode mode, bool held, bool focused, bool expected)
    {
        Assert.That(PrototypeVoice.WantsTransmit(mode, held, focused), Is.EqualTo(expected));
    }

    [UnityTest]
    public IEnumerator RightClickOnlyActivatesTheCurrentRolesActionAndMenusBlockIt()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        var originalBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalUpdateMode = InputSystem.settings.updateMode;
        var originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var mouse = InputSystem.AddDevice<Mouse>();
        using var actions = new PrototypeItemInput();
        try
        {
            actions.SetRole(true, false);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            InputSystem.Update();
            Assert.That(mouse.rightButton.isPressed, Is.True, $"Synthetic device enabled={mouse.enabled}, playing={Application.isPlaying}");
            Assert.That(actions.Catch.IsPressed(), Is.True, $"Catch enabled={actions.Catch.enabled}, phase={actions.Catch.phase}, controls={actions.Catch.controls.Count}");
            Assert.That(actions.Use.enabled, Is.False);
            actions.SetRole(false, false);
            InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); InputSystem.Update();
            Assert.That(actions.Use.IsPressed(), Is.True);
            Assert.That(actions.Catch.enabled, Is.False);
            actions.SetRole(false, true);
            Assert.That(actions.Use.enabled || actions.Drop.enabled || actions.Throw.enabled, Is.False);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = originalBehavior;
            InputSystem.settings.updateMode = originalUpdateMode;
            InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
        }
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterInputTest()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void MaximumLobbyRosterHasDistinctFallbackSpawns()
    {
        var points = new System.Collections.Generic.HashSet<Vector3>();
        for (int i = 0; i < 7; i++) Assert.That(points.Add(PrototypeSpawnPoints.Get(i)), Is.True);
    }

    [Test]
    public void DroppableCatalogPrefabsHaveReplicatedMotionAndIcons()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PrototypeItemCatalog>("Assets/Resources/PrototypeItemCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        foreach (string name in new[] { "WaterGun", "JJB", "DummyItem", "ReviveItem" })
        {
            var entry = catalog.Find(name);
            Assert.That(entry, Is.Not.Null, name);
            Assert.That(entry.prefab.GetComponent<PrototypeWorldItem>(), Is.Not.Null, name);
            Assert.That(entry.icon, Is.Not.Null, name);
        }
    }

    [Test]
    public void AimedShotSupportsVerticalAimAndStopsAtWalls()
    {
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Vector3 origin = new Vector3(20000, 20000, 20000);
        try
        {
            target.transform.position = origin + Vector3.up * 3;
            wall.transform.position = origin + Vector3.up * 1.5f;
            Physics.SyncTransforms();
            Assert.That(PrototypeItemAim.Raycast(Physics.defaultPhysicsScene, origin, Vector3.up, 5, null, out var hit), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(wall));
            Object.DestroyImmediate(wall); Physics.SyncTransforms();
            Assert.That(PrototypeItemAim.Raycast(Physics.defaultPhysicsScene, origin, Vector3.up, 5, null, out hit), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(target));
        }
        finally { Object.DestroyImmediate(target); if (wall != null) Object.DestroyImmediate(wall); }
    }
}
