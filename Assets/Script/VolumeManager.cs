using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeManager : MonoBehaviour
{
    public static VolumeManager instance;
    public PlayerGameState player;
    public GameObject globalVolume;
    public GameObject wbVolume;
    public bool _WasisDead = false;
    public bool _WashasEscaped = false;
    private void Awake()
    {
        instance = this;
    }


    private void Update()
    {
        if (!player) return;

        bool isDead = player.IsDead;
        bool hasEscaped = player.HasEscaped;
        //Debug.Log(isDead+ "+" + hasEscaped);
        if (!_WasisDead && isDead)
        {
            globalVolume?.SetActive(false);
            wbVolume?.SetActive(true);
        }
        else if (!_WashasEscaped && hasEscaped)
        {
            globalVolume?.SetActive(false);
        }
        else if (_WasisDead&& !isDead)
        {
            globalVolume?.SetActive(true);
            wbVolume?.SetActive(false);
            Debug.Log("WASIS DEAD");
        }
        else if (_WashasEscaped&& !hasEscaped)
        {
            globalVolume?.SetActive(true);
            wbVolume?.SetActive(false);
            Debug.Log("WASIS ESCADED");
        }
        _WasisDead = isDead;
        _WashasEscaped = hasEscaped;
    }
    public void Global(bool active)
    {
        globalVolume?.SetActive(active);
        Debug.Log("global "+active);
    }
    
    public void WB(bool active)
    {
        wbVolume?.SetActive(active);
        Debug.Log("wb "+active);
    }
    
    
    
}
