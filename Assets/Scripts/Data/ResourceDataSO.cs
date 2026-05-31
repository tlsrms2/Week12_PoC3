using UnityEngine;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 자원의 분류 카테고리.
    /// 카테고리에 따라 가공 속도 및 물류 특성이 달라진다.
    /// </summary>
    public enum ResourceCategory
    {
        /// <summary>농산 및 식량류 — 빠른 가공 속도.</summary>
        Agricultural,

        /// <summary>직물 및 의복류 — 중간 가공 속도.</summary>
        Textile,

        /// <summary>광물 및 자재류 — 느린 가공 속도, 교통 혼잡 유발.</summary>
        Mineral,

        /// <summary>특산품류.</summary>
        Specialty
    }

    /// <summary>
    /// 개별 자원의 기본 데이터를 정의하는 ScriptableObject.
    /// 원자재(벼, 콩 등)부터 가공품(쌀 가마니, 두부 등)까지 모든 자원에 사용된다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "FactoryDelivery/Data/Resource")]
    public class ResourceDataSO : ScriptableObject
    {
        [Header("기본 정보")]

        /// <summary>자원의 표시 이름 (예: "벼", "쌀 가마니").</summary>
        [Tooltip("자원의 표시 이름")]
        public string DisplayName;

        /// <summary>자원에 대한 설명 텍스트.</summary>
        [Tooltip("자원에 대한 설명")]
        public string Description;

        /// <summary>자원이 속하는 카테고리.</summary>
        [Tooltip("자원 카테고리")]
        public ResourceCategory Category;

        /// <summary>자원의 아이콘 스프라이트.</summary>
        [Tooltip("자원 아이콘")]
        public Sprite Icon;

        [Header("물류 속성")]

        /// <summary>
        /// 일꾼 이동 속도에 적용되는 무게 배율.
        /// 값이 클수록 일꾼이 해당 자원을 운반할 때 느려진다.
        /// </summary>
        [Range(0.5f, 2f)]
        [Tooltip("일꾼 이동 속도에 적용되는 무게 배율 (높을수록 느림)")]
        public float WeightMultiplier = 1f;

        [Header("경제 속성")]

        /// <summary>판매소에서의 기본 판매 가격.</summary>
        [Tooltip("판매소에서의 기본 판매 가격")]
        public int BaseValue;

        [Header("생산 속성 (원자재만 해당)")]

        /// <summary>원자재 여부. true이면 자원 타일에서 자연 생산된다.</summary>
        [Tooltip("원자재 여부 (벼, 콩 등 기본 자원)")]
        public bool IsRawResource;

        /// <summary>원자재의 생산 쿨다운(초 단위).</summary>
        [Tooltip("생산 쿨다운 (초)")]
        public float BaseCooldown = 5f;
    }
}
