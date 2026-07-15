using UnityEngine;

/// <summary>
/// 플레이어가 일정 거리를 이동할 때마다 낮은 확률로 소리를 발생시키는 스크립트입니다.
/// AudioSource 컴포넌트가 필요합니다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerSoundGen : MonoBehaviour
{
    [Header("Movement Sound Settings")]
    [Tooltip("이 거리(미터)를 이동할 때마다 사운드 발생을 시도합니다.")]
    public float distanceThreshold = 5.0f;

    [Tooltip("사운드가 발생할 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float soundProbability = 0.2f; // 20%

    [Tooltip("발생하는 사운드의 크기")]
    public float soundVolume = 10.0f;

    [Header("Audio Clips")]
    [Tooltip("재생할 발소리 오디오 클립 배열. 지정하지 않으면 소리가 재생되지 않습니다.")]
    public AudioClip[] footstepSounds;

    private AudioSource _audioSource;
    private Vector3 _lastPosition;
    private float _distanceAccumulated = 0f;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        _lastPosition = transform.position;
    }

    void Update()
    {
        // 현재 위치와 마지막 위치 사이의 거리를 계산하여 누적합니다.
        float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
        _distanceAccumulated += distanceMoved;
        _lastPosition = transform.position;

        // 누적 이동 거리가 임계점을 넘었는지 확인합니다.
        if (_distanceAccumulated >= distanceThreshold)
        {
            _distanceAccumulated = 0f; // 누적 거리 초기화

            // 확률적으로 사운드 발생 여부를 결정합니다.
            if (Random.value < soundProbability)
            {
                // 사운드 이벤트 매니저에 이벤트를 발생시킵니다.
                SoundEventManager.TriggerSound(transform.position, soundVolume);

                // 재생할 오디오 클립이 있다면 그중 하나를 랜덤하게 재생합니다.
                PlayFootstepSound();

                Debug.Log("발소리 발생!");
            }
        }
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
