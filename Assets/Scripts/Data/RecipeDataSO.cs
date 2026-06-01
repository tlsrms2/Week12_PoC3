using UnityEngine;

namespace FactoryDelivery.Data
{
    // =========================================================================
    //  가공 레시피 데이터 ScriptableObject
    // =========================================================================

    /// <summary>
    /// 1:1 선형 가공 레시피를 정의하는 ScriptableObject입니다.
    /// 하나의 입력 자원을 하나의 출력 자원으로 변환하는 규칙을 나타냅니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "FactoryDelivery/Data/Recipe")]
    public class RecipeDataSO : ScriptableObject
    {
        // =========================================================================
        //  레시피 구성
        // =========================================================================

        [Header("레시피 구성")]
        /// <summary>가공에 필요한 입력 자원입니다.</summary>
        [Tooltip("가공에 필요한 입력 자원")]
        public ResourceDataSO InputResource;

        /// <summary>가공 결과로 생산되는 출력 자원입니다.</summary>
        [Tooltip("가공 결과로 생산되는 출력 자원")]
        public ResourceDataSO OutputResource;

        // =========================================================================
        //  가공 속성
        // =========================================================================

        [Header("가공 속성")]
        /// <summary>기본 가공 시간(초 단위)입니다.</summary>
        [Tooltip("기본 가공 시간 (초)")]
        public float BaseProcessingTime;

        /// <summary>
        /// 가공 단계 (1 = 1차 가공, 2 = 2차 가공)입니다.
        /// 고차 가공일수록 더 높은 가치를 지닌 자원을 생산합니다.
        /// </summary>
        [Tooltip("가공 단계 (1 = 1차, 2 = 2차)")]
        public int ProcessingTier = 1;

        // =========================================================================
        //  유효성 검사
        // =========================================================================

        /// <summary>
        /// 레시피가 유효한지 확인합니다.
        /// 입력 자원과 출력 자원이 모두 할당되어 있으면 유효합니다.
        /// </summary>
        public bool IsValid => InputResource != null && OutputResource != null;
    }
}
