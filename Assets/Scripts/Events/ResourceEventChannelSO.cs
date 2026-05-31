using UnityEngine;
using UnityEngine.Events;

namespace FactoryDelivery.Events
{
    // =========================================================================
    //  자원 이벤트 페이로드 구조체
    // =========================================================================

    /// <summary>
    /// 자원 관련 이벤트에 사용되는 데이터 구조체입니다.
    /// 자원 유형, 수량, 그리드 위치 정보를 포함합니다.
    /// </summary>
    [System.Serializable]
    public struct ResourcePayload
    {
        /// <summary>
        /// 자원 데이터 ScriptableObject에 대한 참조입니다.
        /// </summary>
        public ScriptableObject ResourceData;

        /// <summary>
        /// 해당 이벤트와 관련된 자원의 수량입니다.
        /// </summary>
        public int Amount;

        /// <summary>
        /// 자원 이벤트가 발생한 그리드 좌표입니다.
        /// </summary>
        public Vector2Int GridPosition;
    }

    // =========================================================================
    //  자원 이벤트 채널 ScriptableObject
    // =========================================================================

    /// <summary>
    /// ResourcePayload 데이터를 전달하는 ScriptableObject 기반 이벤트 채널입니다.
    /// 그리드 상에서의 자원 생산, 소비, 이동 등의 이벤트를 방송하는 데 사용됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceEventChannel", menuName = "FactoryDelivery/Events/Resource Event Channel")]
    public class ResourceEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// 이벤트가 발생했을 때 호출되는 UnityAction입니다. 구독자는 자원 페이로드를 받습니다.
        /// </summary>
        public event UnityAction<ResourcePayload> OnEventRaised;

        /// <summary>
        /// 등록된 모든 리스너에게 지정된 자원 페이로드와 함께 이벤트를 방송합니다.
        /// </summary>
        /// <param name="payload">자원 데이터, 수량, 위치 정보를 포함하는 페이로드입니다.</param>
        public void RaiseEvent(ResourcePayload payload)
        {
#if UNITY_EDITOR
            Debug.Log($"[ResourceEventChannel] '{name}' 이벤트 발생 — 자원: {(payload.ResourceData != null ? payload.ResourceData.name : "null")}, 수량: {payload.Amount}, 위치: {payload.GridPosition}.");
#endif
            OnEventRaised?.Invoke(payload);
        }
    }
}
