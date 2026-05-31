using UnityEngine;
using FactoryDelivery.Utils;

namespace FactoryDelivery.Data
{
    /// <summary>
    /// 게임의 전반적인 환경 설정을 정의하는 ScriptableObject입니다.
    /// 그리드 크기, 경제 시스템, 일꾼 속성 등 게임 내 주요 상수를 제어합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Factory Delivery/Game Settings", order = 0)]
    public class GameSettingsSO : ScriptableObject
    {
        // =========================================================================
        //  그리드 설정
        // =========================================================================

        [Header("그리드 설정")]
        [Tooltip("게임 그리드의 가로 셀 개수입니다.")]
        [SerializeField] private int gridWidth = 20;

        [Tooltip("게임 그리드의 세로 셀 개수입니다.")]
        [SerializeField] private int gridHeight = 20;

        [Tooltip("그리드 셀 한 칸의 월드 좌표 크기입니다.")]
        [SerializeField] private float cellSize = 2f;

        // =========================================================================
        //  기본 설정
        // =========================================================================

        [Header("기본 설정")]
        [Tooltip("자원 생산 타일의 기본 쿨다운 시간(초)입니다.")]
        [SerializeField] private float defaultResourceCooldown = 5f;

        [Tooltip("타일이 도달할 수 있는 최대 업그레이드 레벨입니다.")]
        [SerializeField] private int maxTileLevel = 5;

        // =========================================================================
        //  블록 / 하루 구조
        // =========================================================================

        [Header("블록 / 하루 구조")]
        [Tooltip("게임 내 하루 동안 사용 가능한 배치 블록 수입니다.")]
        [SerializeField] private int blocksPerDay = 4;

        [Tooltip("플레이어가 하루에 사용할 수 있는 최대 슬라이스 수입니다.")]
        [SerializeField] private int maxSlicesPerDay = 1;

        // =========================================================================
        //  경제 설정
        // =========================================================================

        [Header("경제 설정")]
        [Tooltip("상점 리롤의 기본 비용입니다.")]
        [SerializeField] private int rerollBaseCost = 10;

        [Tooltip("리롤을 할 때마다 비용에 곱해지는 배율입니다.")]
        [SerializeField] private float rerollCostMultiplier = 2f;

        // =========================================================================
        //  물류 / 일꾼 설정
        // =========================================================================

        [Header("물류 / 일꾼 설정")]
        [Tooltip("일꾼 유닛의 기본 이동 속도 (초당 월드 단위)입니다.")]
        [SerializeField] private float workerBaseSpeed = 3f;

        [Tooltip("일꾼 간의 겹침을 방지하기 위해 유지하는 최소 거리입니다.")]
        [SerializeField] private float workerMinDistance = 0.5f;

        [Tooltip("일꾼 유닛의 최대 운반 용량입니다.")]
        [SerializeField] private int workerCapacity = 3;

        // =========================================================================
        //  상수 적용 메서드
        // =========================================================================

        /// <summary>
        /// 에디터 설정을 전역 정적 클래스인 Constants에 직접 적용합니다.
        /// </summary>
        public void ApplyToConstants()
        {
            Constants.GridWidth = gridWidth;
            Constants.GridHeight = gridHeight;
            Constants.CellSize = cellSize;

            Constants.DefaultResourceCooldown = defaultResourceCooldown;
            Constants.MaxTileLevel = maxTileLevel;

            Constants.BlocksPerDay = blocksPerDay;
            Constants.MaxSlicesPerDay = maxSlicesPerDay;

            Constants.RerollBaseCost = rerollBaseCost;
            Constants.RerollCostMultiplier = rerollCostMultiplier;

            Constants.WorkerBaseSpeed = workerBaseSpeed;
            Constants.WorkerMinDistance = workerMinDistance;
            Constants.WorkerCapacity = workerCapacity;

            Debug.Log("[GameSettingsSO] 에디터 설정을 Constants에 성공적으로 적용했습니다.");
        }

        /// <summary>
        /// 씬이 로드되기 전(BeforeSceneLoad)에 Resources 폴더의 'GameSettings' 에셋을 자동 로드하여
        /// 상수를 주입하는 초기화 메서드입니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoadAndApply()
        {
            GameSettingsSO settings = Resources.Load<GameSettingsSO>("GameSettings");
            if (settings != null)
            {
                settings.ApplyToConstants();
                Debug.Log("[GameSettingsSO] [자동 로드] 'Resources/GameSettings.asset'을 로드하여 설정을 적용했습니다.");
            }
            else
            {
                Debug.LogWarning("[GameSettingsSO] [자동 로드] 'Resources/GameSettings.asset'을 찾을 수 없습니다. GameManager의 직렬화된 참조가 대신 사용됩니다.");
            }
        }
    }
}
