using System;
using UnityEngine;

public class ComponentPool<T> where T : Component
{
    private readonly T[] slots;
    private int availableCount;

    public ComponentPool(int capacity, Transform parent, Func<Transform, T> create)
    {
        int count = capacity > 0 ? capacity : 1;
        slots = new T[count];
        availableCount = count;

        for (int i = 0; i < count; i++)
        {
            T item = create(parent);
            item.gameObject.SetActive(false);
            slots[i] = item;
        }
    }

    public T Rent()
    {
        if (availableCount <= 0)
            return null;

        availableCount--;
        T item = slots[availableCount];
        item.gameObject.SetActive(true);
        return item;
    }

    public void Return(T item)
    {
        if (item == null || availableCount >= slots.Length)
            return;

        item.gameObject.SetActive(false);
        slots[availableCount] = item;
        availableCount++;
    }
}
