using UnityEngine;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 타일의 유형을 정의하는 열거형.
    /// </summary>
    public enum TileType
    {
        /// <summary>빈 타일.</summary>
        Empty,

        /// <summary>자원 타일 — 원자재를 생산한다.</summary>
        Resource,

        /// <summary>경영 타일 — 가공 시설, 숙소, 창고, 판매소를 배치한다.</summary>
        Facility,

        /// <summary>도로 — 일꾼의 이동 경로.</summary>
        Road
    }

    /// <summary>
    /// 개별 타일의 데이터를 정의하는 ScriptableObject.
    /// 타일 유형에 따라 자원 생산, 시설 배치, 도로 역할을 수행한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTile", menuName = "FactoryDelivery/Data/Tile")]
    public class TileDataSO : ScriptableObject
    {
        [Header("기본 정보")]

        /// <summary>타일의 표시 이름.</summary>
        [Tooltip("타일의 표시 이름")]
        public string DisplayName;

        /// <summary>타일의 유형.</summary>
        [Tooltip("타일 유형")]
        public TileType Type;

        /// <summary>타일의 기본 스프라이트.</summary>
        [Tooltip("타일 스프라이트")]
        public Sprite Sprite;

        /// <summary>
        /// 도로가 꺾이는 구간에 사용할 스프라이트.
        /// 기본 방향은 아래에서 들어와 오른쪽으로 나가는 커브이다.
        /// </summary>
        [Tooltip("도로 커브 스프라이트 (기본 방향: 아래에서 진입 후 오른쪽으로 진행)")]
        public Sprite RoadCurveSprite;

        [Header("레벨 설정")]

        /// <summary>타일의 최대 업그레이드 레벨.</summary>
        [Tooltip("최대 레벨")]
        public int MaxLevel = 5;

        /// <summary>
        /// 레벨별 타일 스프라이트 배열.
        /// 인덱스 0이 레벨 1에 대응한다.
        /// </summary>
        [Tooltip("레벨별 타일 스프라이트 (인덱스 0 = 레벨 1)")]
        public Sprite[] LevelSprites;

        /// <summary>
        /// 레벨업 시 표시할 설명 텍스트 배열.
        /// 인덱스 0이 레벨 2 업그레이드 설명에 대응한다.
        /// </summary>
        [Tooltip("레벨업 설명 (인덱스 0 = 레벨 2 업그레이드 설명)")]
        public string[] LevelUpDescriptions;

        [Header("연결 데이터")]

        /// <summary>이 타일에서 생산하는 자원 데이터 (Resource 타일 전용).</summary>
        [Tooltip("연결된 자원 (Resource 타일 전용)")]
        public ResourceDataSO AssociatedResource;

        /// <summary>이 타일에 배치되는 시설 데이터 (Facility 타일 전용).</summary>
        [Tooltip("연결된 시설 (Facility 타일 전용)")]
        public FacilityDataSO AssociatedFacility;
    }
}
