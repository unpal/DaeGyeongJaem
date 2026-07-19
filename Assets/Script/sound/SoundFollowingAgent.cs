using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Cinemachine;

/*
 * 객체지향을 무시한 추적자 코드입니다
 */
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
        public float maxSearchTime = 5.0f; // 전진 탐색 최대 시간
        private float _searchTimer = 0f;
        private float _originalSpeed;

        public bool showDebugGizmos = true; // 시각화 토글용 변수
        public float nodeSpacing = 2.0f; // 노드(점) 간의 간격

        [Header("Attack Settings")] public float fireRange = 5.0f; // 사격 범위 (이 거리 내에서 소리가 나면 발사)
        public float standStillAfterFire = 3.0f; // 발사 후 대기 시간
        public UnityEvent onFireEvent; // 총 발사 시 호출할 이벤트 (파티클, 발사 로직 등 연결)

        [Header("Gun Feedback Settings")]
        public AudioClip gunSoundClip; // 총소리 오디오 클립
        public float gunSoundVolume = 1.0f; // 총소리 볼륨
        public float cameraShakeForce = 1.0f; // 카메라 흔들림 강도
        public float cameraShakeMaxDistance = 30.0f; // 카메라 흔들림이 전달되는 최대 거리
        
        private CinemachineImpulseSource _impulseSource;

        private enum StateMachine
        {
            IntoTheUnknown, // 모르는 길 (더듬기)
            WhatYouGonnaDo, // 아는 길 (빠르게 이동)
            Idle, // 배회 (느리게 탐색)
            AttackCooldown, // 공격 후 대기
            KnowWhereYouAre
        }

        private StateMachine _state = StateMachine.IntoTheUnknown;

        public void SetStateToKnowWhereYouAre()
        {
            _state = StateMachine.KnowWhereYouAre;
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _originalSpeed = _agent.speed;
            
            // 임펄스 소스 추가 (카메라 흔들림용)
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (_impulseSource == null)
            {
                _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
                _impulseSource.m_ImpulseDefinition.m_ImpulseChannel = 1;
                _impulseSource.m_ImpulseDefinition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Legacy;
                _impulseSource.m_ImpulseDefinition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
                _impulseSource.m_DefaultVelocity = Vector3.down; // 흔들림 기본 방향
            }
        }

        private void Start()
        {
            // NavMesh 표면에 일정한 간격으로 점들을 생성
            _constAll = GenerateEvenlySpacedPoints(nodeSpacing);
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

        private void FireAndStandStill(Vector3 targetPosition)
        {
            // 발사 방향을 향해 회전
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // 평면 회전
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            PlayFireFeedback();

            onFireEvent?.Invoke();
            _state = StateMachine.AttackCooldown;

            if (!_agent.isActiveAndEnabled) return;
            
            _agent.ResetPath();
            _agent.isStopped = true;
        }

        private void HandleSoundTriggered(Vector3 soundPosition, float soundRange)
        {
            if (!_agent || !_agent.isActiveAndEnabled) return;

            if (_state == StateMachine.AttackCooldown || _state == StateMachine.KnowWhereYouAre)
                return; // 대기 중이거나 확실한 추적 중에는 추가 소리 무시

            // 에이전트 주변 일정 범위(fireRange) 내에서 소리가 났다면 사격
            if ((transform.position - soundPosition).sqrMagnitude <= fireRange * fireRange)
            {
                FireAndStandStill(soundPosition);
                return;
            }

            var soundHeard = (transform.position - soundPosition).magnitude / soundRange;

            // 추정 위치를 전역 변수에 저장해둬야 코루틴에서 써먹을 수 있음
            _estimatedSoundPosition = soundPosition +
                                      new Vector3(Random.Range(-soundHeard, soundHeard), 0,
                                          Random.Range(-soundHeard, soundHeard)) / correctness;

            // 에이전트 구역, 도달 가능 목적지 구역, 일반 목적지 구역 계산
            Vector3 agentZone =
                _visited.Count > 0 ? FindNearestPoint(_visited, transform.position) : transform.position;
            Vector3 reachableDestZone = _visited.Count > 0
                ? FindNearestPoint(_visited, _estimatedSoundPosition)
                : transform.position;
            Vector3 generalDestZone = _constAll.Length > 0
                ? FindNearestPoint(_constAll, _estimatedSoundPosition)
                : transform.position;

            _agent.speed = _originalSpeed; // 소리를 들었으니 원래 속도로 복귀
            _searchTimer = 0f; // 탐색 타이머 초기화

            if (reachableDestZone == generalDestZone)
            {
                // 1. 도달 가능 목적지 구역 == 일반 목적지 구역
                // 에이전트는 해당 구역에 방문한 적 있으므로, 목적지까지의 정확한 경로를 계산하여 이동
                _targetPosition = _estimatedSoundPosition;
                _state = StateMachine.WhatYouGonnaDo;
            }
            else
            {
                // 2. 도달 가능 목적지 구역 != 일반 목적지 구역
                if (agentZone == reachableDestZone)
                {
                    // 2-1. 에이전트 구역 == 도달 가능 목적지 구역
                    // 에이전트는 올 수 있는 최대치까지 온 상태. 목적지 방향을 향해 조금씩 전진 탐색
                    _state = StateMachine.IntoTheUnknown;
                }
                else
                {
                    // 2-2. 에이전트 구역 != 도달 가능 목적지 구역
                    // 어렴풋한 길을 아는 상태. 도달 가능 목적지 구역까지 간 후, 전진 탐색
                    _targetPosition = reachableDestZone;
                    _state = StateMachine.WhatYouGonnaDo;
                }
            }
        }

        private IEnumerator UpdatePathRoutine()
        {
            while (true)
            {
                if (_state == StateMachine.AttackCooldown)
                {
                    // 설정한 대기 시간만큼 가만히 있기
                    yield return new WaitForSeconds(standStillAfterFire);

                    if (_state == StateMachine.AttackCooldown)
                    {
                        if (_agent.isActiveAndEnabled)
                            _agent.isStopped = false; // 다시 이동 가능하게 설정

                        _state = StateMachine.Idle;
                        _agent.speed = _originalSpeed * 0.3f;
                    }
                }
                else if (_state == StateMachine.WhatYouGonnaDo)
                {
                    // 아는 길을 따라가는 중
                    if (_targetPosition != Vector3.zero)
                        _agent.SetDestination(_targetPosition);

                    if (target)
                        target.transform.position = _targetPosition;

                    // 목적지/도달 가능 목적지 구역에 도착했다면 상태 전환
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
                    {
                        // 실제 목표점(_estimatedSoundPosition)에 도달했는지 확인
                        if ((transform.position - _estimatedSoundPosition).sqrMagnitude <=
                            _agent.stoppingDistance * _agent.stoppingDistance + 0.1f)
                        {
                            _state = StateMachine.Idle;
                            _agent.speed = _originalSpeed * 0.3f;
                        }
                        else
                        {
                            // 도달 가능 구역까지만 온 거라면 전진 탐색 시작
                            _state = StateMachine.IntoTheUnknown;
                            _searchTimer = 0f;
                        }
                    }

                    yield return new WaitForSeconds(0.1f);
                }
                else if (_state == StateMachine.IntoTheUnknown)
                {
                    _searchTimer += 0.5f;

                    // 목적지에 도달했거나 탐색 시간을 초과한 경우
                    if (_searchTimer >= maxSearchTime || (_estimatedSoundPosition - transform.position).sqrMagnitude <=
                        _agent.stoppingDistance * _agent.stoppingDistance)
                    {
                        _state = StateMachine.Idle;
                        _agent.speed = _originalSpeed * 0.3f;
                    }
                    else
                    {
                        // 앞을 향해 2m 정도 목표를 잡고 맹목적으로 나아감
                        Vector3 direction = (_estimatedSoundPosition - transform.position).normalized;
                        _targetPosition = transform.position + direction * 5.0f;

                        _agent.SetDestination(_targetPosition);

                        if (target)
                            target.transform.position = _targetPosition;
                    }

                    yield return new WaitForSeconds(0.5f); // 더듬는 건 0.5초마다 갱신
                }
                else if (_state == StateMachine.Idle)
                {
                    // Idle 상태: 30% 속도로 가장 가까운 _unvisited 노드 탐색
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
                    {
                        if (_unvisited.Count > 0)
                        {
                            _targetPosition = FindNearestPoint(_unvisited, transform.position);
                            _agent.SetDestination(_targetPosition);

                            if (target)
                                target.transform.position = _targetPosition;
                        }
                    }

                    yield return new WaitForSeconds(1.0f);
                }
                else if (_state == StateMachine.KnowWhereYouAre)
                {
                    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                    GameObject nearestPlayer = null;
                    float minSqrDist = float.MaxValue;
                    bool bestIsReachable = false;

                    NavMeshPath path = new NavMeshPath();

                    foreach (var p in players)
                    {
                        if (p.TryGetComponent(out PlayerGameState state))
                        {
                            if (!state.IsInPlayground)
                            {
                                continue;
                            }
                        }
                        float sqrDist = (p.transform.position - transform.position).sqrMagnitude;
                        bool hasPath = _agent.CalculatePath(p.transform.position, path);
                        bool isReachable = hasPath && path.status == NavMeshPathStatus.PathComplete;

                        if (nearestPlayer == null)
                        {
                            nearestPlayer = p;
                            minSqrDist = sqrDist;
                            bestIsReachable = isReachable;
                        }
                        else
                        {
                            if (isReachable && !bestIsReachable)
                            {
                                nearestPlayer = p;
                                minSqrDist = sqrDist;
                                bestIsReachable = isReachable;
                            }
                            else if (isReachable == bestIsReachable && sqrDist < minSqrDist)
                            {
                                nearestPlayer = p;
                                minSqrDist = sqrDist;
                                bestIsReachable = isReachable;
                            }
                        }
                    }

                    if (nearestPlayer != null)
                    {
                        _agent.speed = _originalSpeed; // 추적 시에는 원래 속도로 복귀
                        _agent.SetDestination(nearestPlayer.transform.position);
                        if (target)
                            target.transform.position = nearestPlayer.transform.position;

                        if (minSqrDist <= fireRange * fireRange)
                        {
                            // 발사 방향을 향해 회전
                            Vector3 direction = (nearestPlayer.transform.position - transform.position).normalized;
                            direction.y = 0; // 평면 회전
                            if (direction.sqrMagnitude > 0.01f)
                            {
                                transform.rotation = Quaternion.LookRotation(direction);
                            }

                            PlayFireFeedback();

                            onFireEvent?.Invoke();

                            if (_agent.isActiveAndEnabled)
                            {
                                _agent.ResetPath();
                                _agent.isStopped = true;
                            }

                            // 발사 후 대기 (상태 전환 없이)
                            yield return new WaitForSeconds(standStillAfterFire);

                            if (_agent.isActiveAndEnabled)
                            {
                                _agent.isStopped = false;
                            }
                        }
                    }

                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private Vector3 FindNearestPoint(IEnumerable<Vector3> points, Vector3 targetPosition,
            bool checkLineOfSight = true)
        {
            Vector3 nearest = Vector3.zero;
            float minDistance = float.MaxValue;

            // 1. 점들을 거리와 함께 리스트에 저장
            List<KeyValuePair<Vector3, float>> sortedPoints = new List<KeyValuePair<Vector3, float>>();

            foreach (var point in points)
            {
                float sqrDist = (point - targetPosition).sqrMagnitude;
                sortedPoints.Add(new KeyValuePair<Vector3, float>(point, sqrDist));

                if (sqrDist < minDistance)
                {
                    minDistance = sqrDist;
                    nearest = point;
                }
            }

            if (!checkLineOfSight || sortedPoints.Count == 0)
                return nearest;

            // 2. 거리순으로 오름차순 정렬
            sortedPoints.Sort((a, b) => a.Value.CompareTo(b.Value));

            // 3. 가까운 점부터 차례대로 레이캐스트를 쏴서 시야(경로)가 뚫려있는지 확인
            // 연산량 폭발을 막기 위해 최대 30개의 점까지만 검사합니다.
            int checkCount = Mathf.Min(30, sortedPoints.Count);
            for (int i = 0; i < checkCount; i++)
            {
                Vector3 point = sortedPoints[i].Key;
                // NavMesh.Raycast는 직선 사이에 장애물(벽)이 있으면 true, 없으면 false를 반환합니다.
                if (!NavMesh.Raycast(targetPosition, point, out NavMeshHit hit, NavMesh.AllAreas))
                {
                    return point; // 시야가 확보된 가장 가까운 점
                }
            }

            // 시야가 확보된 점이 아예 없다면(모두 방 너머에 있는 등), 어쩔 수 없이 절대 거리가 가장 가까운 점을 반환
            return nearest;
        }

        private Vector3[] GenerateEvenlySpacedPoints(float spacing)
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            List<Vector3> points = new List<Vector3>();
            float sqrSpacing = spacing * spacing;

            // 빠른 거리 필터링을 위한 공간 해시 (Voxel Grid)
            HashSet<Vector3Int> voxelGrid = new HashSet<Vector3Int>();

            // 삼각형마다 넓이에 비례해 랜덤 점 생성
            for (int i = 0; i < triangulation.indices.Length; i += 3)
            {
                Vector3 v1 = triangulation.vertices[triangulation.indices[i]];
                Vector3 v2 = triangulation.vertices[triangulation.indices[i + 1]];
                Vector3 v3 = triangulation.vertices[triangulation.indices[i + 2]];

                // 삼각형의 면적 계산
                float area = Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;
                // 밀도를 보장하기 위해 넉넉히 생성 (면적이 작아도 최소 1개 보장)
                int samples = Mathf.Max(1, Mathf.CeilToInt((area / sqrSpacing) * 3.0f));

                for (int j = 0; j < samples; j++)
                {
                    // 삼각형 내 랜덤 좌표 생성 (Barycentric coordinates)
                    float r1 = Random.value;
                    float r2 = Random.value;
                    if (r1 + r2 > 1f)
                    {
                        r1 = 1f - r1;
                        r2 = 1f - r2;
                    }

                    Vector3 p = v1 + (v2 - v1) * r1 + (v3 - v1) * r2;

                    // Voxel 좌표 계산 (격자 크기를 spacing으로 하여 너무 가까운 점들을 버림)
                    Vector3Int voxel = new Vector3Int(
                        Mathf.RoundToInt(p.x / spacing),
                        Mathf.RoundToInt(p.y / spacing),
                        Mathf.RoundToInt(p.z / spacing)
                    );

                    // 해당 복셀 공간이 비어있다면 추가
                    if (voxelGrid.Add(voxel))
                    {
                        points.Add(p);
                    }
                }
            }

            // NavMesh 원본 꼭짓점(모서리 부분 등)도 중요하므로 추가
            foreach (var v in triangulation.vertices)
            {
                Vector3Int voxel = new Vector3Int(
                    Mathf.RoundToInt(v.x / spacing),
                    Mathf.RoundToInt(v.y / spacing),
                    Mathf.RoundToInt(v.z / spacing)
                );
                if (voxelGrid.Add(voxel))
                {
                    points.Add(v);
                }
            }

            return points.ToArray();
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;

            // 학습한 지점은 초록색 구체로 표시
            if (_visited != null)
            {
                Gizmos.color = Color.green;
                foreach (var node in _visited)
                {
                    Gizmos.DrawSphere(node, 0.2f);
                }
            }

            // 아직 모르는 지점은 작고 빨간 구체로 표시 (필요 없다면 주석 처리하셔도 좋습니다)
            if (_unvisited != null)
            {
                Gizmos.color = Color.red;
                foreach (var node in _unvisited)
                {
                    Gizmos.DrawSphere(node, 0.1f);
                }
            }
        }
    }
}