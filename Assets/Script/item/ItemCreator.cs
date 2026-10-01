using Fusion;
using UnityEngine;

namespace Script.item
{
    public class ItemCreator : NetworkBehaviour
    {
        [Header("Spawn Area Settings")]
        [Tooltip("아이템 생성 영역의 중심 좌표")]
        [SerializeField] private Vector3 origin = Vector3.zero;

        [Tooltip("수평(X, Z) 평면의 랜덤 범위 (dxz)")]
        [SerializeField] private float horizontalRangeXZ = 10f;

        [Tooltip("수직(Y) 탐색 높이 범위 (dy)")]
        [SerializeField] private float verticalRangeY = 10f;

        [Header("Ground Detection")]
        [Tooltip("지상으로 판정할 레이어 마스크")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("지상 레이캐스트 최대 탐색 거리")]
        [SerializeField] private float maxRayDistance = 30f;

        [Tooltip("지면으로부터 아이템을 띄울 높이")]
        [SerializeField] private float groundOffset = 0.5f;

        [Header("Item Prefabs")]
        [Tooltip("소환할 IItem 프리팹 목록")]
        [SerializeField] private NetworkPrefabRef[] itemPrefabs;

        // dxz, dy 별칭 프로퍼티
        public Vector3 Origin { get => origin; set => origin = value; }
        public float Dxz { get => horizontalRangeXZ; set => horizontalRangeXZ = value; }
        public float Dy { get => verticalRangeY; set => verticalRangeY = value; }

        /// <summary>
        /// 지정된 범위(origin, dxz, dy) 내에서 지상 거리를 판정한 후 랜덤한 IItem 프리팹을 1개 소환합니다.
        /// (서버/호스트 전용)
        /// </summary>
        /// <param name="maxRetryCount">지형 감지 실패 시 재시도 횟수 (기본 5회)</param>
        /// <returns>소환된 아이템 NetworkObject (실패 시 null)</returns>
        public NetworkObject SpawnRandomItem(int maxRetryCount = 5)
        {
            if (Runner == null || !Runner.IsServer)
            {
                Debug.LogWarning("[ItemCreator] 아이템 생성은 Server/Host 권한이 필요합니다.");
                return null;
            }

            if (itemPrefabs == null || itemPrefabs.Length == 0)
            {
                Debug.LogWarning("[ItemCreator] 소환할 itemPrefabs 목록이 설정되지 않았습니다.");
                return null;
            }

            for (int i = 0; i < maxRetryCount; i++)
            {
                // 1. 수평(XZ) 랜덤 좌표 선정 (dxz)
                float randomX = origin.x + Random.Range(-horizontalRangeXZ, horizontalRangeXZ);
                float randomZ = origin.z + Random.Range(-horizontalRangeXZ, horizontalRangeXZ);

                // 2. 수직(Y) 상공 레이캐스트 시작 위치 (dy)
                float startY = origin.y + verticalRangeY;
                Vector3 rayStart = new Vector3(randomX, startY, randomZ);

                // 3. 지상과의 거리 판정 (하향 Raycast)
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxRayDistance, groundLayer))
                {
                    // 지표면 접촉점 위에 groundOffset을 더한 위치
                    Vector3 spawnPosition = hit.point + Vector3.up * groundOffset;

                    // 4. 랜덤 프리팹 선택
                    int randomIndex = Random.Range(0, itemPrefabs.Length);
                    NetworkPrefabRef selectedPrefab = itemPrefabs[randomIndex];

                    // 5. 호스트에서 아이템 스폰
                    NetworkObject spawnedItem = Runner.Spawn(selectedPrefab, spawnPosition, Quaternion.identity);
                    return spawnedItem;
                }
            }

            Debug.LogWarning($"[ItemCreator] {maxRetryCount}회 시도 내에 유효한 지상을 찾지 못했습니다.");
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 center = origin;
            Vector3 size = new Vector3(horizontalRangeXZ * 2f, verticalRangeY * 2f, horizontalRangeXZ * 2f);
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(origin, 0.3f);
        }
    }
}
