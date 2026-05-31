namespace FactoryDelivery.Utils
{
    /// <summary>
    /// 게임 전반에서 사용되는 모든 상수를 관리하는 중앙 저장소입니다.
    /// 관리와 검색이 용이하도록 시스템 도메인별로 정리되어 있습니다.
    /// </summary>
    public static class Constants
    {
        // =========================================================================
        //  그리드
        // =========================================================================

        /// <summary>게임 그리드의 셀 단위 너비입니다.</summary>
        public static int GridWidth = 20;

        /// <summary>게임 그리드의 셀 단위 높이입니다.</summary>
        public static int GridHeight = 20;

        /// <summary>One purchasable land parcel is an 8x8 grid.</summary>
        public static int LandPlotSize = 8;

        /// <summary>단일 그리드 셀의 월드 스페이스 크기입니다.</summary>
        public static float CellSize = 1f;

        // =========================================================================
        //  기본 설정
        // =========================================================================

        /// <summary>자원 생산 타일의 기본 쿨다운 시간(초)입니다.</summary>
        public static float DefaultResourceCooldown = 5f;

        /// <summary>타일이 도달할 수 있는 최대 업그레이드 레벨입니다.</summary>
        public static int MaxTileLevel = 5;

        // =========================================================================
        //  블록 / 하루 구조
        // =========================================================================

        /// <summary>게임 내 하루 동안 사용 가능한 배치 블록의 수입니다.</summary>
        public static int BlocksPerDay = 4;

        /// <summary>플레이어가 하루에 사용할 수 있는 최대 슬라이스 수입니다.</summary>
        public static int MaxSlicesPerDay = 1;

        // =========================================================================
        //  경제
        // =========================================================================

        /// <summary>상점 리롤의 기본 비용입니다.</summary>
        public static int RerollBaseCost = 10;

        /// <summary>연속 리롤 시마다 리롤 비용에 적용되는 배수입니다.</summary>
        public static float RerollCostMultiplier = 2f;

        /// <summary>Cost to unlock one adjacent land parcel.</summary>
        public static int LandPurchaseCost = 100;

        // =========================================================================
        //  물류 / 일꾼
        // =========================================================================

        /// <summary>일꾼 유닛의 기본 이동 속도 (초당 월드 단위)입니다.</summary>
        public static float WorkerBaseSpeed = 3f;

        /// <summary>일꾼들이 서로 겹치지 않도록 유지하는 최소 거리입니다.</summary>
        public static float WorkerMinDistance = 0.5f;

        /// <summary>일꾼 유닛의 최대 운반 용량입니다.</summary>
        public static int WorkerCapacity = 3;
    }
}
