using UnityEngine;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 일별 판매 할당량(쿼터) 테이블을 정의하는 ScriptableObject.
    /// 지수 성장 공식 또는 AnimationCurve를 통해 일별 목표 판매량을 계산한다.
    /// </summary>
    [CreateAssetMenu(fileName = "QuotaTable", menuName = "FactoryDelivery/Data/Quota Table")]
    public class QuotaTableSO : ScriptableObject
    {
        [Header("기본 설정")]

        /// <summary>1일차 기본 할당량.</summary>
        [Tooltip("1일차 기본 할당량")]
        public int BaseQuota = 100;

        /// <summary>일별 지수 성장률.</summary>
        [Tooltip("일별 지수 성장률")]
        public float GrowthRate = 1.5f;

        [Header("난이도 커브 (선택)")]

        /// <summary>커브 사용 시 공식 대신 적용할 난이도 커브.</summary>
        [Tooltip("난이도 오버라이드 커브")]
        public AnimationCurve DifficultyOverride = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        /// <summary>true이면 지수 공식 대신 <see cref="DifficultyOverride"/> 커브를 사용한다.</summary>
        [Tooltip("커브 사용 여부")]
        public bool UseCurve = false;

        /// <summary>
        /// 지정된 일차의 판매 할당량을 계산한다.
        /// <see cref="UseCurve"/>가 true이면 <see cref="DifficultyOverride"/> 커브를 평가하고,
        /// false이면 <c>BaseQuota * GrowthRate^(day-1)</c> 지수 공식을 사용한다.
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
