using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PublicSpeaker : MonoBehaviour
{
    public static PublicSpeaker Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource BGMSpeaker; // BGM용
    public AudioSource OtherSpeaker; // 효과음용

    [Header("Audio Clips")]
    public AudioClip bgmNormal;
    public AudioClip bgmEndgame;
    public AudioClip sfxChaserSpawn;
    public AudioClip sfxPortalSpawn;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayBGM(bool isEndgame)
    {
        if (BGMSpeaker == null) return;

        AudioClip clipToPlay = isEndgame ? bgmEndgame : bgmNormal;
        
        if (clipToPlay != null)
        {
            BGMSpeaker.clip = clipToPlay;
            BGMSpeaker.Play();
        }
    }

    public void PlayChaserSpawn()
    {
        if (OtherSpeaker != null && sfxChaserSpawn != null)
        {
            OtherSpeaker.PlayOneShot(sfxChaserSpawn);
        }
    }

    public void PlayPortalSpawn()
    {
        if (OtherSpeaker != null && sfxPortalSpawn != null)
        {
            OtherSpeaker.PlayOneShot(sfxPortalSpawn);
        }
    }
}
