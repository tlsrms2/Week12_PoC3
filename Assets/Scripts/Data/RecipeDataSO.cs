using UnityEngine;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 1:1 선형 가공 레시피를 정의하는 ScriptableObject.
    /// 하나의 입력 자원을 하나의 출력 자원으로 변환하는 규칙을 나타낸다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "FactoryDelivery/Data/Recipe")]
    public class RecipeDataSO : ScriptableObject
    {
        [Header("레시피 구성")]

        /// <summary>가공에 필요한 입력 자원.</summary>
        [Tooltip("가공에 필요한 입력 자원")]
        public ResourceDataSO InputResource;

        /// <summary>가공 결과로 생산되는 출력 자원.</summary>
        [Tooltip("가공 결과로 생산되는 출력 자원")]
        public ResourceDataSO OutputResource;

        [Header("가공 속성")]

        /// <summary>기본 가공 시간(초 단위).</summary>
        [Tooltip("기본 가공 시간 (초)")]
        public float BaseProcessingTime;

        /// <summary>
        /// 가공 단계 (1 = 1차 가공, 2 = 2차 가공).
        /// 고차 가공일수록 더 높은 가치를 지닌 자원을 생산한다.
        /// </summary>
        [Tooltip("가공 단계 (1 = 1차, 2 = 2차)")]
        public int ProcessingTier = 1;

        /// <summary>
        /// 레시피가 유효한지 확인한다.
        /// 입력 자원과 출력 자원이 모두 할당되어 있으면 유효하다.
        /// </summary>
        public bool IsValid => InputResource != null && OutputResource != null;
    }
}
