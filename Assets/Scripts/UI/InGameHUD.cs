using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FactoryDelivery.Core;
using FactoryDelivery.Events;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 인게임의 핵심 정보(할당량 목표치, 일차, 골드 획득량 등)를 시각적으로 미려하게 노출하고,
    /// 시간 배속 및 일시정지, 가동 수동 종료를 다루는 프리엄 HUD 매니저.
    /// </summary>
    public class InGameHUD : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        //  Inspector: References
        // ─────────────────────────────────────────────

        [Header("매니저 직접 참조")]
        [SerializeField] private DayManager _dayManager;
        [SerializeField] private QuotaManager _quotaManager;

        [Header("상단 목표/골드 정보 GUI")]
        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _quotaText;         // e.g. "목표 실적: 50 / 100 엽전"
        [SerializeField] private TextMeshProUGUI _walletText;        // 보유 엽전 표시 텍스트
        [SerializeField] private Slider _quotaProgressBar;            // 목표치 진행률 슬라이더 게이지
        [SerializeField] private Image _progressBarFillImage;        // 게이지 진행 바 색상 틴트용 (달성 시 초록 변환)
        [SerializeField] private Color _progressBarNormalColor = new Color(0.85f, 0.45f, 0.15f); // 일반 오렌지 주황색
        [SerializeField] private Color _progressBarCompletedColor = new Color(0.15f, 0.75f, 0.15f); // 달성 시 초록색

        [Header("가동 페이즈 시간 배속 제어 GUI")]
        [SerializeField] private TextMeshProUGUI _phaseStatusText;  // 현재 건설/가동/정산 상태 텍스트
        [SerializeField] private Button _pauseButton;
        [SerializeField] private TextMeshProUGUI _pauseButtonText;
        [SerializeField] private Button _speed1XButton;
        [SerializeField] private Button _speed2XButton;
        [SerializeField] private Button _speed3XButton;

        [Header("페이즈 진행 버튼")]
        [SerializeField] private Button _actionButton;              // 건설 완료(가동 시작) 또는 가동 완료(정산 시작) 통합 액션 버튼
        [SerializeField] private TextMeshProUGUI _actionButtonText;

        [Header("정산 요약 GUI (Dynamic)")]
        private GameObject _settlementPanel;
        private TextMeshProUGUI _settlementSummaryText;

        // ─────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            if (_quotaManager != null)
            {
                _quotaManager.OnQuotaProgressChanged += UpdateQuotaProgress;
                _quotaManager.OnWalletBalanceChanged += UpdateWalletBalance;
            }

            if (_dayManager != null)
            {
                _dayManager.OnPhaseChanged += OnPhaseChangedHandler;
                _dayManager.OnDayStarted += OnDayStartedHandler;
            }

            // 버튼 리스너 바인딩
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseClicked);
            if (_speed1XButton != null) _speed1XButton.onClick.AddListener(() => SetSpeed(1f));
            if (_speed2XButton != null) _speed2XButton.onClick.AddListener(() => SetSpeed(2f));
            if (_speed3XButton != null) _speed3XButton.onClick.AddListener(() => SetSpeed(3f));
            if (_actionButton != null) _actionButton.onClick.AddListener(OnActionButtonClicked);

            // 초기 셋업
            RefreshAllUI();
        }

        private void OnDisable()
        {
            if (_quotaManager != null)
            {
                _quotaManager.OnQuotaProgressChanged -= UpdateQuotaProgress;
                _quotaManager.OnWalletBalanceChanged -= UpdateWalletBalance;
            }

            if (_dayManager != null)
            {
                _dayManager.OnPhaseChanged -= OnPhaseChangedHandler;
                _dayManager.OnDayStarted -= OnDayStartedHandler;
            }

            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (_actionButton != null) _actionButton.onClick.RemoveListener(OnActionButtonClicked);
        }

        private void Start()
        {
            // 인벤토리 UI 동적 부착 연동
            if (gameObject.GetComponent<InventoryUI>() == null)
            {
                gameObject.AddComponent<InventoryUI>();
            }

            // 정산 패널 동적 생성
            BuildSettlementPanel();
        }

        private void Update()
        {
            if (_dayManager != null && _dayManager.CurrentPhase == DayPhase.Operation)
            {
                int remainingSeconds = Mathf.CeilToInt(_dayManager.OperationTimer);
                if (_phaseStatusText != null)
                {
                    _phaseStatusText.text = $"<color=#2ECC71>[배달의 폭주] 가동 중 - 남은 시간: {remainingSeconds}초</color>";
                }
            }
        }

        private void BuildSettlementPanel()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            _settlementPanel = new GameObject("[SettlementPanel]");
            _settlementPanel.transform.SetParent(canvas.transform, false);

            var rect = _settlementPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, 250f);
            rect.anchoredPosition = new Vector2(0f, 50f);

            var img = _settlementPanel.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            var outline = _settlementPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.83f, 0.68f, 0.21f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_settlementPanel.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);
            titleRect.sizeDelta = new Vector2(0f, 40f);

            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "일일 정산 보고서";
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.83f, 0.68f, 0.21f, 1f);

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(_settlementPanel.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = new Vector2(-40f, -100f);
            contentRect.anchoredPosition = new Vector2(0f, -20f);

            _settlementSummaryText = contentGo.AddComponent<TextMeshProUGUI>();
            _settlementSummaryText.fontSize = 18;
            _settlementSummaryText.alignment = TextAlignmentOptions.Center;
            _settlementSummaryText.lineSpacing = 15f;

            _settlementPanel.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  UI Refresh & Updates
        // ─────────────────────────────────────────────

        private void RefreshAllUI()
        {
            if (_dayManager == null || _quotaManager == null) return;

            OnDayStartedHandler(_dayManager.CurrentDay);
            OnPhaseChangedHandler(_dayManager.CurrentPhase);
            UpdateQuotaProgress(_quotaManager.CurrentProgress, _quotaManager.CurrentQuota);
            UpdateWalletBalance(_quotaManager.WalletBalance);
        }

        private void UpdateWalletBalance(int balance)
        {
            if (_walletText != null)
            {
                _walletText.text = $"보유 엽전: <color=#D4AF37><b>{balance}</b></color> 엽전";
            }
        }

        private void OnDayStartedHandler(int day)
        {
            if (_dayText != null)
            {
                _dayText.text = $"<color=#D4AF37>제 {day} 일차</color>"; // 조선 황실 골드 색상 틴트
            }
            
            if (_settlementPanel != null) _settlementPanel.SetActive(false);
        }

        private void OnPhaseChangedHandler(DayPhase newPhase)
        {
            UpdatePhaseStatusText(newPhase);
            UpdateActionButtonsState(newPhase);
            UpdateTimeControlsState(newPhase);

            if (newPhase == DayPhase.Settlement)
            {
                ShowSettlementSummary();
            }
            else
            {
                if (_settlementPanel != null) _settlementPanel.SetActive(false);
            }
        }

        private void ShowSettlementSummary()
        {
            if (_settlementPanel == null || _settlementSummaryText == null || _quotaManager == null) return;

            bool isMet = _quotaManager.IsQuotaMet;
            string resultColor = isMet ? "#2ECC71" : "#E74C3C";
            string resultText = isMet ? "할당량 달성 성공!" : "할당량 달성 실패...";

            _settlementSummaryText.text = 
                $"현재 일차: 제 {_dayManager.CurrentDay} 일차\n" +
                $"총 판매액: <b>{_quotaManager.CurrentProgress}</b> 엽전\n" +
                $"목표 할당량: <b>{_quotaManager.CurrentQuota}</b> 엽전\n\n" +
                $"<color={resultColor}><size=120%>{resultText}</size></color>";

            _settlementPanel.SetActive(true);
        }

        private void UpdateQuotaProgress(int progress, int quota)
        {
            if (_quotaText != null)
            {
                _quotaText.text = $"오늘의 상단 상납금: <b>{progress}</b> / <color=#D4AF37>{quota}</color> 엽전";
            }

            if (_quotaProgressBar != null)
            {
                _quotaProgressBar.maxValue = quota;
                _quotaProgressBar.value = progress;
            }

            // 달성 여부에 따라 게이지 바 색상 변경 (주황 -> 초록)
            if (_progressBarFillImage != null)
            {
                _progressBarFillImage.color = progress >= quota 
                    ? _progressBarCompletedColor 
                    : _progressBarNormalColor;
            }
        }

        private void UpdatePhaseStatusText(DayPhase phase)
        {
            if (_phaseStatusText == null) return;

            _phaseStatusText.text = phase switch
            {
                DayPhase.Operation => "<color=#2ECC71>[배달의 폭주] 가동 중</color>",
                DayPhase.Settlement => "<color=#E74C3C>[세무의 검증] 일일 정산 중</color>",
                _ => ""
            };
        }

        private void UpdateActionButtonsState(DayPhase phase)
        {
            if (_actionButton == null || _actionButtonText == null) return;

            switch (phase)
            {
                case DayPhase.Operation:
                    _actionButton.gameObject.SetActive(false); // 조기 정산 불가
                    break;

                case DayPhase.Settlement:
                    _actionButton.gameObject.SetActive(true);
                    _actionButton.interactable = true;
                    bool isMet = _quotaManager != null && _quotaManager.IsQuotaMet;
                    _actionButtonText.text = isMet ? "다음 날로 전진 (Space)" : "강제 파산 종료";
                    if (!isMet) _actionButton.interactable = false; // 파산 시 정지
                    break;
            }
        }

        private void UpdateTimeControlsState(DayPhase phase)
        {
            bool isOperation = phase == DayPhase.Operation;

            if (_pauseButton != null) _pauseButton.gameObject.SetActive(isOperation);
            if (_speed1XButton != null) _speed1XButton.gameObject.SetActive(isOperation);
            if (_speed2XButton != null) _speed2XButton.gameObject.SetActive(isOperation);
            if (_speed3XButton != null) _speed3XButton.gameObject.SetActive(isOperation);

            if (isOperation)
            {
                RefreshTimeScaleVisuals();
            }
        }

        // ─────────────────────────────────────────────
        //  Button Handlers
        // ─────────────────────────────────────────────

        private void OnActionButtonClicked()
        {
            if (_dayManager == null) return;

            switch (_dayManager.CurrentPhase)
            {
                case DayPhase.Settlement:
                    _dayManager.ProceedToNextDay();
                    break;
            }
        }

        private void OnPauseClicked()
        {
            if (_dayManager == null) return;

            _dayManager.TogglePause();

            if (_pauseButtonText != null)
            {
                _pauseButtonText.text = _dayManager.IsPaused ? "재개" : "일시정지";
            }
        }

        private void SetSpeed(float scale)
        {
            if (_dayManager == null) return;

            _dayManager.SetTimeScale(scale);
            RefreshTimeScaleVisuals();
        }

        private void RefreshTimeScaleVisuals()
        {
            if (_dayManager == null) return;

            float currentScale = _dayManager.TimeScale;

            // 선택된 배속 버튼의 불투명도를 올려 강조하는 시각 연출
            SetButtonAlpha(_speed1XButton, currentScale == 1f ? 1.0f : 0.4f);
            SetButtonAlpha(_speed2XButton, currentScale == 2f ? 1.0f : 0.4f);
            SetButtonAlpha(_speed3XButton, currentScale == 3f ? 1.0f : 0.4f);
        }

        private void SetButtonAlpha(Button btn, float alpha)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }
    }
}