using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}

public class Pool<T> where T : Component
{
    private readonly Queue<T> _available = new Queue<T>();
    private readonly List<T> _all = new List<T>();
    private readonly T _prefab;
    private readonly Transform _parent;

    public Pool(T prefab, int initialSize, Transform parent)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < Mathf.Max(0, initialSize); i++)
        {
            var instance = CreateInstance();
            _available.Enqueue(instance);
        }
    }

    public T Get()
    {
        if (_available.Count == 0)
        {
            _available.Enqueue(CreateInstance());
        }

        var item = _available.Dequeue();
        item.gameObject.SetActive(true);

        if (item is IPoolable poolable)
        {
            poolable.OnSpawned();
        }

        return item;
    }

    public void Release(T item)
    {
        if (item == null)
        {
            return;
        }

        if (item is IPoolable poolable)
        {
            poolable.OnDespawned();
        }

        item.transform.SetParent(_parent, false);
        item.gameObject.SetActive(false);
        _available.Enqueue(item);
    }

    public void ReleaseAllActive()
    {
        for (int i = 0; i < _all.Count; i++)
        {
            var item = _all[i];
            if (item != null && item.gameObject.activeSelf)
            {
                Release(item);
            }
        }
    }

    private T CreateInstance()
    {
        var instance = UnityEngine.Object.Instantiate(_prefab, _parent);
        instance.gameObject.SetActive(false);
        _all.Add(instance);
        return instance;
    }
}
