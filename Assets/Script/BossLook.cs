using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossLook : MonoBehaviour
{
    public Animator animator;
    public Vector3 target;

    [Range(0, 1)]
    public float weight = 1f;

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || target == null)
            return;

        animator.SetLookAtWeight(weight);

        animator.SetLookAtPosition(target);
        Debug.Log("IK »£√‚");
    }
}
