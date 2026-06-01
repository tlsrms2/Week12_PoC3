using System.Collections.Generic;
using UnityEngine;

namespace FactoryDelivery.Utils
{
    /// <summary>
    /// <see cref="ObjectPool{T}"/>에 의해 관리될 수 있는 객체를 위한 인터페이스입니다.
    /// 풀 생명주기 콜백을 받으려면 MonoBehaviour 파생 클래스에서 이를 구현하십시오.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 객체가 풀에서 회수되어 활성화될 때 호출됩니다.
        /// 상태를 초기화하고 재사용을 위해 객체를 준비하는 데 사용하십시오.
        /// </summary>
        void OnSpawnFromPool();

        /// <summary>
        /// 객체가 풀로 반환되어 비활성화될 때 호출됩니다.
        /// 참조를 정리하고 진행 중인 동작을 중지하는 데 사용하십시오.
        /// </summary>
        void OnReturnToPool();
    }

    /// <summary>
    /// <see cref="IPoolable"/>을 구현하는 <see cref="MonoBehaviour"/> 기반 객체를 위한 범용 오브젝트 풀입니다.
    /// 설정 가능한 수의 객체를 미리 생성하고 재사용하여 할당 오버헤드를 줄입니다.
    /// </summary>
    /// <typeparam name="T">
    /// 풀링된 객체의 타입입니다. <see cref="MonoBehaviour"/>에서 파생되어야 하며 <see cref="IPoolable"/>을 구현해야 합니다.
    /// </typeparam>
    public class ObjectPool<T> where T : MonoBehaviour, IPoolable
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _available;

        /// <summary>
        /// 현재 풀에서 사용 가능한 객체의 수를 가져옵니다.
        /// </summary>
        public int CountAvailable => _available.Count;

        /// <summary>
        /// 지정된 프리팹으로 새 오브젝트 풀을 초기화하고 <paramref name="initialSize"/>만큼 객체를 생성하여 준비합니다.
        /// </summary>
        /// <param name="prefab">풀에 새 객체가 필요할 때 생성할 프리팹.</param>
        /// <param name="initialSize">생성 시 미리 인스턴스화할 객체의 수.</param>
        /// <param name="parent">계층 구조에서 풀링된 객체들이 정리될 부모 트랜스폼.</param>
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
        /// 풀에서 객체를 가져옵니다. 사용 가능한 객체가 없으면 새 인스턴스가 자동으로 생성됩니다.
        /// </summary>
        /// <returns>활성화되고 초기화된 풀링 가능 객체.</returns>
        public T Get()
        {
            T instance = _available.Count > 0 ? _available.Dequeue() : CreateInstance();

            instance.gameObject.SetActive(true);
            instance.OnSpawnFromPool();
            return instance;
        }

        /// <summary>
        /// 미래의 재사용을 위해 객체를 풀로 반환합니다.
        /// 객체는 비활성화되며 <see cref="IPoolable.OnReturnToPool"/> 콜백이 호출됩니다.
        /// </summary>
        /// <param name="obj">풀로 반환할 객체.</param>
        public void Return(T obj)
        {
            obj.OnReturnToPool();
            obj.gameObject.SetActive(false);
            _available.Enqueue(obj);
        }

        /// <summary>
        /// 지정된 부모 트랜스폼 아래에 풀링된 프리팹의 새 인스턴스를 생성합니다.
        /// </summary>
        /// <returns><typeparamref name="T"/> 타입의 새로 인스턴스화된 객체.</returns>
        private T CreateInstance()
        {
            T instance = Object.Instantiate(_prefab, _parent);
            return instance;
        }
    }
}
