using Script.sound;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

public class Shotgun : MonoBehaviour
{
    // Start is called before the first frame update
    SoundFollowingAgent agent;
    public GameObject hitbox;
    public ParticleSystem particles;
    public Vector3 LastRotate;//마지막 총쏜 방향(Debug.Ray 확인용)
    public GameObject ParentObj;//내 부모 오브젝트
    void Start()
    {
        transform.parent.TryGetComponent(out agent);
        agent.onFireEvent += Fire;
    }
    private void Update()
    {
        Debug.DrawRay(
        transform.position,
        LastRotate * 5.0f,
        Color.red
        );
    }

    private void Fire(Vector3 Rotate)
    {
        StartCoroutine(PrivateFire(Rotate));
    }

    private IEnumerator PrivateFire(Vector3 Rotate)
    {
        LayerMask shootMask = LayerMask.GetMask("Player", "Wall");
        RaycastHit hit;
        LastRotate = Rotate;
        yield return new WaitForSeconds(0.1f);
        if (Physics.Raycast(ParentObj.transform.position, Rotate,out hit, 10f, shootMask))
        {
            PlayerCondition condition = hit.transform.GetComponent<PlayerCondition>();
            if(condition != null)
            {
                condition.ApplyPermanentDamage(70);
                Debug.Log("총에 맞음!");
            }
        }
        particles.Play();
    }
}
