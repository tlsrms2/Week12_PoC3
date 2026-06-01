using UnityEngine;
using UnityEngine.Events;

namespace FactoryDelivery.Events
{
    // =========================================================================
    //  Void 이벤트 채널 ScriptableObject
    // =========================================================================

    /// <summary>
    /// 전달할 데이터가 없는 단순 신호용 ScriptableObject 기반 이벤트 채널입니다.
    /// 페이즈 변경, UI 트리거, 시스템 알림 등 단순 알림 이벤트에 사용됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "VoidEventChannel", menuName = "FactoryDelivery/Events/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// 이벤트가 발생했을 때 호출되는 UnityAction입니다. 알림을 받으려면 이 이벤트에 구독하세요.
        /// </summary>
        public event UnityAction OnEventRaised;

        /// <summary>
        /// 등록된 모든 리스너에게 이벤트를 방송합니다.
        /// </summary>
        public void RaiseEvent()
        {
#if UNITY_EDITOR
            Debug.Log($"[VoidEventChannel] '{name}' 이벤트 발생.");
#endif
            OnEventRaised?.Invoke();
        }
    }
}
