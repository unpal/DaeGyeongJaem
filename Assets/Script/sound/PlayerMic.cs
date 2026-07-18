using System;
using UnityEngine;

/// <summary>
/// 마이크 입력을 감지하여 볼륨이 일정 크기 이상일 때 사운드 이벤트를 발생시키는 스크립트입니다.
/// AudioSource 컴포넌트가 필요합니다.
/// 플레이어에게 붙이지 않아도 됩니다
/// </summary>
[RequireComponent(typeof(AudioSource))]
[Obsolete("사용되지 않는 코드입니다. PlayerNoise 컴포넌트를 사용하세요")]
public class PlayerMic : MonoBehaviour
{
    [Tooltip("사운드 이벤트를 발생시킬 볼륨 임계점 (0.0 ~ 1.0)")]
    public float volumeThreshold = 0.1f;

    [Tooltip("마이크 소리로 인해 생성될 사운드의 크기(범위)")]
    public float soundRange = 20.0f;

    [Tooltip("마이크 입력의 최소 데시벨 임계값 (데시벨 단위). 낮을수록 미세한 소리도 감지합니다.")]
    public float minDecibels = -50f;

    [Tooltip("콘솔에 실시간 마이크 볼륨과 데시벨 정보를 출력할지 여부")]
    public bool debugVolume = false;
    
    [Tooltip("분석할 오디오 샘플의 크기. 높을수록 정확하지만 무거워집니다.")]
    private const int SampleSize = 1024; // 256에서 1024로 넉넉하게 잡는 것이 보통 좋아

    private AudioSource _audioSource;
    private string _microphoneDevice;
    private float[] _samples;

    System.Collections.IEnumerator Start()
    {
        _audioSource = GetComponent<AudioSource>();
        _samples = new float[SampleSize];

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("마이크를 찾을 수 없습니다! PlayerMic 스크립트를 비활성화합니다.");
            enabled = false;
            yield break;
        }
        
        _microphoneDevice = Microphone.devices[0];
        Debug.Log("Selected Device : " + _microphoneDevice);

        // 마이크의 최소/최대 주파수 확인 후 적절한 샘플 레이트 선택
        int minFreq, maxFreq;
        Microphone.GetDeviceCaps(_microphoneDevice, out minFreq, out maxFreq);
        int sampleRate = AudioSettings.outputSampleRate;
        if (maxFreq > 0)
        {
            sampleRate = Mathf.Clamp(sampleRate, minFreq, maxFreq);
        }
        
        _audioSource.clip = Microphone.Start(_microphoneDevice, true, 10, sampleRate);
        _audioSource.loop = true;

        // 마이크가 실제로 녹음을 개시하여 위치가 0보다 커질 때까지 프레임을 넘기며 대기 (무한 루프 방지 타임아웃 추가)
        float timer = 0f;
        while (Microphone.GetPosition(_microphoneDevice) <= 0 && timer < 2.0f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (Microphone.GetPosition(_microphoneDevice) <= 0)
        {
            Debug.LogError("마이크 시작 대기 중 타임아웃이 발생했습니다. 권한이 거부되었거나 장치 오류일 수 있습니다.");
            enabled = false;
            yield break;
        }

        // 스피커로 내 목소리가 흘러나오는 에코 현상을 막기 위해 AudioSource를 음소거 처리
        _audioSource.mute = true;
        _audioSource.Play(); 
    }

    void Update()
    {
        float volume = GetCurrentVolume();

        if (debugVolume)
        {
            Debug.Log($"[PlayerMic] Current Volume: {volume:F4} (Threshold: {volumeThreshold})");
        }
        
        if (volume > volumeThreshold)
        {
            SoundEventManager.TriggerSound(transform.position, soundRange);
        }
    }

    private float GetCurrentVolume()
    {
        // 1. 현재 마이크가 녹음 중인 위치를 가져옴
        int micPosition = Microphone.GetPosition(_microphoneDevice) - SampleSize + 1;

        // 루프 녹음이므로 음수일 때는 클립 끝부분부터 시작하도록 랩핑합니다.
        if (micPosition < 0)
        {
            if (_audioSource.clip != null)
            {
                micPosition = (micPosition + _audioSource.clip.samples) % _audioSource.clip.samples;
            }
            else
            {
                return 0f;
            }
        }

        // 2. 출력(스피커)이 아닌 녹음된 AudioClip 원본 데이터에서 직접 읽어옴
        if (_audioSource.clip != null)
        {
            _audioSource.clip.GetData(_samples, micPosition);
        }
        else
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = 0; i < _samples.Length; i++)
        {
            sum += _samples[i] * _samples[i];
        }

        float rms = Mathf.Sqrt(sum / _samples.Length);
        
        // 아주 작은 소리나 무음일 때 로그 에러(-Infinity) 방지
        if (rms < 0.0001f) return 0f;

        // 기준 레벨을 1.0f로 하여 dBFS 단위의 데시벨 값을 계산
        float db = 20 * Mathf.Log10(rms);

        // minDecibels(기본 -50dB)에서 0dB까지 범위 내에서 선형 보간하여 0~1 값으로 변환
        return Mathf.Clamp01(Mathf.InverseLerp(minDecibels, 0f, db));
    }

    void OnDisable()
    {
        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
    }

    void OnApplicationQuit()
    {
        if (Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
    }
}