using System;
using UnityEngine;

public enum PrototypeMicMode { PushToTalk, PushToMute }
public enum PrototypeAudioBus { Effects, Music, Voice }

[Serializable]
public sealed class PrototypeSettingsData
{
    public float sensitivity = 0.1f;
    public PrototypeMicMode micMode = PrototypeMicMode.PushToTalk;
    public float master = 1f, effects = 1f, music = 1f, voice = 1f;
    public int width = 1280, height = 720;
    public FullScreenMode screenMode = FullScreenMode.Windowed;

    public void Validate()
    {
        sensitivity = FiniteClamp(sensitivity, 0.01f, 1f, 0.1f);
        master = FiniteClamp(master, 0f, 1f, 1f);
        effects = FiniteClamp(effects, 0f, 1f, 1f);
        music = FiniteClamp(music, 0f, 1f, 1f);
        voice = FiniteClamp(voice, 0f, 1f, 1f);
        if (!Enum.IsDefined(typeof(PrototypeMicMode), micMode)) micMode = PrototypeMicMode.PushToTalk;
        if (screenMode != FullScreenMode.Windowed && screenMode != FullScreenMode.ExclusiveFullScreen &&
            screenMode != FullScreenMode.FullScreenWindow) screenMode = FullScreenMode.Windowed;
        width = Mathf.Clamp(width, 640, 7680);
        height = Mathf.Clamp(height, 480, 4320);
    }

    private static float FiniteClamp(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
}

// Settings have no dependency on a particular scene or UI prefab.
public static class PrototypeSettings
{
    private const string Key = "JITAL.Settings.v1";
    public static PrototypeSettingsData Current { get; private set; }
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Current = null;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (Current != null) return;
        try { Current = JsonUtility.FromJson<PrototypeSettingsData>(PlayerPrefs.GetString(Key, "")); }
        catch (ArgumentException) { Current = null; }
        if (Current == null)
            Current = new PrototypeSettingsData {
                master = PlayerPrefs.GetFloat("MasterVolume", 1f),
                width = Screen.width, height = Screen.height, screenMode = Screen.fullScreenMode
            };
        Current.Validate();
        Apply(true);
    }

    public static void Apply(bool display = false)
    {
        Current.Validate();
        AudioListener.volume = Current.master;
        if (display) Screen.SetResolution(Current.width, Current.height, Current.screenMode);
        Changed?.Invoke();
    }

    public static void Save()
    {
        Current.Validate();
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    public static float Volume(PrototypeAudioBus bus) => bus == PrototypeAudioBus.Music ? Current.music :
        bus == PrototypeAudioBus.Voice ? Current.voice : Current.effects;
}
