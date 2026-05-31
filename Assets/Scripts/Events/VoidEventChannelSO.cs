using UnityEngine;
using UnityEngine.Events;

namespace FactoryDelivery.Events
{
    /// <summary>
    /// ScriptableObject-based event channel with no payload.
    /// Used for broadcasting simple events such as phase changes, UI triggers, or system notifications.
    /// </summary>
    [CreateAssetMenu(fileName = "VoidEventChannel", menuName = "FactoryDelivery/Events/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// Raised when the event is broadcast. Subscribe to this event to receive notifications.
        /// </summary>
        public event UnityAction OnEventRaised;

        /// <summary>
        /// Broadcasts the event to all registered listeners.
        /// </summary>
        public void RaiseEvent()
        {
#if UNITY_EDITOR
            Debug.Log($"[VoidEventChannel] '{name}' raised.");
#endif
            OnEventRaised?.Invoke();
        }
    }
}
