using UnityEngine;
using Script.sound;

/// <summary>
/// 일정 시간마다 소리를 발생시키는 스크립트입니다.
/// 시간이 지날수록 주기가 점점 짧아집니다.
/// AudioSource 컴포넌트가 필요합니다.
/// 플레이어에게 붙여야 합니다
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerSoundGen : MonoBehaviour
{
    [Header("Sound Settings")]
    [Tooltip("발생하는 사운드의 크기")]
    public float soundVolume = 10.0f;

    [Header("Audio Clips")]
    [Tooltip("재생할 발소리 오디오 클립 배열. 지정하지 않으면 소리가 재생되지 않습니다.")]
    public AudioClip[] footstepSounds;

    [Header("Generation Settings")]
    [Tooltip("초기 소리 발생 간격 (초)")]
    public float initialInterval = 10f;
        
    [Tooltip("최소 소리 발생 간격 (초)")]
    public float minInterval = 2f;
        
    [Tooltip("최소 간격에 도달하기까지 걸리는 시간 (초)")]
    public float timeToMinInterval = 60f;

    private AudioSource _audioSource;
    private float _timer = 0f;
    private float _currentNextTime = 0f;

    private bool _isTimerStarted = false;
    private float _startTime;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        SetNextTime();
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= _currentNextTime)
        {
            _timer = 0f;
            SetNextTime();

            // 사운드 이벤트 매니저에 이벤트를 발생시킵니다.
            SoundEventManager.TriggerSound(transform.position, soundVolume);

            // 재생할 오디오 클립이 있다면 그중 하나를 랜덤하게 재생합니다.
            PlayFootstepSound();

            Debug.Log("주기적 소리 발생!");
        }
    }

    /// <summary>
    /// 외부에서 호출 시 점진적 주기 단축 타이머를 시작합니다.
    /// </summary>
    public void StartTimer()
    {
        _isTimerStarted = true;
        _startTime = Time.time;
    }

    /// <summary>
    /// 시간이 지날수록 점점 짧아지는 간격을 반환합니다.
    /// </summary>
    private float GetCurrentInterval()
    {
        if (!_isTimerStarted)
        {
            return initialInterval;
        }

        float elapsedTime = Time.time - _startTime;
        float t = Mathf.Clamp01(elapsedTime / timeToMinInterval);
            
        // 시간에 따라 간격이 선형적으로 줄어듦
        return Mathf.Lerp(initialInterval, minInterval, t);
    }

    private void SetNextTime()
    {
        float baseInterval = GetCurrentInterval();

        // 지정한 시간에 +- 20% 정도의 간격
        float minTime = baseInterval * 0.8f;
        float maxTime = baseInterval * 1.2f;
        
        _currentNextTime = Random.Range(minTime, maxTime);
    }

    private void PlayFootstepSound()
    {
        if (footstepSounds != null && footstepSounds.Length > 0)
        {
            // 랜덤한 발소리 클립을 선택합니다.
            AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }
}
