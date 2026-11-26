using Photon.Voice.Unity;
using Script.UI;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrototypeVoice : MonoBehaviour
{
    private InputAction talk;
    private Recorder recorder;
    private float nextScan;
    private bool focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var obj = new GameObject("Prototype Voice Settings");
        DontDestroyOnLoad(obj);
        obj.AddComponent<PrototypeVoice>();
    }

    private void Awake()
    {
        talk = new InputAction("Voice", InputActionType.Button, "<Keyboard>/t");
        talk.Enable();
    }

    public static bool WantsTransmit(PrototypeMicMode mode, bool held, bool focused) =>
        focused && (mode == PrototypeMicMode.PushToTalk ? held : !held);

    private void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            var connection = FindFirstObjectByType<VoiceConnection>();
            recorder = connection != null ? connection.PrimaryRecorder : null;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                bool bound = false;
                foreach (var binding in source.GetComponents<PrototypeAudioVolume>())
                    if (binding.Source == source) bound = true;
                if (bound) continue;
                PrototypeAudioVolume.Bind(source, source.GetComponentInParent<Speaker>() != null
                    ? PrototypeAudioBus.Voice : PrototypeAudioBus.Effects);
            }
        }
        bool transmit = WantsTransmit(PrototypeSettings.Current.micMode, talk.IsPressed(), focused);
        if (recorder != null) recorder.TransmitEnabled = transmit;
        bool muted = recorder == null || !recorder.isActiveAndEnabled || !recorder.RecordingEnabled ||
                     !recorder.TransmitEnabled || recorder.LevelMeter == null;
        if (InGameUIManager.Instance != null) InGameUIManager.Instance.SetMicMuted(muted);
    }

    private void OnApplicationFocus(bool value)
    {
        focused = value;
        if (!value && recorder != null) recorder.TransmitEnabled = false;
    }
    private void OnApplicationQuit() { PrototypeSettings.Save(); }
    private void OnDestroy() { talk?.Dispose(); }
}
