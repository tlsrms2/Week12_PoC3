using System;
using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Grid
{
    /// <summary>
    /// 레벨 진행을 지원하는 엔티티를 위한 인터페이스입니다.
    /// </summary>
    public interface ILevelable
    {
        /// <summary>엔티티의 현재 레벨입니다.</summary>
        int Level { get; }

        /// <summary>엔티티가 도달할 수 있는 최대 레벨입니다.</summary>
        int MaxLevel { get; }

        /// <summary>엔티티가 아직 레벨업 가능한지 여부입니다.</summary>
        bool CanLevelUp { get; }

        /// <summary>가능한 경우 엔티티의 레벨을 한 단계 올립니다.</summary>
        void LevelUp();
    }

    /// <summary>
    /// 그리드에 배치된 단일 타일의 런타임 표현입니다.
    /// <see cref="TileDataSO"/>를 가변 상태(레벨, 위치)로 감싸며 타일 분류를 위한 편의 쿼리를 제공합니다.
    /// </summary>
    public class TileEntity : ILevelable
    {
        // =========================================================================
        //  필드
        // =========================================================================

        /// <summary>이 타일의 속성을 정의하는 정적 데이터 에셋입니다.</summary>
        public TileDataSO Data { get; }

        /// <summary>그리드 상의 타일 위치 (열, 행)입니다.</summary>
        public Vector2Int GridPosition { get; }

        private int _level;

        // =========================================================================
        //  이벤트
        // =========================================================================

        /// <summary>
        /// 타일의 레벨이 변경될 때마다 발생합니다.
        /// 페이로드는 새로운 레벨 값입니다.
        /// </summary>
        public event Action<int> OnLevelChanged;

        // =========================================================================
        //  생성자
        // =========================================================================

        /// <summary>
        /// 지정된 그리드 위치에 새로운 <see cref="TileEntity"/>를 생성합니다.
        /// </summary>
        /// <param name="data">이 타일에 대한 ScriptableObject 데이터 정의.</param>
        /// <param name="gridPosition">이 타일이 위치할 그리드 좌표.</param>
        /// <param name="initialLevel">시작 레벨 (기본값 1).</param>
        public TileEntity(TileDataSO data, Vector2Int gridPosition, int initialLevel = 1)
        {
            Data = data;
            GridPosition = gridPosition;
            _level = Mathf.Clamp(initialLevel, 1, data != null ? data.MaxLevel : 1);
        }

        // =========================================================================
        //  ILevelable 구현
        // =========================================================================

        /// <inheritdoc />
        public int Level => _level;

        /// <inheritdoc />
        public int MaxLevel => Data != null ? Data.MaxLevel : 1;

        /// <inheritdoc />
        public bool CanLevelUp => _level < MaxLevel;

        /// <inheritdoc />
        public void LevelUp()
        {
            if (!CanLevelUp) return;

            _level++;
            OnLevelChanged?.Invoke(_level);
        }

        // =========================================================================
        //  타입 쿼리
        // =========================================================================

        /// <summary>이 타일이 자원 생산 타일이면 true를 반환합니다.</summary>
        public bool IsResource => Data != null && Data.Type == TileType.Resource;

        /// <summary>이 타일이 시설 타일이면 true를 반환합니다.</summary>
        public bool IsFacility => Data != null && Data.Type == TileType.Facility;

        /// <summary>이 타일이 도로 타일이면 true를 반환합니다.</summary>
        public bool IsRoad => Data != null && Data.Type == TileType.Road;

        // =========================================================================
        //  비주얼 헬퍼
        // =========================================================================

        /// <summary>
        /// <see cref="TileDataSO.LevelSprites"/>에서 현재 레벨에 해당하는 스프라이트를 반환합니다.
        /// 레벨 스프라이트를 사용할 수 없는 경우 기본 <see cref="TileDataSO.Sprite"/>로 대체됩니다.
        /// </summary>
        /// <returns>현재 레벨의 스프라이트.</returns>
        public Sprite GetCurrentSprite()
        {
            if (Data == null) return null;
            if (Data.LevelSprites == null || Data.LevelSprites.Length == 0)
            {
                return Data.Sprite;
            }

            // LevelSprites 인덱스 0 = 레벨 1
            int index = Mathf.Clamp(_level - 1, 0, Data.LevelSprites.Length - 1);
            return Data.LevelSprites[index] ?? Data.Sprite;
        }

        /// <summary>
        /// 디버깅을 위해 사람이 읽을 수 있는 문자열을 반환합니다.
        /// </summary>
        public override string ToString()
        {
            string dataName = Data != null ? Data.DisplayName : "null";
            return $"[TileEntity] {dataName} Lv.{_level} @ {GridPosition}";
        }
    }
}
