using Script.item;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int MaxSlots = 3;

    [SerializeField] private PlayerCondition Condition;
    [SerializeField] private List<ItemData> MyItem = new();
    [SerializeField] private int InventoryNum = -1;

    private PlayerGameState gameState;
    private Un ownerUn;

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
        Condition.CancelRevive();
        item?.OnUse(Condition);
        MyItem.RemoveAt(slotIndex);
        InventoryNum = -1;
        ownerUn?.NotifyInventoryRemoved(slotIndex);
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
