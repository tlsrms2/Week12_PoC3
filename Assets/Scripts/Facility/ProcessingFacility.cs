using UnityEngine;
using FactoryDelivery.Data;
using FactoryDelivery.Grid;

namespace FactoryDelivery.Facility
{
    /// <summary>
    /// <see cref="FacilityBase"/>를 확장하는 가공 시설.
    /// <see cref="FacilityDataSO.SupportedRecipes"/>에 정의된 레시피를 기반으로
    /// 입력 자원을 출력 자원으로 변환한다.
    /// </summary>
    public class ProcessingFacility : FacilityBase
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("비주얼")]
        [Tooltip("가공 진행률 표시용 스프라이트 렌더러")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        private float _processingTimer;
        private float _totalProcessingTime;
        private ResourceDataSO _currentlyProcessing;
        private RecipeDataSO _currentRecipe;

        // ─────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────

        /// <summary>현재 자원을 가공 중인지 여부.</summary>
        public bool IsProcessing => _currentlyProcessing != null;

        /// <summary>현재 가공 중인 자원 데이터. 가공 중이 아니면 <c>null</c>.</summary>
        public ResourceDataSO CurrentlyProcessing => _currentlyProcessing;

        /// <summary>현재 적용 중인 레시피. 가공 중이 아니면 <c>null</c>.</summary>
        public RecipeDataSO CurrentRecipe => _currentRecipe;

        /// <summary>가공 진행률 (0 ~ 1). 가공 중이 아니면 0.</summary>
        public float ProcessingProgress
        {
            get
            {
                if (!IsProcessing || _totalProcessingTime <= 0f)
                    return 0f;
                return 1f - (_processingTimer / _totalProcessingTime);
            }
        }

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private GridManager _gridManager;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            _gridManager = FindAnyObjectByType<GridManager>();
        }

        // ─────────────────────────────────────────────
        //  Processing Logic
        // ─────────────────────────────────────────────

        /// <summary>
        /// 매 프레임 호출되는 가공 처리 로직.
        /// 1) 가공 중이 아니고 입력 큐에 자원이 있으면 → 가공 시작
        /// 2) 가공 중이면 → 타이머 감소, 완료 시 출력 큐로 이동
        /// </summary>
        /// <param name="deltaTime">이전 프레임 이후 경과 시간 (초).</param>
        protected override void ProcessTick(float deltaTime)
        {
            // 가공 중이 아닌 경우: 입력 큐에서 다음 자원을 꺼내 가공 시작
            if (!IsProcessing)
            {
                TryStartProcessing();
                UpdateVisual();
                return;
            }

            // 가공 중: 타이머 감소
            _processingTimer -= deltaTime;
            UpdateVisual();

            if (_processingTimer <= 0f)
            {
                CompleteProcessing();
            }
        }

        // ─────────────────────────────────────────────
        //  Internal Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// 입력 큐에서 자원을 꺼내 매칭되는 레시피를 찾아 가공을 시작한다.
        /// </summary>
        private void TryStartProcessing()
        {
            if (_inputQueue.Count == 0)
                return;

            // 출력 큐가 가득 차면 가공 시작하지 않음
            if (_outputQueue.Count >= OutputCapacity)
                return;

            ResourceDataSO input = _inputQueue.Peek();
            RecipeDataSO recipe = FindRecipe(input);

            if (recipe == null)
            {
                Debug.LogWarning(
                    $"[ProcessingFacility] '{gameObject.name}': " +
                    $"'{input.DisplayName}'에 대한 레시피를 찾을 수 없습니다.");
                // 처리할 수 없는 자원이므로 큐에서 제거
                _inputQueue.Dequeue();
                return;
            }

            _inputQueue.Dequeue();
            _currentlyProcessing = input;
            _currentRecipe = recipe;

            float speed = _facilityData.GetProcessingSpeed(Level);
            _totalProcessingTime = recipe.BaseProcessingTime / speed;
            _processingTimer = _totalProcessingTime;
        }

        /// <summary>
        /// 가공 완료 시 호출. 출력 자원을 출력 큐에 추가한다.
        /// </summary>
        private void CompleteProcessing()
        {
            if (_currentRecipe != null && _currentRecipe.OutputResource != null)
            {
                _outputQueue.Enqueue(_currentRecipe.OutputResource);
            }

            _currentlyProcessing = null;
            _currentRecipe = null;
            _processingTimer = 0f;
            _totalProcessingTime = 0f;
        }

        /// <summary>
        /// 입력 자원에 매칭되는 레시피를 <see cref="FacilityDataSO.SupportedRecipes"/>에서 검색한다.
        /// </summary>
        /// <param name="input">입력 자원 데이터.</param>
        /// <returns>매칭되는 레시피. 없으면 <c>null</c>.</returns>
        private RecipeDataSO FindRecipe(ResourceDataSO input)
        {
            if (_facilityData == null || _facilityData.SupportedRecipes == null)
                return null;

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
        /// 가공 상태에 따라 스프라이트 색상을 변경하여 시각적 피드백을 제공한다.
        /// - 가공 중: 녹색 계열로 진행률 표시
        /// - 유휴: 기본 색상
        /// </summary>
        private void UpdateVisual()
        {
            if (_spriteRenderer == null)
                return;

            Color baseColor = Color.white;

            if (IsProcessing)
            {
                // 가공 진행률에 따라 고유 색상에서 부드러운 가공 초록색(투명도 섞인)으로 보간
                float progress = ProcessingProgress;
                _spriteRenderer.color = Color.Lerp(baseColor, new Color(0.2f, 0.8f, 0.2f, baseColor.a), progress);
            }
            else
            {
                _spriteRenderer.color = baseColor;
            }
        }
    }
}
