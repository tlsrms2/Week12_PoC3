using UnityEngine;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Data
{
    [CreateAssetMenu(fileName = "NewWorkerData", menuName = "FactoryDelivery/Data/Worker")]
    public class WorkerDataSO : ScriptableObject
    {
        [Header("Visual")]
        [Min(0.05f)] public float WorkerScale = 1.2f;

        [Header("Movement")]
        [Min(0.1f)] public float BaseSpeed = 3f;
        [Min(0f)] public float MinDistance = 0.5f;

        [Header("Carrying")]
        [Min(1)] public int CarryCapacity = 1;
        [Min(0.05f)] public float CarriedItemScale = 0.32f;
        public Vector2 CarriedItemBaseOffset = new Vector2(0f, 0.34f);
        public float CarriedItemStackOffset = 0.18f;

        public float EffectiveBaseSpeed => BaseSpeed > 0f ? BaseSpeed : Constants.WorkerBaseSpeed;
        public float EffectiveMinDistance => MinDistance > 0f ? MinDistance : Constants.WorkerMinDistance;
        public int EffectiveCarryCapacity => Mathf.Max(1, CarryCapacity);
        public float EffectiveWorkerScale => Mathf.Max(0.05f, WorkerScale);
    }
}
