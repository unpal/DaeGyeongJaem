using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{


    [SerializeField] private string matchingSceneName = "PrototypeLobbyScene";

    [Header("Audio")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.35f;

    private void Awake()
    {
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
