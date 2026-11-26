using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyShaderSetting : MonoBehaviour
{
    [SerializeField] private Material PlayerMaterial;
    [SerializeField] private GameObject PlayerObj;
    public float distance = 0;
    void Start()
    {
       PlayerMaterial = PlayerObj.GetComponent<SkinnedMeshRenderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
        PlayerMaterial.SetFloat("_Alpha", distance);
    }
}
