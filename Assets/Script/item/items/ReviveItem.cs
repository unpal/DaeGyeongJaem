using Fusion;
using UnityEngine;

namespace Script.item.items
{
    // 기존 아이템처럼 닿으면 획득. 실제 소모는 10초 소생이 끝났을 때 한다.
    public class ReviveItem : NetworkBehaviour, IItem
    {
        [Networked] private NetworkBool IsCollected { get; set; }
        public string ItemName => "ReviveItem";
        public ItemData _itemData => new ItemData("JJB", 0);//Temporary. 이쪽의 변경 사항을 무시해 주세요.
        private void OnTriggerEnter(Collider other)
        {
            OnPickup(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnPickup(collision.gameObject);
        }

        public bool OnPickup(GameObject player) //Temporary. 이쪽의 변경 사항을 무시해 주세요
        {
            if (Object == null || !Object.HasStateAuthority || IsCollected || player == null)
                return false; //Temporary. 이쪽의 변경 사항을 무시해 주세요

            PlayerCondition condition = player.GetComponentInParent<PlayerCondition>();
            if (condition == null || !condition.AddReviveItem())
                return false; //Temporary. 이쪽의 변경 사항을 무시해 주세요

            IsCollected = true;
            Runner.Despawn(Object);
            return true; //Temporary. 이쪽의 변경 사항을 무시해 주세요
        }
    }
}
