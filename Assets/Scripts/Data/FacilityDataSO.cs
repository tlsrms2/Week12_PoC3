using System.Collections.Generic;
using UnityEngine;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 시설의 유형을 정의하는 열거형.
    /// </summary>
    public enum FacilityType
    {
        /// <summary>일꾼 숙소 — 일꾼을 배치하여 자원을 운반하게 한다.</summary>
        WorkerHouse,

        /// <summary>가공 시설 — 레시피에 따라 자원을 가공한다.</summary>
        ProcessingFacility,

        /// <summary>물류 창고 — 자원을 임시 저장한다.</summary>
        Warehouse
    }

    /// <summary>
    /// 시설의 기본 데이터를 정의하는 ScriptableObject.
    /// 시설 유형에 따라 가공, 일꾼 관리, 저장, 판매 기능을 수행한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFacility", menuName = "FactoryDelivery/Data/Facility")]
    public class FacilityDataSO : ScriptableObject
    {
        [Header("기본 정보")]

        /// <summary>시설의 표시 이름.</summary>
        [Tooltip("시설의 표시 이름")]
        public string DisplayName;

        /// <summary>시설의 유형.</summary>
        [Tooltip("시설 유형")]
        public FacilityType Type;

        /// <summary>시설의 아이콘 스프라이트.</summary>
        [Tooltip("시설 아이콘")]
        public Sprite Icon;

        [Header("가공 설정 (ProcessingFacility 전용)")]

        /// <summary>이 시설에서 수행 가능한 레시피 목록.</summary>
        [Tooltip("지원하는 레시피 목록")]
        public List<RecipeDataSO> SupportedRecipes = new List<RecipeDataSO>();

        /// <summary>기본 가공 속도 배율.</summary>
        [Tooltip("기본 가공 속도 배율")]
        public float BaseProcessingSpeed = 1f;

        /// <summary>레벨당 추가되는 가공 속도 보너스.</summary>
        [Tooltip("레벨당 가공 속도 보너스")]
        public float LevelSpeedBonus = 0.2f;

        [Header("일꾼 설정 (WorkerHouse 전용)")]

        /// <summary>기본 일꾼 수.</summary>
        [Tooltip("기본 일꾼 수")]
        [Min(0.1f)] public float WorkerSpawnInterval = 4f;

        /// <summary>기본 활동 반경 (AoE).</summary>
        [Tooltip("기본 활동 반경")]

        /// <summary>
        /// 지정 레벨에서의 가공 속도를 계산한다.
        /// </summary>
        /// <param name="level">현재 시설 레벨 (1부터 시작).</param>
        /// <returns>해당 레벨에서의 가공 속도 배율.</returns>
        public float GetProcessingSpeed(int level)
        {
            return BaseProcessingSpeed * (1f + LevelSpeedBonus * (level - 1));
        }

        /// <summary>
        /// 지정 레벨에서의 일꾼 숙소 활동 반경을 계산한다.
        /// 레벨이 오를수록 활동 반경이 넓어진다.
        /// </summary>
        /// <param name="level">현재 시설 레벨 (1부터 시작).</param>
        /// <returns>해당 레벨에서의 활동 반경.</returns>
        public float GetWorkerSpawnInterval()
        {
            return Mathf.Max(0.1f, WorkerSpawnInterval);
        }

        /// <summary>
        /// 지정 레벨에서의 일꾼 수를 계산한다.
        /// 레벨이 오를수록 더 많은 일꾼을 배치할 수 있다.
        /// </summary>
        /// <param name="level">현재 시설 레벨 (1부터 시작).</param>
        /// <returns>해당 레벨에서의 일꾼 수.</returns>
    }
}
