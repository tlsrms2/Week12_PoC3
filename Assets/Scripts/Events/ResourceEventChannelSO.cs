using UnityEngine;
using UnityEngine.Events;

namespace FactoryDelivery.Events
{
    /// <summary>
    /// Payload struct for resource-related events.
    /// Contains information about the resource type, amount, and grid position.
    /// </summary>
    [System.Serializable]
    public struct ResourcePayload
    {
        /// <summary>
        /// Reference to the resource data ScriptableObject.
        /// Consumers should cast this to <c>ResourceDataSO</c> for type-safe access.
        /// </summary>
        public ScriptableObject ResourceData;

        /// <summary>
        /// The quantity of the resource involved in this event.
        /// </summary>
        public int Amount;

        /// <summary>
        /// The grid coordinates where the resource event occurred.
        /// </summary>
        public Vector2Int GridPosition;
    }

    /// <summary>
    /// ScriptableObject-based event channel with a <see cref="ResourcePayload"/> payload.
    /// Used for broadcasting resource-related events such as production, consumption,
    /// or transfer of resources on the grid.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceEventChannel", menuName = "FactoryDelivery/Events/Resource Event Channel")]
    public class ResourceEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// Raised when the event is broadcast. Listeners receive the resource payload.
        /// </summary>
        public event UnityAction<ResourcePayload> OnEventRaised;

        /// <summary>
        /// Broadcasts the event with the specified resource payload to all registered listeners.
        /// </summary>
        /// <param name="payload">The resource payload containing resource data, amount, and grid position.</param>
        public void RaiseEvent(ResourcePayload payload)
        {
#if UNITY_EDITOR
            Debug.Log($"[ResourceEventChannel] '{name}' raised — Resource: {(payload.ResourceData != null ? payload.ResourceData.name : "null")}, Amount: {payload.Amount}, Position: {payload.GridPosition}.");
#endif
            OnEventRaised?.Invoke(payload);
        }
    }
}
