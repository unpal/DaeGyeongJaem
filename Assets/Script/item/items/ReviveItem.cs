using Fusion;
using UnityEngine;

namespace Script.item.items
{
    // 기존 아이템처럼 닿으면 획득. 실제 소모는 10초 소생이 끝났을 때 한다.
    public class ReviveItem : NetworkBehaviour, IItem
    {
        [Networked] private NetworkBool IsCollected { get; set; }

        public ItemData _itemData => new ItemData("ReviveItem",0);

        private void OnTriggerEnter(Collider other)
        {
            OnPickup(other.gameObject);
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

            if (target.TryGetComponent(out global::Un _))
            {
                IsCollected = true;
                if (OnPickup(target))
                    Runner.Despawn(Object);
            }
        }

        public bool OnPickup(GameObject player)
        {
            if (player.TryGetComponent(out global::Un Player))
            {
                return Player._playerInventory.AddItem(this);
            }
            return false;
        }

        public bool OnUse(GameObject player)
        {
            throw new System.NotImplementedException();
        }
    }
}
