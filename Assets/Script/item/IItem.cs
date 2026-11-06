using Fusion;
using System;
using UnityEngine;

namespace Script.item
{
    [Serializable]
    public class ItemData
    {
        public string ItemName;
        public float AmountHeal;
        public ItemData(string itemName, float amountHeal)
        {
            ItemName = itemName;
            AmountHeal = amountHeal;
        }
    }
    public interface IItem
    {

        ItemData _itemData { get; }
        bool OnPickup(GameObject player);
    }
}
