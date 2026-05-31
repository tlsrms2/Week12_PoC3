using UnityEngine;
using UnityEngine.Events;

namespace FactoryDelivery.Events
{
    /// <summary>
    /// ScriptableObject-based event channel with an <see cref="int"/> payload.
    /// Used for broadcasting events that carry a single integer value,
    /// such as score changes, level indices, or quantity updates.
    /// </summary>
    [CreateAssetMenu(fileName = "IntEventChannel", menuName = "FactoryDelivery/Events/Int Event Channel")]
    public class IntEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// Raised when the event is broadcast. Listeners receive the integer payload.
        /// </summary>
        public event UnityAction<int> OnEventRaised;

        /// <summary>
        /// Broadcasts the event with the specified integer value to all registered listeners.
        /// </summary>
        /// <param name="value">The integer payload to send with the event.</param>
        public void RaiseEvent(int value)
        {
#if UNITY_EDITOR
            Debug.Log($"[IntEventChannel] '{name}' raised with value: {value}.");
#endif
            OnEventRaised?.Invoke(value);
        }
    }
}
