using Script.item;
using Script.item.items;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private PlayerCondition Condition;//
    [SerializeField] private List<ItemData> MyItem = new();
    [SerializeField] private int InventoryNum = 0;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public bool AddItem(ItemData Item)
    {
        if (MyItem.Count >= 3)
            return false;
        MyItem.Add(Item);
        return true;
    }
    public void SwitchInventory(int Num)
    {
        InventoryNum = Num;
    }

    public void UseItem()
    {
        if (Condition == null)
            return;
        if (InventoryNum < 0 || InventoryNum >= MyItem.Count)
            return;
        Condition.RecoverTemporaryDamage(MyItem[InventoryNum].AmountHeal);
        MyItem.RemoveAt(InventoryNum);
    }

}
