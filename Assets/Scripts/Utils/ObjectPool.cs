using System.Collections.Generic;
using UnityEngine;

namespace FactoryDelivery.Utils
{
    /// <summary>
    /// Interface for objects that can be managed by an <see cref="ObjectPool{T}"/>.
    /// Implement this on MonoBehaviour-derived classes to receive pool lifecycle callbacks.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called when the object is retrieved from the pool and activated.
        /// Use this to reset state and prepare the object for reuse.
        /// </summary>
        void OnSpawnFromPool();

        /// <summary>
        /// Called when the object is returned to the pool and deactivated.
        /// Use this to clean up references and stop ongoing behaviors.
        /// </summary>
        void OnReturnToPool();
    }

    /// <summary>
    /// Generic object pool for <see cref="MonoBehaviour"/>-based objects that implement <see cref="IPoolable"/>.
    /// Pre-instantiates a configurable number of objects and reuses them to reduce allocation overhead.
    /// </summary>
    /// <typeparam name="T">
    /// The type of pooled object. Must derive from <see cref="MonoBehaviour"/> and implement <see cref="IPoolable"/>.
    /// </typeparam>
    public class ObjectPool<T> where T : MonoBehaviour, IPoolable
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _available;

        /// <summary>
        /// Gets the number of objects currently available in the pool.
        /// </summary>
        public int CountAvailable => _available.Count;

        /// <summary>
        /// Initializes a new object pool with the specified prefab and pre-warms it
        /// by instantiating <paramref name="initialSize"/> objects.
        /// </summary>
        /// <param name="prefab">The prefab to instantiate when the pool needs new objects.</param>
        /// <param name="initialSize">The number of objects to pre-instantiate during construction.</param>
        /// <param name="parent">The parent transform under which pooled objects are organized in the hierarchy.</param>
        public ObjectPool(T prefab, int initialSize, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
            _available = new Queue<T>(initialSize);

            for (int i = 0; i < initialSize; i++)
            {
                T instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _available.Enqueue(instance);
            }
        }

        /// <summary>
        /// Retrieves an object from the pool. If no objects are available,
        /// a new instance is created automatically.
        /// </summary>
        /// <returns>An activated and initialized poolable object.</returns>
        public T Get()
        {
            T instance = _available.Count > 0 ? _available.Dequeue() : CreateInstance();

            instance.gameObject.SetActive(true);
            instance.OnSpawnFromPool();
            return instance;
        }

        /// <summary>
        /// Returns an object to the pool for future reuse.
        /// The object is deactivated and its <see cref="IPoolable.OnReturnToPool"/> callback is invoked.
        /// </summary>
        /// <param name="obj">The object to return to the pool.</param>
        public void Return(T obj)
        {
            obj.OnReturnToPool();
            obj.gameObject.SetActive(false);
            _available.Enqueue(obj);
        }

        /// <summary>
        /// Creates a new instance of the pooled prefab under the designated parent transform.
        /// </summary>
        /// <returns>A newly instantiated object of type <typeparamref name="T"/>.</returns>
        private T CreateInstance()
        {
            T instance = Object.Instantiate(_prefab, _parent);
            return instance;
        }
    }
}
