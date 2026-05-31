using UnityEngine;

namespace FactoryDelivery.Data
{
    // =========================================================================
    //  판매 할당량 테이블 ScriptableObject
    // =========================================================================

    /// <summary>
    /// 일별 판매 할당량(쿼터) 테이블을 정의하는 ScriptableObject입니다.
    /// 지수 성장 공식 또는 AnimationCurve를 통해 일별 목표 판매량을 계산합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "QuotaTable", menuName = "FactoryDelivery/Data/Quota Table")]
    public class QuotaTableSO : ScriptableObject
    {
        // =========================================================================
        //  기본 설정
        // =========================================================================

        [Header("기본 설정")]
        /// <summary>1일차 기본 할당량입니다.</summary>
        [Tooltip("1일차 기본 할당량")]
        public int BaseQuota = 100;

        /// <summary>일별 지수 성장률입니다.</summary>
        [Tooltip("일별 지수 성장률")]
        public float GrowthRate = 1.5f;

        // =========================================================================
        //  난이도 커브 설정
        // =========================================================================

        [Header("난이도 커브 (선택 사항)")]
        /// <summary>커브 사용 시 공식 대신 적용할 난이도 커브입니다.</summary>
        [Tooltip("난이도 오버라이드 커브")]
        public AnimationCurve DifficultyOverride = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        /// <summary>true이면 지수 공식 대신 DifficultyOverride 커브를 사용합니다.</summary>
        [Tooltip("커브 사용 여부")]
        public bool UseCurve = false;

        // =========================================================================
        //  할당량 계산 메서드
        // =========================================================================

        /// <summary>
        /// 지정된 일차의 판매 할당량을 계산합니다.
        /// UseCurve가 true이면 DifficultyOverride 커브를 평가하고,
        /// false이면 지수 공식을 사용합니다.
        /// </summary>
        /// <param name="day">일차 (1부터 시작).</param>
        /// <returns>해당 일차의 판매 할당량 (정수).</returns>
        public int GetQuotaForDay(int day)
        {
            if (UseCurve)
            {
                return Mathf.RoundToInt(DifficultyOverride.Evaluate(day));
            }

            return Mathf.RoundToInt(BaseQuota * Mathf.Pow(GrowthRate, day - 1));
        }
    }
}
