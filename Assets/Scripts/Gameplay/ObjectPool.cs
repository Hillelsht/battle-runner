using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Generic prewarmed LIFO pool (doc 04). Nothing gameplay-related is instantiated
    /// mid-run: pools are filled during RunLoading behind the transition, and pooled
    /// objects reset state in OnSpawned/OnDespawned — never in OnEnable.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    public sealed class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> _inactive = new Stack<T>();
        private readonly Func<T> _factory;
        private readonly Transform _poolRoot;
        private int _totalCreated;

        public int TotalCreated => _totalCreated;
        public int InactiveCount => _inactive.Count;

        public ObjectPool(Func<T> factory, Transform poolRoot)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _poolRoot = poolRoot;
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T item = Create();
                item.gameObject.SetActive(false);
                _inactive.Push(item);
            }
        }

        public T Get(Transform parent)
        {
            T item = _inactive.Count > 0 ? _inactive.Pop() : Create();
            item.transform.SetParent(parent, false);
            item.gameObject.SetActive(true);
            (item as IPoolable)?.OnSpawned();
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            (item as IPoolable)?.OnDespawned();
            item.gameObject.SetActive(false);
            item.transform.SetParent(_poolRoot, false);
            _inactive.Push(item);
        }

        private T Create()
        {
            T item = _factory();
            item.transform.SetParent(_poolRoot, false);
            _totalCreated++;
            return item;
        }
    }
}
