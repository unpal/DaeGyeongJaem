using Fusion;
using UnityEngine;

namespace Script.item.items
{
    public class JJB : NetworkBehaviour, IItem
    {
        [SerializeField] private float recoverAmount = 10f;
        public Sprite itemSprite;

        [Networked] private NetworkBool IsCollected { get; set; }

        public ItemData _itemData => new ItemData("JJB", 0f, itemSprite, OnUse);

        private void Awake()
        {
            ItemData.RegisterSprite("JJB", itemSprite);
        }

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
            if (Object == null || !Object.HasStateAuthority || IsCollected || target == null)
                return;

            if (target.GetComponentInParent<PlayerCondition>() != null && OnPickup(target))
            {
                IsCollected = true;
                Runner.Despawn(Object);
            }
        }

        public bool OnPickup(GameObject player)
        {
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
            condition.RecoverStamina(recoverAmount);
        }
    }
}
