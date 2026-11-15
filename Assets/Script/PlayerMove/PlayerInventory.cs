using Script.item;
using Script.item.items;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private IItem[] MyItem = new IItem[3];
    [SerializeField] private int InventoryNum = 0;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public bool AddItem(IItem Item)
    {
        for(int i = 0; i < 3;i++)
        {
            if (MyItem[i] == null)
            {
                MyItem[i] = Item;
                return true;
            }
            
        }
        return false;
    }
    public void SwitchInventory(int Num)
    {
        InventoryNum = Num;
    }

    public void UseItem()
    {
        if (InventoryNum < 0 || InventoryNum >= MyItem.Length)
        {
            Debug.Log("인벤토리 넘버가 내 인벤토리를 넘어감");
            return;
        }
        if (MyItem[InventoryNum] == null)
        {
            Debug.Log("아이템 없음");
            return;
        }
        MyItem[InventoryNum].OnUse(gameObject);

        MyItem[InventoryNum] = null;

        Debug.Log("정상 아이템 사용");
    }

}
