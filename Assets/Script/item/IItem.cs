using UnityEngine;

namespace Script.item
{
    public interface IItem
    {
        string ItemName { get; }
        void OnPickup(GameObject player);
    }
}
