using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Script.sound
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class SoundFollowingAgent : MonoBehaviour
    {
        private NavMeshAgent _agent;
        public float correctness = 10.0f;
        public GameObject target;
        private HashSet<Vector3> _visited;
        private HashSet<Vector3> _unvisited;
        private Vector3[] _constAll;
        private Vector3 _targetPosition = Vector3.zero;
    
        // 소리가 났다고 추정한 최종 위치를 기억해야 해
        private Vector3 _estimatedSoundPosition = Vector3.zero; 
    
        public int distanceForVisit = 1;

        public enum StateMachine
        {
            IntoTheUnknown, // 모르는 길 (더듬기)
            WhatYouGonnaDo  // 아는 길 (빠르게 이동)
        }

        public StateMachine _state = StateMachine.IntoTheUnknown;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            // NavMesh의 모든 꼭짓점을 가져옴[cite: 1]
            _constAll = NavMesh.CalculateTriangulation().vertices;
            _unvisited = new HashSet<Vector3>(_constAll);
            _visited = new HashSet<Vector3>();
        }

        private void OnEnable()
        {
            SoundEventManager.OnSoundTriggered += HandleSoundTriggered;
            StartCoroutine(UpdatePathRoutine());
        }

        private void OnDisable()
        {
            SoundEventManager.OnSoundTriggered -= HandleSoundTriggered;
            StopCoroutine(UpdatePathRoutine());
        }

        private void FixedUpdate()
        {
            // 1. 에러 방지: 새로 방문한 노드들을 담을 임시 리스트
            List<Vector3> newlyVisited = new List<Vector3>();
            float sqrDist = distanceForVisit * distanceForVisit;

            // 2. unvisited 순회 (LINQ 대신 명시적 foreach로 성능 확보)
            foreach (var node in _unvisited)
            {
                if ((node - transform.position).sqrMagnitude <= sqrDist)
                {
                    newlyVisited.Add(node);
                }
            }

            // 3. 순회 종료 후 안전하게 추가/삭제
            foreach (var node in newlyVisited)
            {
                _visited.Add(node);
                _unvisited.Remove(node);
            }
        }

        private void HandleSoundTriggered(Vector3 soundPosition, float soundRange)
        {
            if (!_agent || !_agent.isActiveAndEnabled) return;

            var soundHeard = (transform.position - soundPosition).magnitude / soundRange;
        
            // 추정 위치를 전역 변수에 저장해둬야 코루틴에서 써먹을 수 있음
            _estimatedSoundPosition = soundPosition +
                                      new Vector3(Random.Range(-soundHeard, soundHeard), 0,
                                          Random.Range(-soundHeard, soundHeard)) / correctness;

            // 내가 아는 지점(_visited) 중 추정 위치와 가장 '가까운' 점 찾기
            var closestKnownPoint = transform.position;
            float minDistance = float.MaxValue;

            foreach (var visitedNode in _visited)
            {
                float dist = (_estimatedSoundPosition - visitedNode).sqrMagnitude;
                if (dist < minDistance) // 부등호 논리 수정
                {
                    minDistance = dist;
                    closestKnownPoint = visitedNode;
                }
            }

            // 가장 가까운 아는 곳으로 먼저 이동하도록 타겟 설정
            _targetPosition = closestKnownPoint;
            _state = StateMachine.WhatYouGonnaDo; 
        }

        private IEnumerator UpdatePathRoutine()
        {
            while (true)
            {
                if (_state == StateMachine.WhatYouGonnaDo)
                {
                    // 아는 길을 따라가는 중
                    if (_targetPosition != Vector3.zero)
                        _agent.SetDestination(_targetPosition);
                
                    if (target)
                        target.transform.position = _targetPosition;

                    // 목적지에 얼추 도착했다면 상태 전환
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
                    {
                        _state = StateMachine.IntoTheUnknown;
                    }
                
                    yield return new WaitForSeconds(0.2f);
                }
                else // IntoTheUnknown 상태
                {
                    // 아는 길의 끝에 왔는데 목표에 안 닿았다면, 추정 위치를 향해 조금씩 더듬으며 전진 (비어있던 로직 채움)
                    Vector3 direction = (_estimatedSoundPosition - transform.position).normalized;
                
                    // 앞을 향해 2m 정도 목표를 잡고 맹목적으로 나아감
                    _targetPosition = transform.position + direction * 2.0f; 
                
                    _agent.SetDestination(_targetPosition);
                
                    if (target)
                        target.transform.position = _targetPosition;

                    yield return new WaitForSeconds(0.5f); // 더듬는 건 0.5초마다 갱신
                }
            }
        }

        private Vector3 FindNearestPoint(IEnumerable<Vector3> points, Vector3 targetPosition)
        {
        }
        
    }
}