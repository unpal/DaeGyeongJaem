using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.item
{
    [Serializable]
    public class ItemData
    {
        private static readonly Dictionary<string, Sprite> RegisteredSprites = new();

        public string ItemName;
        public float AmountHeal;
        public Sprite ItemSprite;

        private readonly Action<PlayerCondition> _onUseCallback;

        public ItemData(
            string itemName,
            float amountHeal,
            Sprite itemSprite = null,
            Action<PlayerCondition> onUseCallback = null)
        {
            ItemName = itemName;
            AmountHeal = amountHeal;
            if (itemSprite != null)
            {
                RegisterSprite(itemName, itemSprite);
            }
            ItemSprite = itemSprite != null ? itemSprite : GetRegisteredSprite(itemName);
            _onUseCallback = onUseCallback;
        }

        public void OnUse(PlayerCondition condition = null)
        {
            if (_onUseCallback != null)
            {
                _onUseCallback.Invoke(condition);
                return;
            }

            if (condition != null && AmountHeal > 0f)
            {
                condition.CancelRevive();
                condition.RecoverTemporaryDamage(AmountHeal);
            }
        }

        public static void RegisterSprite(string itemName, Sprite sprite)
        {
            if (string.IsNullOrEmpty(itemName) || sprite == null)
                return;

            RegisteredSprites[itemName] = sprite;
        }

        public static Sprite GetRegisteredSprite(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return null;

            return RegisteredSprites.TryGetValue(itemName, out Sprite sprite) ? sprite : null;
        }
    }

    public interface IItem
    {
        ItemData _itemData { get; }
        bool OnPickup(GameObject player);
        void OnUse(PlayerCondition condition);
    }
}
