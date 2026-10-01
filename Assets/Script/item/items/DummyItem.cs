using Fusion;
using UnityEngine;

namespace Script.item.items
{
    public class DummyItem : NetworkBehaviour, IItem
    {

        [Networked] private NetworkBool IsCollected { get; set; }

        public string ItemName => "DummyItem";

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

            if (target.TryGetComponent(out global::PlayerMove _))
            {
                IsCollected = true;
                OnPickup(target);
                Runner.Despawn(Object);
            }
        }

        public void OnPickup(GameObject player)
        {
            if (player.TryGetComponent(out global::PlayerMove move))
            {
                //버프 같은 것들
            }
        }
    }
}
