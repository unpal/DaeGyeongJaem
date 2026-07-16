using System.Collections;
using System.Collections.Generic;
using Script.sound;
using UnityEngine;
using UnityEngine.Events;

public class Shotgun : MonoBehaviour
{
    // Start is called before the first frame update
    SoundFollowingAgent agent;
    public GameObject hitbox;
    public ParticleSystem particles;
    void Start()
    {
        transform.parent.TryGetComponent(out agent);
        agent.onFireEvent.AddListener(Fire);
    }

    private void Fire()
    {
        StartCoroutine(PrivateFire());
    }

    private IEnumerator PrivateFire()
    {
        hitbox.SetActive(true);
        particles.Play();
        yield return new WaitForSeconds(0.1f);
        hitbox.SetActive(false);
    }
}
