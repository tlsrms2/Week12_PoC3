using UnityEngine;
using UnityEngine.Events;

namespace FactoryDelivery.Events
{
    // =========================================================================
    //  Int 이벤트 채널 ScriptableObject
    // =========================================================================

    /// <summary>
    /// int 타입의 데이터를 전달하는 ScriptableObject 기반 이벤트 채널입니다.
    /// 점수 변경, 레벨 인덱스, 수량 업데이트와 같이 단일 정수 값을 전달하는 이벤트에 사용됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "IntEventChannel", menuName = "FactoryDelivery/Events/Int Event Channel")]
    public class IntEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// 이벤트가 발생했을 때 호출되는 UnityAction입니다. 구독자는 정수 데이터를 받습니다.
        /// </summary>
        public event UnityAction<int> OnEventRaised;

        /// <summary>
        /// 등록된 모든 리스너에게 지정된 정수 값과 함께 이벤트를 방송합니다.
        /// </summary>
        /// <param name="value">이벤트와 함께 전달할 정수 데이터입니다.</param>
        public void RaiseEvent(int value)
        {
#if UNITY_EDITOR
            Debug.Log($"[IntEventChannel] '{name}' 이벤트 발생 — 값: {value}.");
#endif
            OnEventRaised?.Invoke(value);
        }
    }
}
