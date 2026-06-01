using UnityEngine;
using FactoryDelivery.Core;
using FactoryDelivery.Data;
using FactoryDelivery.Logistics;
using FactoryDelivery.Resource;

namespace FactoryDelivery.Facility
{
    /// <summary>
    /// 아이템을 가공하여 다른 자원으로 변환하는 시설 클래스.
    /// </summary>
    public class ProcessingFacility : FacilityBase
    {
        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("시각 효과")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        /// <summary>현재 시설을 점유 중인 일꾼.</summary>
        private Worker _activeWorker;
        /// <summary>시설 진입을 허가받고 이동 중인 일꾼.</summary>
        private Worker _incomingWorker;
        /// <summary>가공 남은 시간 타이머.</summary>
        private float _processingTimer;
        /// <summary>현재 가공 작업의 총 소요 시간.</summary>
        private float _totalProcessingTime;
        /// <summary>현재 가공 중인 입력 자원.</summary>
        private ResourceDataSO _currentlyProcessing;
        /// <summary>현재 실행 중인 가공 레시피.</summary>
        private RecipeDataSO _currentRecipe;

        // =========================================================================
        //  프로퍼티
        // =========================================================================

        /// <summary>가공이 진행 중인지 여부.</summary>
        public bool IsProcessing => _currentlyProcessing != null;
        /// <summary>일꾼이 시설을 사용 중인지 여부.</summary>
        public bool IsOccupied => _activeWorker != null;
        /// <summary>현재 작업 중인 일꾼 참조.</summary>
        public Worker ActiveWorker => _activeWorker;
        /// <summary>현재 가공 중인 자원 데이터.</summary>
        public ResourceDataSO CurrentlyProcessing => _currentlyProcessing;
        /// <summary>현재 사용 중인 레시피 데이터.</summary>
        public RecipeDataSO CurrentRecipe => _currentRecipe;

        /// <summary>
        /// 가공 진행률 (0에서 1 사이의 값).
        /// </summary>
        public float ProcessingProgress
        {
            get
            {
                if (!IsProcessing || _totalProcessingTime <= 0f)
                {
                    return 0f;
                }

                return 1f - (_processingTimer / _totalProcessingTime);
            }
        }

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// 매 프레임 시설 상태를 갱신한다.
        /// </summary>
        protected override void ProcessTick(float deltaTime)
        {
            UpdateVisual();
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 특정 일꾼이 이 시설에 진입하여 작업할 수 있는지 확인한다.
        /// </summary>
        public bool CanWorkerEnter(Worker worker)
        {
            if (worker == null) return false;
            
            // 이미 작업 중인 일꾼이거나, 시설이 비어 있고 예약자가 없거나 내가 예약자인 경우 진입 가능
            if (_activeWorker != null && _activeWorker != worker) return false;
            if (_incomingWorker != null && _incomingWorker != worker) return false;

            // 추가: 물리적으로 해당 시설 위치에 다른 일꾼이 있는지 체크 (나가는 일꾼 배려)
            // 작업 중인 본인이거나 이미 예약자인 경우는 제외
            Worker[] allWorkers = Object.FindObjectsByType<Worker>(FindObjectsSortMode.None);
            foreach (var w in allWorkers)
            {
                if (w == worker || !w.gameObject.activeInHierarchy) continue;
                
                // 시설 중심점으로부터의 거리가 일정 미만이면 누군가 있는 것으로 간주
                if (Vector3.Distance(w.transform.position, transform.position) < 0.65f)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 시설 진입 권한을 예약합니다. 다른 일꾼들의 진입을 막습니다.
        /// </summary>
        public void Reserve(Worker worker)
        {
            if (_activeWorker == null && (_incomingWorker == null || _incomingWorker == worker))
            {
                _incomingWorker = worker;
            }
        }

        /// <summary>
        /// 주어진 입력 자원으로 제작 가능한 레시피가 있는지 확인한다.
        /// </summary>
        public RecipeDataSO FindRecipeFor(ResourceDataSO input)
        {
            if (_facilityData == null || _facilityData.SupportedRecipes == null || input == null)
            {
                return null;
            }

            foreach (RecipeDataSO recipe in _facilityData.SupportedRecipes)
            {
                if (recipe != null && recipe.IsValid && recipe.InputResource == input)
                {
                    return recipe;
                }
            }

            return null;
        }

        /// <summary>
        /// 가공 프로세스를 시작한다.
        /// </summary>
        /// <param name="worker">작업을 수행할 일꾼.</param>
        /// <param name="input">투입된 자원.</param>
        /// <param name="recipe">결정된 레시피 (출력).</param>
        /// <param name="duration">계산된 소요 시간 (출력).</param>
        /// <returns>가공 시작 성공 여부.</returns>
        public bool TryBeginProcessing(Worker worker, ResourceDataSO input, out RecipeDataSO recipe, out float duration)
        {
            recipe = null;
            duration = 0f;

            if (!CanWorkerEnter(worker))
            {
                return false;
            }

            recipe = FindRecipeFor(input);
            if (recipe == null)
            {
                return false;
            }

            float speed = _facilityData != null ? _facilityData.GetProcessingSpeed(Level) : 1f;
            DayManager dayManager = GameManager.Instance != null
                ? GameManager.Instance.Day
                : FindFirstObjectByType<DayManager>();
            if (dayManager != null)
            {
                speed *= dayManager.ProcessingSpeedMultiplier;
            }

            duration = recipe.BaseProcessingTime / Mathf.Max(0.01f, speed);

            _activeWorker = worker;
            _incomingWorker = null; // 작업 시작 시 예약 해제
            _currentlyProcessing = input;
            _currentRecipe = recipe;
            _totalProcessingTime = duration;
            _processingTimer = duration;
            UpdateVisual();
            return true;
        }

        /// <summary>
        /// 가공 진행 상황을 업데이트한다.
        /// </summary>
        public void UpdateProcessingProgress(Worker worker, float remainingTime, float totalTime)
        {
            if (_activeWorker != worker)
            {
                return;
            }

            _totalProcessingTime = Mathf.Max(0f, totalTime);
            _processingTimer = Mathf.Clamp(remainingTime, 0f, _totalProcessingTime);
            UpdateVisual();
        }

        /// <summary>
        /// 가공을 완료하고 결과 자원을 반환한다.
        /// </summary>
        public ResourceDataSO CompleteProcessing(Worker worker)
        {
            if (_activeWorker != worker || _currentRecipe == null)
            {
                return null;
            }

            ResourceDataSO output = _currentRecipe.OutputResource;
            ClearProcessingState();
            return output;
        }

        /// <summary>
        /// 시설을 점유 중인 일꾼을 해제한다.
        /// </summary>
        public void ReleaseWorker(Worker worker)
        {
            if (_activeWorker == worker || _incomingWorker == worker)
            {
                ClearProcessingState();
            }
        }

        // =========================================================================
        //  내부 로직
        // =========================================================================

        /// <summary>
        /// 현재 가공 상태를 초기화한다.
        /// </summary>
        private void ClearProcessingState()
        {
            _activeWorker = null;
            _incomingWorker = null;
            _currentlyProcessing = null;
            _currentRecipe = null;
            _processingTimer = 0f;
            _totalProcessingTime = 0f;
            UpdateVisual();
        }

        /// <summary>
        /// 시설의 시각적 피드백을 업데이트한다. (PoC: 현재는 기본 상태 유지)
        /// </summary>
        private void UpdateVisual()
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            // 시설 자체의 색상 변경은 제거하고 기본 상태 유지 (일꾼 머리 위 진행 바가 주 지표임)
            _spriteRenderer.color = Color.white;
        }
    }
}
