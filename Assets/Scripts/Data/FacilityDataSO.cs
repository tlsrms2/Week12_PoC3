using System.Collections.Generic;
using UnityEngine;

namespace FactoryDelivery.Data
{
    // =========================================================================
    //  열거형 정의
    // =========================================================================

    /// <summary>
    /// 시설의 유형을 정의하는 열거형입니다.
    /// </summary>
    public enum FacilityType
    {
        /// <summary>일꾼 숙소 — 일꾼을 배치하여 자원을 운반하게 합니다.</summary>
        WorkerHouse,

        /// <summary>가공 시설 — 레시피에 따라 자원을 가공합니다.</summary>
        ProcessingFacility,

        /// <summary>물류 창고 — 자원을 임시 저장합니다.</summary>
        Warehouse
    }

    // =========================================================================
    //  시설 데이터 ScriptableObject
    // =========================================================================

    /// <summary>
    /// 시설의 기본 데이터를 정의하는 ScriptableObject입니다.
    /// 시설 유형에 따라 가공, 일꾼 관리, 저장 기능을 수행합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFacility", menuName = "FactoryDelivery/Data/Facility")]
    public class FacilityDataSO : ScriptableObject
    {
        // =========================================================================
        //  기본 정보
        // =========================================================================

        [Header("기본 정보")]
        /// <summary>시설의 표시 이름입니다.</summary>
        [Tooltip("시설의 표시 이름")]
        public string DisplayName;

        /// <summary>시설의 유형입니다.</summary>
        [Tooltip("시설 유형")]
        public FacilityType Type;

        /// <summary>시설의 아이콘 스프라이트입니다.</summary>
        [Tooltip("시설 아이콘")]
        public Sprite Icon;

        // =========================================================================
        //  가공 설정 (ProcessingFacility 전용)
        // =========================================================================

        [Header("가공 설정 (가공 시설 전용)")]
        /// <summary>이 시설에서 수행 가능한 레시피 목록입니다.</summary>
        [Tooltip("지원하는 레시피 목록")]
        public List<RecipeDataSO> SupportedRecipes = new List<RecipeDataSO>();

        /// <summary>기본 가공 속도 배율입니다.</summary>
        [Tooltip("기본 가공 속도 배율")]
        public float BaseProcessingSpeed = 1f;

        /// <summary>레벨당 추가되는 가공 속도 보너스입니다.</summary>
        [Tooltip("레벨당 가공 속도 보너스")]
        public float LevelSpeedBonus = 0.2f;

        // =========================================================================
        //  일꾼 설정 (WorkerHouse 전용)
        // =========================================================================

        [Header("일꾼 설정 (일꾼 숙소 전용)")]
        /// <summary>일꾼 생성 간격 (초)입니다.</summary>
        [Tooltip("일꾼 생성 간격 (초)")]
        [Min(0.1f)] public float WorkerSpawnInterval = 4f;

        // =========================================================================
        //  계산 메서드
        // =========================================================================

        /// <summary>
        /// 지정 레벨에서의 가공 속도를 계산합니다.
        /// </summary>
        /// <param name="level">현재 시설 레벨 (1부터 시작).</param>
        /// <returns>해당 레벨에서의 가공 속도 배율.</returns>
        public float GetProcessingSpeed(int level)
        {
            return BaseProcessingSpeed * (1f + LevelSpeedBonus * (level - 1));
        }

        /// <summary>
        /// 현재 설정된 일꾼 생성 간격을 반환합니다.
        /// </summary>
        /// <returns>일꾼 생성 간격 (초).</returns>
        public float GetWorkerSpawnInterval()
        {
            return Mathf.Max(0.1f, WorkerSpawnInterval);
        }
    }
}
