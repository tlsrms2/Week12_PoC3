using UnityEngine;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 일꾼의 기본 데이터와 속성을 정의하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWorkerData", menuName = "FactoryDelivery/Data/Worker")]
    public class WorkerDataSO : ScriptableObject
    {
        // =========================================================================
        //  시각적 설정
        // =========================================================================

        [Header("시각적 설정")]
        [Tooltip("일꾼 모델의 크기 배율입니다.")]
        [Min(0.05f)] public float WorkerScale = 1.2f;

        [Tooltip("앞모습(아래쪽 이동) 스프라이트입니다.")]
        public Sprite SpriteFront;

        [Tooltip("뒷모습(위쪽 이동) 스프라이트입니다.")]
        public Sprite SpriteBack;

        [Tooltip("옆모습(좌우 이동) 스프라이트입니다.")]
        public Sprite SpriteSide;

        // =========================================================================
        //  이동 설정
        // =========================================================================

        [Header("이동 설정")]
        [Tooltip("일꾼의 기본 이동 속도입니다.")]
        [Min(0.1f)] public float BaseSpeed = 3f;

        [Tooltip("일꾼 간의 충돌 방지를 위한 최소 유지 거리입니다.")]
        [Min(0f)] public float MinDistance = 0.5f;

        // =========================================================================
        //  운반 설정
        // =========================================================================

        [Header("운반 설정")]
        [Tooltip("일꾼이 한 번에 운반할 수 있는 최대 자원 수입니다.")]
        [Min(1)] public int CarryCapacity = 1;

        [Tooltip("일꾼이 운반 중인 아이템의 크기 배율입니다.")]
        [Min(0.05f)] public float CarriedItemScale = 0.32f;

        [Tooltip("일꾼의 머리 위 아이템이 배치될 기본 오프셋 위치입니다.")]
        public Vector2 CarriedItemBaseOffset = new Vector2(0f, 0.34f);

        [Tooltip("여러 아이템을 쌓아 운반할 때 아이템 간의 수직 간격입니다.")]
        public float CarriedItemStackOffset = 0.18f;

        // =========================================================================
        //  계산된 속성 (Properties)
        // =========================================================================

        public float EffectiveBaseSpeed => BaseSpeed > 0f ? BaseSpeed : Constants.WorkerBaseSpeed;
        public float EffectiveMinDistance => MinDistance > 0f ? MinDistance : Constants.WorkerMinDistance;
        public int EffectiveCarryCapacity => Mathf.Max(1, CarryCapacity);
        public float EffectiveWorkerScale => Mathf.Max(0.05f, WorkerScale);
    }
}
