using Fusion;
using UnityEngine;

namespace Script.item.items
{
    // 기존 아이템처럼 닿으면 획득. 실제 소모는 10초 소생이 끝났을 때 한다.
    public class ReviveItem : NetworkBehaviour, IItem
    {
        [Networked] private NetworkBool IsCollected { get; set; }
        public string ItemName => "ReviveItem";
        public ItemData _itemData => new ItemData(ItemName, 0f);

        private void OnTriggerEnter(Collider other)
        {
            OnPickup(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnPickup(collision.gameObject);
        }

        public bool OnPickup(GameObject player)
        {
            if (Object == null || !Object.HasStateAuthority || IsCollected || player == null)
                return false;

            PlayerCondition condition = player.GetComponentInParent<PlayerCondition>();
            if (condition == null || !condition.AddReviveItem())
                return false;

            IsCollected = true;
            Runner.Despawn(Object);
            return true;
        }
    }
}
