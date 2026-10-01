using Fusion;
using UnityEngine;

namespace Script.item.items
{
    public class JJB : NetworkBehaviour, IItem
    {
        [SerializeField] private float recoverAmount = 10f;

        [Networked] private NetworkBool IsCollected { get; set; }

        public string ItemName => "JJB";

        private void OnTriggerEnter(Collider other)
        {
            HandlePickup(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandlePickup(collision.gameObject);
        }

        private void HandlePickup(GameObject target)
        {
            // 호스트(StateAuthority)에서만 획득 판정 및 Despawn 처리
            if (!Object.HasStateAuthority || IsCollected)
                return;

            if (target.TryGetComponent(out PlayerCondition _))
            {
                IsCollected = true;
                OnPickup(target);
                Runner.Despawn(Object);
            }
        }

        public void OnPickup(GameObject player)
        {
            if (player.TryGetComponent(out PlayerCondition condition))
            {
                condition.RecoverStamina(recoverAmount);
            }
        }
    }
}
