using System.Collections.Generic;
using Fusion;
using Script.sound;
using UnityEngine;

public class PlayerNoise : NetworkBehaviour
{
    private readonly Queue<AudioSource> audioPool = new();
    [SerializeField] private int countSources = 2;
    
    
    
    // Audio Clips
    [SerializeField] private AudioClip whistleClip;
    [SerializeField] private AudioClip walkClip;
    [SerializeField] private AudioClip runClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;
    [SerializeField] private AudioClip climbClip;

    private AudioClip Matcher(Vpx clip)
    {
        return clip switch
        {
            Vpx.Whistle => whistleClip,
            Vpx.Walk => walkClip,
            Vpx.Run => runClip,
            Vpx.Jump => jumpClip,
            Vpx.Land => landClip,
            Vpx.Climb => climbClip,
            _ => null
        };
    }

    public override void Spawned()
    {
        for (int i = 0; i < countSources; i++)
        {
            var childObj = new GameObject($"AudioSource_{i}");
            childObj.transform.SetParent(transform, false);
            
            var source = childObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1.0f;

            audioPool.Enqueue(source);
        }
    }
    public void Play(Vpx noiseType)
    {
        if (!HasInputAuthority) return;
        RPC_PlayNoise(noiseType);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayNoise(Vpx noiseType)
    {
        PlayAudioInternal(noiseType);
    }

    private void PlayAudioInternal(Vpx noiseType)
    {
        if (audioPool.Count == 0) return;

        var audioSource = audioPool.Dequeue();
        audioPool.Enqueue(audioSource);

        audioSource.clip = Matcher(noiseType);
        audioSource.Play();
    }
}