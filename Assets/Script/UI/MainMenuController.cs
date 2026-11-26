using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public enum EntryMode { Choose, Host, Join }
    public static EntryMode RequestedEntry { get; set; }
    private bool menuBuilt;

    private void Start() { BuildMenu(); }

    public void BuildMenu()
    {
        if (menuBuilt) return;
        Button play = GameObject.Find("PlayButton")?.GetComponent<Button>();
        Button quit = GameObject.Find("QuitButton")?.GetComponent<Button>();
        if (play == null || quit == null) return;
        menuBuilt = true;
        Button join = Instantiate(play, play.transform.parent);
        join.name = "JoinButton";
        Button settings = Instantiate(play, play.transform.parent);
        settings.name = "SettingsButton";
        Configure(play, "방 만들기", 0, () => Enter(EntryMode.Host));
        Configure(join, "게임 참가", 1, () => Enter(EntryMode.Join));
        Configure(settings, "설정", 2, () => PrototypeSettingsPanel.Show(play.GetComponentInChildren<TMP_Text>().font));
        Configure(quit, "게임 종료", 3, Quit);
    }

    private void Configure(Button button, string label, int row, UnityEngine.Events.UnityAction click)
    {
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(click);
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.075f, 1f);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(500, 110);
        rect.anchoredPosition = new Vector2(-5, -450 - row * 115);
    }

    private void Enter(EntryMode mode)
    {
        RequestedEntry = mode;
        SceneManager.LoadScene(matchingSceneName);
    }


    [SerializeField] private string matchingSceneName = "PrototypeLobbyScene";

    [Header("Audio")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.35f;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (backgroundMusic == null)
            return;

        AudioSource source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.clip = backgroundMusic;
        source.volume = backgroundMusicVolume;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.Play();
        PrototypeAudioVolume.Bind(source, PrototypeAudioBus.Music);
    }


    //플레이버튼누르면 다음씬으로,씬인덱스 하드코딩해서 안맞을수도있으니 귀찮아서 이름으로 하기. << 이름변경시 조심!!!!!
    public void Play()
    {
        SceneManager.LoadScene(matchingSceneName);
    }


    //나가기
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
