using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIK : MonoBehaviour
{
    public Un Player;
    [Header("Climb Shared Settings")]
    [SerializeField]
    private Animator animator;

    private Vector3 _currentRightHandPos;
    private Vector3 _currentLeftHandPos;
    private Vector3 _currentRightFootPos;
    private Vector3 _currentLeftFootPos;
    private void OnAnimatorIK(int layerIndex)
    {

        if (animator == null) return;
        if (layerIndex != 0) return; // 베이스 레이어에서만 실행 (다중 레이어 중복 호출 방지)

        float currentHandWeight = (Player.statemachine == Un.StateMachine.Wall) ? Player.handikweight : 0f;
        float currentFootWeight = (Player.statemachine == Un.StateMachine.Wall) ? Player.footikweight : 0f;

        // 목표 위치가 아직 잡히지 않은 경우(Vector3.zero) 애니메이션 기본 위치로 초기화
        if (_currentRightHandPos == Vector3.zero) _currentRightHandPos = animator.GetIKPosition(AvatarIKGoal.RightHand);
        if (_currentLeftHandPos == Vector3.zero)  _currentLeftHandPos = animator.GetIKPosition(AvatarIKGoal.LeftHand);
        if (_currentRightFootPos == Vector3.zero) _currentRightFootPos = animator.GetIKPosition(AvatarIKGoal.RightFoot);
        if (_currentLeftFootPos == Vector3.zero) _currentLeftFootPos = animator.GetIKPosition(AvatarIKGoal.LeftFoot);

        // --- 오른손 ---
        IKSet(AvatarIKGoal.RightHand, currentHandWeight, Player._rightHandPos, ref _currentRightHandPos);

        // --- 왼손 ---
        IKSet(AvatarIKGoal.LeftHand, currentHandWeight, Player._leftHandPos, ref _currentLeftHandPos);

        // --- 오른발 ---
        IKSet(AvatarIKGoal.RightFoot, currentFootWeight, Player._rightFootPos, ref _currentRightFootPos);

        // --- 왼발 ---
        IKSet(AvatarIKGoal.LeftFoot, currentFootWeight, Player._leftFootPos, ref _currentLeftFootPos);
    }

    private void IKSet(AvatarIKGoal goal, float weight, Vector3 targetPos, ref Vector3 currentSmoothedPos)
    {
        animator.SetIKPositionWeight(goal, weight);

        if (weight > 0.001f && targetPos != Vector3.zero)
        {
            // 이전 프레임의 보간 위치에서 목표 위치로 서서히 이동
            currentSmoothedPos = Vector3.Lerp(currentSmoothedPos, targetPos, Player.tweenconst);
            animator.SetIKPosition(goal, currentSmoothedPos);
        }
        else
        {
            // IK가 꺼져있을 때는 기본 애니메이션 위치 동기화
            currentSmoothedPos = animator.GetIKPosition(goal);
        }
    }
}
