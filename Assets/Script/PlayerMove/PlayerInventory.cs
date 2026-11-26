using Script.item;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerInventory : MonoBehaviour
{
    public const int MaxSlots = 3;

    [SerializeField] private PlayerCondition Condition;
    [SerializeField] private List<ItemData> MyItem = new();
    [SerializeField] private int InventoryNum = -1;

    private PlayerGameState gameState;
    private Un ownerUn;
    private PrototypeItemCatalog catalog;

    public IReadOnlyList<ItemData> Items => MyItem;
    public int SelectedSlot => InventoryNum;

    private void Awake()
    {
        EnsureReferences();
        InventoryNum = -1;
    }

    private void EnsureReferences()
    {
        if (Condition == null)
            Condition = GetComponent<PlayerCondition>();
        if (gameState == null)
            gameState = GetComponent<PlayerGameState>();
        if (ownerUn == null)
            ownerUn = GetComponent<Un>();
        if (catalog == null) catalog = PrototypeItemCatalog.Load();
    }

    public bool AddItem(ItemData Item)
    {
        EnsureReferences();

        if (Item == null || MyItem.Count >= MaxSlots)
            return false;

        if (gameState != null && (gameState.IsSeeker || !gameState.IsInPlayground || gameState.IsDowned))
            return false;

        MyItem.Add(Item);
        ownerUn?.NotifyInventoryAdded(Item);
        return true;
    }

    public void ToggleSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
        {
            InventoryNum = -1;
            return;
        }

        InventoryNum = InventoryNum == slotIndex ? -1 : slotIndex;
    }

    public void SwitchInventory(int Num)
    {
        InventoryNum = (Num >= -1 && Num < MaxSlots) ? Num : -1;
    }

    public bool UseItem(int slotIndex)
    {
        EnsureReferences();

        if (Condition == null)
            return false;

        if (gameState != null && (gameState.IsSeeker || !gameState.IsInPlayground || gameState.IsDowned))
            return false;

        if (slotIndex < 0 || slotIndex >= MyItem.Count)
            return false;

        ItemData item = MyItem[slotIndex];
        // Revive items are consumed only after the existing hold-to-revive completes.
        if (item != null && item.ItemName == "ReviveItem") return false;
        Condition.CancelRevive();
        if (item != null && item.ItemName == ItemData.WaterGunItemName)
        {
            ownerUn?.UseWaterGun();
            return true;
        }
        item?.OnUse(Condition);
        MyItem.RemoveAt(slotIndex);
        InventoryNum = -1;
        ownerUn?.NotifyInventoryRemoved(slotIndex);
        return true;
    }

    public bool ReleaseItem(int slotIndex, Vector3 direction, bool throwing)
    {
        EnsureReferences();
        if (gameState == null || gameState.Object == null || !gameState.Object.HasStateAuthority ||
            !gameState.CanMove || gameState.IsSeeker || slotIndex < 0 || slotIndex >= MyItem.Count) return false;
        float magnitude = direction.sqrMagnitude;
        if (float.IsNaN(magnitude) || float.IsInfinity(magnitude) || magnitude < 0.01f) return false;
        var entry = catalog != null ? catalog.Find(MyItem[slotIndex].ItemName) : null;
        if (entry == null || entry.prefab == null) return false;
        direction.Normalize();
        Vector3 origin = ownerUn.GetCameraPivotPosition();
        // Keep the spawn point on this side of walls, including when looking straight down.
        Vector3 position = origin + direction * 0.7f;
        if (PrototypeItemAim.Raycast(gameObject.scene.GetPhysicsScene(), origin, direction, 0.85f, transform, out RaycastHit hit))
            position = origin + direction * Mathf.Max(0, hit.distance - 0.2f);
        Fusion.NetworkObject spawned = gameState.Runner.Spawn(entry.prefab, position, Quaternion.identity);
        if (spawned == null) return false;
        var world = spawned.GetComponent<PrototypeWorldItem>();
        if (world == null) { gameState.Runner.Despawn(spawned); return false; }
        world.Release(throwing ? direction * 9f + Vector3.up * 2f : Vector3.zero, transform);
        Condition.CancelRevive();
        MyItem.RemoveAt(slotIndex);
        if (entry.name == "ReviveItem") Condition.RemoveReviveItem(false);
        InventoryNum = -1;
        ownerUn.NotifyInventoryRemoved(slotIndex);
        return true;
    }

    public bool RemoveFirst(string itemName)
    {
        EnsureReferences();
        int index = MyItem.FindIndex(item => item != null && item.ItemName == itemName);
        if (index < 0) return false;
        MyItem.RemoveAt(index); InventoryNum = -1;
        ownerUn?.NotifyInventoryRemoved(index);
        return true;
    }

    public void UseItem()
    {
        UseItem(InventoryNum);
    }

    public void ClearInventory()
    {
        EnsureReferences();
        MyItem.Clear();
        InventoryNum = -1;
        ownerUn?.NotifyInventoryCleared();
    }

    public void SyncAddItem(ItemData item)
    {
        if (item == null || MyItem.Count >= MaxSlots)
            return;

        MyItem.Add(item);
    }

    public void SyncRemoveAt(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < MyItem.Count)
            MyItem.RemoveAt(slotIndex);

        InventoryNum = -1;
    }

    public void SyncClear()
    {
        MyItem.Clear();
        InventoryNum = -1;
    }
}
