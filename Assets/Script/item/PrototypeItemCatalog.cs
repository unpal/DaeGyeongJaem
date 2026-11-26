using System;
using Fusion;
using UnityEngine;

namespace Script.item
{
    [CreateAssetMenu(menuName = "JITAL/Item Catalog")]
    public sealed class PrototypeItemCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string name;
            public NetworkObject prefab;
            public Sprite icon;
        }
        public Entry[] entries = Array.Empty<Entry>();
        public static PrototypeItemCatalog Load() => Resources.Load<PrototypeItemCatalog>("PrototypeItemCatalog");
        public Entry Find(string name) => Array.Find(entries, entry => entry.name == name);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterIcons()
        {
            var catalog = Load();
            if (catalog == null) return;
            foreach (Entry entry in catalog.entries) ItemData.RegisterSprite(entry.name, entry.icon);
        }
    }
}
