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
    public Vector3 LastRotate;//������ �ѽ� ����(Debug.Ray Ȯ�ο�)
    public GameObject ParentObj;//�� �θ� ������Ʈ
    public Animator Anim;
    public GameObject light;
    public override void Spawned()
    {
        //transform.parent.TryGetComponent(out agent);
        if (agent != null) agent.onFireEvent += Fire;
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (agent != null) agent.onFireEvent -= Fire;
    }

    // Shared by the AI weapon and the player-controlled seeker. Call only on state authority.
    public static bool FireAt(Vector3 origin, Vector3 direction, Transform shooter, out Vector3 end)
    {
        direction.Normalize();
        end = origin + direction * 10f;
        RaycastHit[] hits = Physics.BoxCastAll(origin, Vector3.one * 0.25f, direction,
            Quaternion.LookRotation(direction), 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (shooter != null && hit.transform.IsChildOf(shooter)) continue;
            PlayerGameState target = hit.collider.GetComponentInParent<PlayerGameState>();
            if (target != null && (!target.IsInPlayground || target.IsSeeker)) continue;
            end = origin + direction * hit.distance;
            PlayerCondition condition = hit.collider.GetComponentInParent<PlayerCondition>();
            if (condition == null) return false; // Walls block shots.
            condition.ApplyPermanentDamage(70f);
            return true;
        }
        return false;
    }

    private static Material tracerMaterial;
    public static void ShowTracer(Vector3 origin, Vector3 end, bool hit)
    {
        if (tracerMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            tracerMaterial = new Material(shader);
        }
        GameObject visual = new GameObject("Shotgun tracer");
        LineRenderer line = visual.AddComponent<LineRenderer>();
        line.sharedMaterial = tracerMaterial;
        line.startWidth = 0.04f;
        line.endWidth = 0.01f;
        line.startColor = line.endColor = hit ? Color.red : Color.yellow;
        line.positionCount = 2;
        line.SetPosition(0, origin);
        line.SetPosition(1, end);
        Destroy(visual, 0.1f);
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
        if (!Object.HasStateAuthority) return;
        StartCoroutine(PrivateFire(Rotate));
    }

    private IEnumerator PrivateFire(Vector3 Rotate)
    {
        LastRotate = Rotate;
        
        light.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        light.SetActive(false);

        FireAt(transform.position, Rotate, ParentObj != null ? ParentObj.transform : transform, out _);
        particles.Play();
    }
}
