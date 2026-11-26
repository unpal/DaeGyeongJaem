using UnityEngine;

// Keep the authored source volume; master gain is applied once by AudioListener.
[RequireComponent(typeof(AudioSource))]
public sealed class PrototypeAudioVolume : MonoBehaviour
{
    public PrototypeAudioBus bus;
    [SerializeField] private AudioSource source;
    public AudioSource Source => source;
    private float baseVolume, appliedVolume;
    private bool initialized;

    private void Awake()
    {
        PrototypeSettings.Initialize();
        InitializeSource();
    }

    private void OnEnable() { PrototypeSettings.Changed += Apply; Apply(); }
    private void OnDisable() { PrototypeSettings.Changed -= Apply; }
    private void LateUpdate() { Apply(); }

    private void Apply()
    {
        if (source == null) return;
        InitializeSource();
        if (!Mathf.Approximately(source.volume, appliedVolume)) baseVolume = source.volume;
        appliedVolume = baseVolume * PrototypeSettings.Volume(bus);
        source.volume = appliedVolume;
    }

    private void InitializeSource()
    {
        if (initialized || source == null) return;
        baseVolume = appliedVolume = source.volume;
        initialized = true;
    }

    public static void Bind(AudioSource source, PrototypeAudioBus bus)
    {
        if (source == null) return;
        PrototypeAudioVolume binding = null;
        foreach (var candidate in source.GetComponents<PrototypeAudioVolume>())
            if (candidate.source == source) { binding = candidate; break; }
        if (binding == null) binding = source.gameObject.AddComponent<PrototypeAudioVolume>();
        if (binding.source != source)
        {
            binding.source = source;
            binding.initialized = false;
        }
        binding.bus = bus;
        binding.Apply();
    }

    public static void PlayEffectAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        var obj = new GameObject("One Shot Effect");
        obj.transform.position = position;
        var audio = obj.AddComponent<AudioSource>();
        audio.clip = clip; audio.volume = volume; audio.spatialBlend = 1f;
        Bind(audio, PrototypeAudioBus.Effects);
        audio.Play();
        Destroy(obj, clip.length + 0.1f);
    }

#if UNITY_EDITOR
    public void SetEditorSource(AudioSource target, PrototypeAudioBus category) { source = target; bus = category; }
#endif
}
