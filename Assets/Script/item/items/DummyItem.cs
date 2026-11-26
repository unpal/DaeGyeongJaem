using Fusion;
using UnityEngine;

namespace Script.item.items
{
    public class DummyItem : NetworkBehaviour, IItem
    {
        [SerializeField] private float amountHeal = 30f;
        public Sprite itemSprite;

        [Networked] private NetworkBool IsCollected { get; set; }

        public ItemData _itemData => new ItemData("DummyItem", amountHeal, itemSprite, OnUse);

        private void Awake()
        {
            ItemData.RegisterSprite("DummyItem", itemSprite);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandlePickup(other.gameObject);
        }

        private void OnTriggerStay(Collider other) { HandlePickup(other.gameObject); }

        private void OnCollisionEnter(Collision collision)
        {
            HandlePickup(collision.gameObject);
        }

        private void HandlePickup(GameObject target)
        {
            // 호스트(StateAuthority)에서만 획득 판정 및 Despawn 처리
            if (Object == null || !Object.HasStateAuthority || IsCollected || target == null)
                return;

            if (target.GetComponentInParent<global::Un>() != null && OnPickup(target))
            {
                IsCollected = true;
                Runner.Despawn(Object);
            }
        }

        public bool OnPickup(GameObject player)
        {
            if (!PrototypeWorldItem.AllowsPickup(this)) return false;
            if (player == null)
                return false;

            global::Un playerUn = player.GetComponentInParent<global::Un>();
            if (playerUn == null || playerUn._playerInventory == null)
                return false;

            return playerUn._playerInventory.AddItem(_itemData);
        }

        public void OnUse(PlayerCondition condition)
        {
            if (condition == null)
                return;

            condition.CancelRevive();
            condition.RecoverAllDamage(amountHeal);
        }
    }
}
