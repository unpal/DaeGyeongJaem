using Fusion;
using Script.sound;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class Shotgun : NetworkBehaviour
{
    // Start is called before the first frame update
    public SoundFollowingAgent agent;
    //public GameObject hitbox;
    public ParticleSystem particles;
    public Vector3 LastRotate;//마지막 총쏜 방향(Debug.Ray 확인용)
    public GameObject ParentObj;//내 부모 오브젝트
    public Animator Anim;
    public override void Spawned()
    {
        //transform.parent.TryGetComponent(out agent);
        agent.onFireEvent += Fire;
    }
    private void Update()
    {
        Debug.DrawRay(
        transform.position,
        LastRotate * 8.0f,
        Color.red
        );
        Debug.DrawRay(
        ParentObj.transform.position,
        LastRotate * 10f,
        Color.blue,
        1f
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

        Vector3 halfExtents = new Vector3(0.25f, 0.25f, 0.25f);

        if (Physics.BoxCast(
                transform.position,
                halfExtents,
                Rotate.normalized,
                out hit,
                transform.rotation,
                10f,
                shootMask))
        {
            PlayerCondition condition = hit.transform.GetComponent<PlayerCondition>();
            if (condition != null)
            {
                condition.ApplyPermanentDamage(70);
                Debug.Log("총에 맞음!");
            }
        }
        particles.Play();
    }
}
