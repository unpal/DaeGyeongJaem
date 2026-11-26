using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PrototypeSettingsPanel : MonoBehaviour
{
    private static PrototypeSettingsPanel active;
    private static int closedFrame = -1;
    private Action onClosed;
    private TMP_FontAsset font;
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int resolutionIndex, modeIndex;
    private static readonly FullScreenMode[] Modes = {
        FullScreenMode.Windowed, FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow
    };
    public static bool IsOpen => active != null;
    public static bool ConsumesEscape => IsOpen || closedFrame == Time.frameCount;

    public static void Show(TMP_FontAsset font, Action onClosed = null)
    {
        if (active != null) return;
        PrototypeSettings.Initialize();
        var root = new GameObject("Settings", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        active = root.AddComponent<PrototypeSettingsPanel>();
        active.font = font != null ? font : TMP_Settings.defaultFontAsset;
        active.onClosed = onClosed;
        active.Build();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Build()
    {
        RectTransform shade = Rect("Backdrop", transform, new Vector2(1920, 1080), Vector2.zero);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
        shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.9f);
        RectTransform panel = Rect("Settings Content", shade, new Vector2(1000, 920), Vector2.zero);
        Label(panel, "설정", new Vector2(0, 390), 38);
        var data = PrototypeSettings.Current;
        Slider(panel, "마우스 감도", 300, 0.01f, 1f, data.sensitivity, v => data.sensitivity = v, false);
        Dropdown(panel, "마이크 모드 (T 키를 누르는 동안)", 210,
            new List<string> { "눌러서 말하기", "눌러서 음소거" }, (int)data.micMode,
            v => { data.micMode = (PrototypeMicMode)v; PrototypeSettings.Apply(); });
        Slider(panel, "전체 음량", 120, 0, 1, data.master, v => data.master = v);
        Slider(panel, "효과음", 40, 0, 1, data.effects, v => data.effects = v);
        Slider(panel, "음악", -40, 0, 1, data.music, v => data.music = v);
        Slider(panel, "목소리", -120, 0, 1, data.voice, v => data.voice = v);
        modeIndex = Mathf.Max(0, Array.IndexOf(Modes, data.screenMode));
        Dropdown(panel, "화면 모드", -210, new List<string> { "창 모드", "전체 화면", "테두리 없는 창" },
            modeIndex, v => modeIndex = v);
        var current = new Vector2Int(data.width, data.height);
        resolutions.Add(current);
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }
        var names = new List<string>();
        foreach (var size in resolutions) names.Add($"{size.x} × {size.y}");
        Dropdown(panel, "해상도", -290, names, 0, v => resolutionIndex = v);
        Button(panel, "화면 설정 적용", new Vector2(-200, -390), () => {
            data.width = resolutions[resolutionIndex].x;
            data.height = resolutions[resolutionIndex].y;
            data.screenMode = Modes[modeIndex];
            PrototypeSettings.Apply(true);
            PrototypeSettings.Save();
        });
        Button(panel, "저장하고 돌아가기", new Vector2(200, -390), Close);
    }

    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Close(); }
    public void Close()
    {
        if (active != this) return;
        PrototypeSettings.Save();
        active = null;
        closedFrame = Time.frameCount;
        onClosed?.Invoke();
        Destroy(gameObject);
    }
    private void OnDestroy() { if (active == this) { PrototypeSettings.Save(); active = null; } }

    private void Slider(Transform parent, string title, float y, float min, float max, float initial,
        Action<float> setValue, bool percent = true)
    {
        var label = Label(parent, "", new Vector2(-240, y), 24);
        var obj = DefaultControls.CreateSlider(new DefaultControls.Resources());
        obj.name = title; obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.sizeDelta = new Vector2(360, 32);
        rect.anchoredPosition = new Vector2(240, y);
        var slider = obj.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
        slider.SetValueWithoutNotify(initial);
        void Refresh(float value) { label.text = percent ? $"{title}  {value:P0}" : $"{title}  {value:F2}"; }
        Refresh(initial);
        slider.onValueChanged.AddListener(v => { setValue(v); Refresh(v); PrototypeSettings.Apply(); });
    }

    private void Dropdown(Transform parent, string title, float y, List<string> options, int value, Action<int> changed)
    {
        Label(parent, title, new Vector2(-240, y), 22);
        var obj = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        obj.name = title; obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.sizeDelta = new Vector2(360, 50);
        rect.anchoredPosition = new Vector2(240, y);
        foreach (TMP_Text text in obj.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSize = 22; }
        var dropdown = obj.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(value); dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(v => changed(v));
    }

    private void Button(Transform parent, string title, Vector2 position, Action action)
    {
        var rect = Rect(title, parent, new Vector2(340, 60), position);
        rect.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.28f, 0.36f);
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => action());
        Label(rect, title, Vector2.zero, 24);
    }

    private TMP_Text Label(Transform parent, string text, Vector2 position, float size)
    {
        var rect = Rect("Label", parent, new Vector2(450, 60), position);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text; label.font = font; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
}
