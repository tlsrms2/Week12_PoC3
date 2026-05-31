using UnityEngine;
using FactoryDelivery.Data;

namespace FactoryDelivery.Facility
{
    /// <summary>
    /// <see cref="FacilityBase"/>를 확장하는 물류 창고.
    /// 대용량 입출력 버퍼를 보유하여 자원을 임시 저장하며,
    /// 입력에서 출력으로 소량의 지연을 두고 통과시킨다.
    /// 교통 혼잡 완화 역할 — 일꾼이 가득 찬 시설 대신 여기에 자원을 내려놓을 수 있다.
    /// </summary>
    public class Warehouse : FacilityBase
    {
        // ─────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────

        /// <summary>입력에서 출력으로의 이동 간격 (초).</summary>
        private const float TRANSFER_INTERVAL = 0.5f;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        /// <summary>이동 타이머.</summary>
        private float _transferTimer;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            // 창고는 대용량 버퍼
            InputCapacity = 10;
            OutputCapacity = 10;
        }

        // ─────────────────────────────────────────────
        //  Processing Logic
        // ─────────────────────────────────────────────

        /// <summary>
        /// 매 프레임 호출. 글로벌 인벤토리 저장 방식으로 개편되어 더 이상 패스스루 처리를 하지 않습니다.
        /// </summary>
        /// <param name="deltaTime">이전 프레임 이후 경과 시간 (초).</param>
        protected override void ProcessTick(float deltaTime)
        {
            // 글로벌 인벤토리 저장 방식으로 통합되어, 더 이상 자원을 출력 큐로 밀어 넣지 않습니다.
        }
    }
}
