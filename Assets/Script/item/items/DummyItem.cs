using Fusion;
using UnityEngine;

namespace Script.item.items
{
    public class DummyItem : NetworkBehaviour, IItem
    {

        [Networked] private NetworkBool IsCollected { get; set; }

        public ItemData _itemData => new ItemData("DummyItem",30);

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

            if (target.TryGetComponent(out global::Un _))
            {
                IsCollected = true;
                if(OnPickup(target));
                    Runner.Despawn(Object);
            }
        }

        public bool OnPickup(GameObject player)
        {
            if (player.TryGetComponent(out global::Un Player))
            {
               return Player._playerInventory.AddItem(_itemData);
            }
            return false;
        }
    }
}
