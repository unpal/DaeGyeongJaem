using UnityEngine;

/// <summary>
/// 전역 사운드 이벤트를 관리하는 싱글톤 매니저입니다.
/// </summary>
public class SoundEventManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static SoundEventManager _instance;

    /// <summary>
    /// SoundEventManager의 싱글톤 인스턴스를 가져옵니다.
    /// </summary>
    public static SoundEventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에서 SoundEventManager 인스턴스를 찾습니다.
                _instance = FindObjectOfType<SoundEventManager>();

                // 씬에 없다면 새로 생성합니다.
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(nameof(SoundEventManager));
                    _instance = singletonObject.AddComponent<SoundEventManager>();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 사운드 발생 시 호출될 이벤트의 델리게이트입니다.
    /// </summary>
    /// <param name="position">사운드가 발생한 위치</param>
    /// <param name="volume">사운드의 크기</param>
    public delegate void SoundEvent(Vector3 position, float volume);

    /// <summary>
    /// 사운드가 발생했을 때 구독자들에게 알리는 정적 이벤트입니다.
    /// </summary>
    public static event SoundEvent OnSoundTriggered;

    private void Awake()
    {
        // 싱글톤 인스턴스를 설정하고, 중복 인스턴스를 파괴합니다.
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            // 씬이 변경되어도 파괴되지 않도록 설정합니다.
            DontDestroyOnLoad(this.gameObject);
        }
    }

    /// <summary>
    /// 사운드 이벤트를 발생시킵니다.
    /// </summary>
    /// <param name="position">사운드가 발생한 위치</param>
    /// <param name="volume">사운드의 크기</param>
    public static void TriggerSound(Vector3 position, float volume)
    {
        // 구독자가 있을 경우에만 이벤트를 호출합니다.
        Debug.Log($"[{position}] 에서 ({volume})만큼의 사운드 발생");
        OnSoundTriggered?.Invoke(position, volume);
    }
}
