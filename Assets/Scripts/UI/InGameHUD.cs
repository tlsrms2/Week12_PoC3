using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FactoryDelivery.Core;
using FactoryDelivery.Events;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 寃뚯엫 ??HUD瑜?愿由ы븯硫??쒓컙, ?좊떦?? ?먭툑 ?깆쓽 ?뺣낫瑜??붾㈃???쒖떆?섍퀬 ?ъ슜???낅젰??泥섎━?⑸땲??
    /// </summary>
    public class InGameHUD : MonoBehaviour
    {
        // =========================================================================
        //  吏곷젹???꾨뱶
        // =========================================================================

        [Header("留ㅻ땲? 李몄“")]
        [SerializeField] private DayManager _dayManager;
        [SerializeField] private QuotaManager _quotaManager;
        [SerializeField] private TributeManager _tributeManager;

        [Header("?곷떒 ?뺣낫 UI")]
        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _quotaText;
        [SerializeField] private TextMeshProUGUI _walletText;
        [SerializeField] private TextMeshProUGUI _tributeText;

        [Header("?쒓컙 ?쒖뼱 UI")]
        [SerializeField] private TextMeshProUGUI _phaseStatusText;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private TextMeshProUGUI _pauseButtonText;
        [SerializeField] private Button _speed1XButton;
        [SerializeField] private Button _speed2XButton;
        [SerializeField] private Button _speed3XButton;

        [Header("?섏씠利??≪뀡 UI")]
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionButtonText;

        [Header("寃곗궛 ?붿빟 UI")]
        [SerializeField] private GameObject _settlementPanel;
        [SerializeField] private TextMeshProUGUI _settlementSummaryText;

        // =========================================================================
        //  MonoBehaviour
        // =========================================================================

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

            ResolveTributeManager();
            if (_tributeManager != null)
            {
                _tributeManager.OnTributeChanged += UpdateTributeInfo;
                _tributeManager.OnFavorChanged += OnFavorChanged;
            }

            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseClicked);
            if (_speed1XButton != null) _speed1XButton.onClick.AddListener(() => SetSpeed(1f));
            if (_speed2XButton != null) _speed2XButton.onClick.AddListener(() => SetSpeed(2f));
            if (_speed3XButton != null) _speed3XButton.onClick.AddListener(() => SetSpeed(3f));
            if (_actionButton != null) _actionButton.onClick.AddListener(OnActionButtonClicked);

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

            if (_tributeManager != null)
            {
                _tributeManager.OnTributeChanged -= UpdateTributeInfo;
                _tributeManager.OnFavorChanged -= OnFavorChanged;
            }

            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (_actionButton != null) _actionButton.onClick.RemoveListener(OnActionButtonClicked);
        }

        private void Start()
        {
            if (_settlementPanel != null) _settlementPanel.SetActive(false);

            if (GetComponentInChildren<FavorShopUI>() == null)
            {
                GameObject favorShopGo = new GameObject("FavorShopUI");
                favorShopGo.transform.SetParent(transform, false);
                favorShopGo.AddComponent<FavorShopUI>();
            }

            if (GetComponentInChildren<RunSystemsPanelUI>() == null)
            {
                GameObject runSystemsGo = new GameObject("RunSystemsPanelUI");
                runSystemsGo.transform.SetParent(transform, false);
                runSystemsGo.AddComponent<RunSystemsPanelUI>();
            }
        }

        private void Update()
        {
            if (_dayManager == null || _dayManager.CurrentPhase != DayPhase.Operation) return;

            int remainingSeconds = Mathf.CeilToInt(_dayManager.OperationTimer);
            if (_phaseStatusText != null)
            {
                string periodLabel = _dayManager.IsNight ? "밤" : "낮";
                string periodColor = _dayManager.IsNight ? "#8E44AD" : "#2ECC71";
                _phaseStatusText.text = $"<color={periodColor}>[배달 업무] {periodLabel} - 남은 시간: {remainingSeconds}초</color>";
            }
        }
        // =========================================================================
        //  UI 媛깆떊
        // =========================================================================

        /// <summary>
        /// 紐⑤뱺 UI ?붿냼瑜??꾩옱 ?곗씠?곗뿉 留욊쾶 媛깆떊?⑸땲??
        /// </summary>
        private void RefreshAllUI()
        {
            if (_dayManager == null || _quotaManager == null) return;

            OnDayStartedHandler(_dayManager.CurrentDay);
            OnPhaseChangedHandler(_dayManager.CurrentPhase);
            UpdateQuotaProgress(_quotaManager.CurrentProgress, _quotaManager.CurrentQuota);
            UpdateWalletBalance(_quotaManager.WalletBalance);
            UpdateTributeInfo();
        }

        /// <summary>
        /// 蹂댁쑀 ?먭툑 ?띿뒪?몃? ?낅뜲?댄듃?⑸땲??
        /// </summary>
        private void UpdateWalletBalance(int balance)
        {
            if (_walletText != null)
            {
                _walletText.text = $"蹂댁쑀 ?쎌쟾: <color=#D4AF37><b>{balance}</b></color> ?쎌쟾";
            }
        }

        /// <summary>
        /// ?덈줈???좎씠 ?쒖옉?????몄텧?섏뼱 ?좎쭨 ?쒖떆瑜?媛깆떊?⑸땲??
        /// </summary>
        private void OnDayStartedHandler(int day)
        {
            if (_dayText != null)
            {
                _dayText.text = $"<color=#D4AF37>??{day} ?쇱감</color>";
            }

            if (_settlementPanel != null) _settlementPanel.SetActive(false);
        }

        /// <summary>
        /// ?섏씠利?蹂寃????몄텧?섏뼱 UI ?곹깭瑜??꾪솚?⑸땲??
        /// </summary>
        private void OnPhaseChangedHandler(DayPhase newPhase)
        {
            UpdatePhaseStatusText(newPhase);
            UpdateActionButtonsState(newPhase);
            UpdateTimeControlsState(newPhase);

            if (newPhase == DayPhase.Settlement)
            {
                ShowSettlementSummary();
            }
            else if (_settlementPanel != null)
            {
                _settlementPanel.SetActive(false);
            }
        }

        /// <summary>
        /// ?쇱씪 寃곗궛 ?붿빟 ?⑤꼸???쒖떆?⑸땲??
        /// </summary>
        private void ShowSettlementSummary()
        {
            if (_settlementPanel == null || _settlementSummaryText == null || _quotaManager == null) return;

            bool isMet = _quotaManager.IsQuotaMet;
            string resultColor = isMet ? "#2ECC71" : "#E74C3C";
            string resultText = isMet ? "?좊떦???ъ꽦 ?깃났!" : "?좊떦???ъ꽦 ?ㅽ뙣...";

            _settlementSummaryText.text =
                $"?꾩옱 ?쇱감: ??{_dayManager.CurrentDay} ?쇱감\n" +
                $"珥??먮ℓ?? <b>{_quotaManager.CurrentProgress}</b> ?쎌쟾\n" +
                $"紐⑺몴 ?좊떦?? <b>{_quotaManager.CurrentQuota}</b> ?쎌쟾\n\n" +
                $"<color={resultColor}><size=120%>{resultText}</size></color>";

            _settlementPanel.SetActive(true);
        }

        /// <summary>
        /// ?좊떦??吏꾪뻾?꾨? ?낅뜲?댄듃?⑸땲??
        /// </summary>
        private void UpdateQuotaProgress(int progress, int quota)
        {
            if (_quotaText != null)
            {
                _quotaText.text = $"?ㅻ뒛???곷떒 ?곷궔湲? <b>{progress}</b> / <color=#D4AF37>{quota}</color> ?쎌쟾";
            }
        }

        private void UpdateTributeInfo()
        {
            if (_tributeText == null) return;

            ResolveTributeManager();
            if (_tributeManager == null || _tributeManager.CurrentTributeResource == null)
            {
                _tributeText.text = "Tribute: preparing";
                return;
            }

            _tributeText.text =
                $"吏꾩긽?? <b>{_tributeManager.CurrentTributeResource.DisplayName}</b> " +
                $"{_tributeManager.DeliveredAmount}/{_tributeManager.RequiredAmount} | " +
                $"珥앹븷: <color=#D4AF37><b>{_tributeManager.FavorBalance}</b></color>";
        }

        private void OnFavorChanged(int favor)
        {
            UpdateTributeInfo();
        }

        private void ResolveTributeManager()
        {
            if (_tributeManager != null) return;

            _tributeManager = GameManager.Instance != null
                ? GameManager.Instance.Tribute
                : FindFirstObjectByType<TributeManager>();
        }

        /// <summary>
        /// ?섏씠利??곹깭 ?띿뒪?몃? ?낅뜲?댄듃?⑸땲??
        /// </summary>
        private void UpdatePhaseStatusText(DayPhase phase)
        {
            if (_phaseStatusText == null) return;

            _phaseStatusText.text = phase switch
            {
                DayPhase.Operation => "<color=#2ECC71>[諛곕떖 ?낅Т] 媛??以?/color>",
                DayPhase.Settlement => "<color=#E74C3C>[?λ? 寃利? ?쇱씪 寃곗궛 以?/color>",
                _ => string.Empty
            };
        }

        /// <summary>
        /// ?꾩옱 ?섏씠利덉뿉 ?곕씪 ?≪뀡 踰꾪듉???쒖꽦 ?곹깭瑜??낅뜲?댄듃?⑸땲??
        /// </summary>
        private void UpdateActionButtonsState(DayPhase phase)
        {
            if (_actionButton == null || _actionButtonText == null) return;

            switch (phase)
            {
                case DayPhase.Operation:
                    _actionButton.gameObject.SetActive(false);
                    break;

                case DayPhase.Settlement:
                    _actionButton.gameObject.SetActive(true);
                    _actionButton.interactable = true;

                    bool isMet = _quotaManager != null && _quotaManager.IsQuotaMet;
                    _actionButtonText.text = isMet ? "?ㅼ쓬 ?좊줈 ?꾩쭊 (Space)" : "媛뺤젣 ?뚯궛 醫낅즺";
                    if (!isMet) _actionButton.interactable = false;
                    break;
            }
        }

        /// <summary>
        /// ?꾩옱 ?섏씠利덉뿉 ?곕씪 ?쒓컙 ?쒖뼱 UI???쒖꽦 ?곹깭瑜??낅뜲?댄듃?⑸땲??
        /// </summary>
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

        // =========================================================================
        //  ?대깽???몃뱾??
        // =========================================================================

        /// <summary>
        /// ?≪뀡 踰꾪듉 ?대┃ ???ㅼ쓬 ?④퀎濡?吏꾪뻾?⑸땲??
        /// </summary>
        private void OnActionButtonClicked()
        {
            if (_dayManager == null) return;

            if (_dayManager.CurrentPhase == DayPhase.Settlement)
            {
                _dayManager.ProceedToNextDay();
            }
        }

        /// <summary>
        /// ?쇱떆?뺤? 踰꾪듉 ?대┃ ???ㅽ뻾?⑸땲??
        /// </summary>
        private void OnPauseClicked()
        {
            if (_dayManager == null) return;

            _dayManager.TogglePause();

            if (_pauseButtonText != null)
            {
                _pauseButtonText.text = _dayManager.IsPaused ? "?ш컻" : "?쇱떆?뺤?";
            }
        }

        /// <summary>
        /// 寃뚯엫 ?띾룄瑜??ㅼ젙?⑸땲??
        /// </summary>
        private void SetSpeed(float scale)
        {
            if (_dayManager == null) return;

            _dayManager.SetTimeScale(scale);
            RefreshTimeScaleVisuals();
        }

        /// <summary>
        /// ?쒓컙 諛곗냽 踰꾪듉??鍮꾩＜???곹깭瑜?媛깆떊?⑸땲??
        /// </summary>
        private void RefreshTimeScaleVisuals()
        {
            if (_dayManager == null) return;

            float currentScale = _dayManager.TimeScale;

            SetButtonAlpha(_speed1XButton, currentScale == 1f ? 1.0f : 0.4f);
            SetButtonAlpha(_speed2XButton, currentScale == 2f ? 1.0f : 0.4f);
            SetButtonAlpha(_speed3XButton, currentScale == 3f ? 1.0f : 0.4f);
        }

        /// <summary>
        /// 踰꾪듉???щ챸?꾨? 議곗젅?⑸땲??
        /// </summary>
        private void SetButtonAlpha(Button btn, float alpha)
        {
            if (btn == null) return;

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                Color color = img.color;
                color.a = alpha;
                img.color = color;
            }
        }
    }
}

