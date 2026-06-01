using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FactoryDelivery.Core;

namespace FactoryDelivery.UI
{
    public class InGameHUD : MonoBehaviour
    {
        private enum SettlementUiStep
        {
            None,
            Report,
            MandateChoice,
            Shop
        }

        [Header("Manager References")]
        [SerializeField] private DayManager _dayManager;
        [SerializeField] private QuotaManager _quotaManager;
        [SerializeField] private TributeManager _tributeManager;

        [Header("Top HUD")]
        [SerializeField] private TextMeshProUGUI _dayText;
        [SerializeField] private TextMeshProUGUI _quotaText;
        [SerializeField] private TextMeshProUGUI _walletText;
        [SerializeField] private TextMeshProUGUI _tributeText;

        [Header("Primary Controls")]
        [SerializeField] private TextMeshProUGUI _phaseStatusText;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private TextMeshProUGUI _pauseButtonText;
        [SerializeField] private Button _speed1XButton;
        [SerializeField] private Button _speed2XButton;
        [SerializeField] private Button _speed3XButton;

        [Header("Legacy Action Button")]
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionButtonText;

        [Header("Settlement")]
        [SerializeField] private GameObject _settlementPanel;
        [SerializeField] private TextMeshProUGUI _settlementSummaryText;

        [Header("Scene Runtime Panels")]
        [SerializeField] private RectTransform _mandateListRoot;
        [SerializeField] private RectTransform _mandateChoiceRoot;
        [SerializeField] private FavorShopUI _favorShopUI;

        [Header("Mandate Choice UI References")]
        [Tooltip("씬에 미리 생성된 어명 선택 카드 버튼 목록 (Bake 툴에 의해 자동 연동됨)")]
        [SerializeField] private List<Button> _choiceButtons = new List<Button>();

        private bool _settlementMandateSelected;
        private SettlementUiStep _settlementUiStep = SettlementUiStep.None;

        private void OnEnable()
        {
            ResolveReferences();
            ResolveSceneUiReferences();
            Subscribe();
            RefreshAllUI();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Start()
        {
            ResolveReferences();
            ResolveSceneUiReferences();

            if (_settlementPanel != null) _settlementPanel.SetActive(false);
            if (_mandateChoiceRoot != null) _mandateChoiceRoot.gameObject.SetActive(false);
            _favorShopUI?.HideShop();

            if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems)
            {
                if (_tributeText != null) _tributeText.gameObject.SetActive(false);
                if (_mandateListRoot != null) _mandateListRoot.gameObject.SetActive(false);
            }

            RefreshAllUI();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                OnPrimaryButtonClicked();
            }

            if (_dayManager == null || _dayManager.CurrentPhase != DayPhase.Operation)
            {
                return;
            }

            int remainingSeconds = Mathf.CeilToInt(_dayManager.OperationTimer);
            if (_phaseStatusText != null)
            {
                string periodLabel = _dayManager.IsNight ? "밤" : "낮";
                string periodColor = _dayManager.IsNight ? "#8E44AD" : "#2ECC71";
                _phaseStatusText.text = $"<color={periodColor}>[가동 페이즈 {periodLabel} - 남은 시간: {remainingSeconds}초]</color>";
            }
        }

        private void Subscribe()
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

            if (_tributeManager != null)
            {
                _tributeManager.OnTributeChanged += UpdateTributeInfo;
                _tributeManager.OnFavorChanged += OnFavorChanged;
            }

            if (MandateManager.Instance != null)
            {
                MandateManager.Instance.OnMandatesChanged += RefreshMandateList;
            }

            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPrimaryButtonClicked);
            if (_speed1XButton != null) _speed1XButton.onClick.AddListener(SetSpeed1x);
            if (_speed2XButton != null) _speed2XButton.onClick.AddListener(SetSpeed2x);
            if (_speed3XButton != null) _speed3XButton.onClick.AddListener(SetSpeed3x);
            if (_actionButton != null) _actionButton.onClick.AddListener(OnPrimaryButtonClicked);
        }

        private void Unsubscribe()
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

            if (MandateManager.Instance != null)
            {
                MandateManager.Instance.OnMandatesChanged -= RefreshMandateList;
            }

            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPrimaryButtonClicked);
            if (_speed1XButton != null) _speed1XButton.onClick.RemoveListener(SetSpeed1x);
            if (_speed2XButton != null) _speed2XButton.onClick.RemoveListener(SetSpeed2x);
            if (_speed3XButton != null) _speed3XButton.onClick.RemoveListener(SetSpeed3x);
            if (_actionButton != null) _actionButton.onClick.RemoveListener(OnPrimaryButtonClicked);
        }

        private void ResolveReferences()
        {
            if (_dayManager == null) _dayManager = FindFirstObjectByType<DayManager>();
            if (_quotaManager == null) _quotaManager = FindFirstObjectByType<QuotaManager>();
            if (_tributeManager == null)
            {
                _tributeManager = GameManager.Instance != null
                    ? GameManager.Instance.Tribute
                    : FindFirstObjectByType<TributeManager>();
            }
        }

        private void ResolveSceneUiReferences()
        {
            if (_pauseButton == null)
            {
                Transform found = transform.Find("PauseButton");
                if (found != null) _pauseButton = found.GetComponent<Button>();
            }

            if (_pauseButtonText == null && _pauseButton != null)
            {
                _pauseButtonText = _pauseButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_tributeText == null)
            {
                Transform found = transform.Find("TributeText");
                if (found != null) _tributeText = found.GetComponent<TextMeshProUGUI>();
            }

            if (_mandateListRoot == null)
            {
                Transform found = transform.Find("MandateListPanel");
                if (found != null) _mandateListRoot = found as RectTransform;
            }

            if (_mandateChoiceRoot == null)
            {
                Transform found = transform.Find("MandateChoicePanel");
                if (found != null) _mandateChoiceRoot = found as RectTransform;
            }

            if (_favorShopUI == null)
            {
                _favorShopUI = GetComponentInChildren<FavorShopUI>(true);
            }
        }

        private void RefreshAllUI()
        {
            if (_dayManager == null || _quotaManager == null) return;

            OnDayStartedHandler(_dayManager.CurrentDay);
            OnPhaseChangedHandler(_dayManager.CurrentPhase);
            UpdateQuotaProgress(_quotaManager.CurrentProgress, _quotaManager.CurrentQuota);
            UpdateWalletBalance(_quotaManager.WalletBalance);
            UpdateTributeInfo();
            RefreshMandateList();
            RefreshPrimaryButtonState();
        }

        private void OnDayStartedHandler(int day)
        {
            _settlementMandateSelected = false;
            _settlementUiStep = SettlementUiStep.None;
            ClearMandateChoicePanel();
            _favorShopUI?.HideShop();

            if (_dayText != null)
            {
                _dayText.text = $"<color=#D4AF37>제 {day}일차</color>";
            }

            if (_settlementPanel != null) _settlementPanel.SetActive(false);
            UpdateTributeInfo();
            RefreshPrimaryButtonState();
        }

        private void OnPhaseChangedHandler(DayPhase newPhase)
        {
            UpdatePhaseStatusText(newPhase);
            UpdateTimeControlsState(newPhase);

            if (newPhase == DayPhase.Settlement)
            {
                _settlementMandateSelected = false;
                _settlementUiStep = SettlementUiStep.Report;
                ShowSettlementSummary();
                ClearMandateChoicePanel();
                _favorShopUI?.HideShop();
            }
            else
            {
                _settlementUiStep = SettlementUiStep.None;
                if (_settlementPanel != null) _settlementPanel.SetActive(false);
            }

            RefreshPrimaryButtonState();
        }

        private void ShowSettlementSummary()
        {
            if (_settlementPanel == null || _settlementSummaryText == null || _quotaManager == null || _dayManager == null) return;

            bool isMet = _quotaManager.IsQuotaMet;
            string resultColor = isMet ? "#2ECC71" : "#E74C3C";
            string resultText = isMet ? "할당량 달성 성공!" : "할당량 달성 실패...";
            string favorInfo = BuildFavorSettlementText();

            _settlementSummaryText.text =
                $"제 {_dayManager.CurrentDay}일차\n" +
                $"총 판매액: <b>{_quotaManager.CurrentProgress}</b> 엽전\n" +
                $"할당량: <b>{_quotaManager.CurrentQuota}</b> 엽전\n" +
                $"{favorInfo}\n" +
                $"<color={resultColor}><size=120%>{resultText}</size></color>\n\n" +
                $"<size=80%>Space로 계속</size>";

            _settlementPanel.SetActive(true);
        }

        private string BuildFavorSettlementText()
        {
            if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems)
            {
                return string.Empty;
            }

            if (_tributeManager == null)
            {
                return "총애 획득: 0";
            }

            if (_tributeManager.TodayFavorEarned <= 0)
            {
                return "총애 획득: 0\n";
            }

            return $"총애 획득: <color=#D4AF37>+{_tributeManager.TodayFavorEarned}</color>\n" +
                   string.Join(", ", _tributeManager.TodayFavorReasons);
        }

        private void ShowMandateChoicesIfNeeded()
        {
            if (_quotaManager == null || !_quotaManager.IsQuotaMet)
            {
                RefreshPrimaryButtonState();
                return;
            }

            List<MandateType> choices = BuildMandateChoices(3);
            if (choices.Count == 0)
            {
                _settlementMandateSelected = true;
                _settlementUiStep = SettlementUiStep.Shop;
                _favorShopUI?.ShowShop();
                RefreshPrimaryButtonState();
                return;
            }

            _settlementUiStep = SettlementUiStep.MandateChoice;
            CreateMandateChoicePanel(choices);
            RefreshPrimaryButtonState();
        }

        private List<MandateType> BuildMandateChoices(int count)
        {
            var available = new List<MandateType>();
            foreach (MandateType type in System.Enum.GetValues(typeof(MandateType)))
            {
                if (type == MandateType.None) continue;
                if (MandateManager.Instance != null && MandateManager.Instance.HasMandate(type)) continue;
                available.Add(type);
            }

            var result = new List<MandateType>();
            while (available.Count > 0 && result.Count < count)
            {
                int index = Random.Range(0, available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }

            return result;
        }

        private void CreateMandateChoicePanel(List<MandateType> choices)
        {
            if (_mandateChoiceRoot == null) return;

            _mandateChoiceRoot.gameObject.SetActive(true);

            // 씬에 미리 배치되어 할당된 어명 선택 카드들을 순회하며 바인딩
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                Button btn = _choiceButtons[i];
                if (btn == null) continue;

                if (i < choices.Count)
                {
                    MandateType augment = choices[i];
                    btn.gameObject.SetActive(true);

                    // 버튼 이벤트 등록 (중복 리스너 방지를 위해 먼저 클리어)
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        MandateManager.Instance?.AddMandate(augment);
                        _settlementMandateSelected = true;
                        _settlementUiStep = SettlementUiStep.Shop;
                        ClearMandateChoicePanel();
                        _favorShopUI?.ShowShop();
                        RefreshPrimaryButtonState();
                    });

                    // 카드 텍스트 업데이트
                    Transform nameTrans = btn.transform.Find("NameText");
                    if (nameTrans != null)
                    {
                        TextMeshProUGUI nameTxt = nameTrans.GetComponent<TextMeshProUGUI>();
                        if (nameTxt != null) nameTxt.text = GetMandateLabel(augment);
                    }

                    Transform descTrans = btn.transform.Find("DescText");
                    if (descTrans != null)
                    {
                        TextMeshProUGUI descTxt = descTrans.GetComponent<TextMeshProUGUI>();
                        if (descTxt != null) descTxt.text = GetMandateDescription(augment);
                    }
                }
                else
                {
                    // 활성화할 어명이 없으면 카드 숨김
                    btn.gameObject.SetActive(false);
                }
            }
        }

        private void ClearMandateChoicePanel()
        {
            if (_mandateChoiceRoot == null) return;
            _mandateChoiceRoot.gameObject.SetActive(false);
        }

        private void UpdateQuotaProgress(int progress, int quota)
        {
            if (_quotaText != null)
            {
                _quotaText.text = $"오늘의 할당량 수입: <b>{progress}</b> / <color=#D4AF37>{quota}</color> 엽전";
            }
        }

        private void UpdateWalletBalance(int balance)
        {
            if (_walletText != null)
            {
                _walletText.text = $"보유 엽전: <color=#D4AF37><b>{balance}</b></color>";
            }
        }

        private void UpdateTributeInfo()
        {
            if (_tributeText == null) return;

            ResolveReferences();
            if (_tributeManager == null || _tributeManager.CurrentTributeResource == null)
            {
                _tributeText.text = "<b>오늘의 진상품</b> 준비 중";
                return;
            }

            _tributeText.text =
                $"<b>오늘의 진상품</b>  {_tributeManager.CurrentTributeResource.DisplayName}  " +
                $"<color=#7FDBFF>{_tributeManager.DeliveredAmount}</color> / " +
                $"<color=#FFD166>{_tributeManager.RequiredAmount}</color>";
        }

        private void RefreshMandateList()
        {
            if (_mandateListRoot == null) return;

            ClearChildren(_mandateListRoot);

            TextMeshProUGUI title = CreateText(_mandateListRoot, "획득한 어명", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.color = new Color(1f, 0.84f, 0.22f, 1f);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            IReadOnlyCollection<MandateType> augments = MandateManager.Instance != null
                ? MandateManager.Instance.ActiveMandates
                : null;

            if (augments == null || augments.Count == 0)
            {
                TextMeshProUGUI empty = CreateText(_mandateListRoot, "아직 획득한 어명이 없습니다.", 13f, FontStyles.Normal, TextAlignmentOptions.Left);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
                return;
            }

            foreach (MandateType augment in augments)
            {
                CreateMandateListItem(augment);
            }
        }

        private void CreateMandateListItem(MandateType augment)
        {
            GameObject itemGo = new GameObject("Mandate_" + augment);
            itemGo.transform.SetParent(_mandateListRoot, false);
            itemGo.AddComponent<LayoutElement>().preferredHeight = 42f;

            Image image = itemGo.AddComponent<Image>();
            image.color = new Color(0.13f, 0.10f, 0.06f, 0.92f);

            TextMeshProUGUI label = CreateText(itemGo.transform, GetMandateLabel(augment), 13f, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 5f);
            labelRect.offsetMax = new Vector2(-10f, -5f);
            label.color = Color.white;

            itemGo.AddComponent<HoverTooltip>().SetText($"{GetMandateLabel(augment)}\n{GetMandateDescription(augment)}");
        }

        private void OnFavorChanged(int favor)
        {
            UpdateTributeInfo();
            if (_dayManager != null && _dayManager.CurrentPhase == DayPhase.Settlement && _settlementUiStep == SettlementUiStep.Report)
            {
                ShowSettlementSummary();
            }
        }

        private void UpdatePhaseStatusText(DayPhase phase)
        {
            if (_phaseStatusText == null) return;

            _phaseStatusText.text = phase switch
            {
                DayPhase.Operation => "<color=#2ECC71>[가동 페이즈 진행 중]</color>",
                DayPhase.Settlement => "<color=#E74C3C>[정산 페이즈]</color>",
                _ => string.Empty
            };
        }

        private void UpdateTimeControlsState(DayPhase phase)
        {
            bool isOperation = phase == DayPhase.Operation;

            if (_pauseButton != null) _pauseButton.gameObject.SetActive(true);
            if (_speed1XButton != null) _speed1XButton.gameObject.SetActive(isOperation);
            if (_speed2XButton != null) _speed2XButton.gameObject.SetActive(isOperation);
            if (_speed3XButton != null) _speed3XButton.gameObject.SetActive(isOperation);
            if (_actionButton != null) _actionButton.gameObject.SetActive(false);

            if (isOperation)
            {
                RefreshTimeScaleVisuals();
            }
        }

        private void RefreshPrimaryButtonState()
        {
            if (_pauseButton == null) return;

            _pauseButton.gameObject.SetActive(true);
            _pauseButton.interactable = true;

            if (_pauseButtonText == null || _dayManager == null) return;

            if (_dayManager.CurrentPhase == DayPhase.Operation)
            {
                _pauseButtonText.text = _dayManager.IsPaused ? "재개 (Space)" : "일시정지 (Space)";
                return;
            }

            if (_dayManager.CurrentPhase == DayPhase.Settlement)
            {
                bool isMet = _quotaManager != null && _quotaManager.IsQuotaMet;
                if (_settlementUiStep == SettlementUiStep.Report)
                {
                    _pauseButtonText.text = isMet ? "정산 확인 (Space)" : "정산 종료 (Space)";
                }
                else if (_settlementUiStep == SettlementUiStep.MandateChoice)
                {
                    _pauseButtonText.text = "어명 선택 필요";
                    _pauseButton.interactable = false;
                }
                else
                {
                    _pauseButtonText.text = _settlementMandateSelected ? "다음 날로 진행 (Space)" : "대기";
                }
            }
        }

        private void OnPrimaryButtonClicked()
        {
            if (_dayManager == null) return;

            if (_dayManager.CurrentPhase == DayPhase.Operation)
            {
                _dayManager.TogglePause();
                RefreshPrimaryButtonState();
                return;
            }

            if (_dayManager.CurrentPhase != DayPhase.Settlement) return;

            if (_settlementUiStep == SettlementUiStep.Report)
            {
                if (_settlementPanel != null) _settlementPanel.SetActive(false);

                if (GameManager.Instance != null && GameManager.Instance.DisableRoguelikeSystems)
                {
                    _settlementUiStep = SettlementUiStep.None;
                    _dayManager.ProceedToNextDay();
                    return;
                }

                if (_quotaManager != null && _quotaManager.IsQuotaMet)
                {
                    ShowMandateChoicesIfNeeded();
                }
                else
                {
                    _settlementUiStep = SettlementUiStep.None;
                }

                RefreshPrimaryButtonState();
                return;
            }

            if (_settlementMandateSelected)
            {
                _dayManager.ProceedToNextDay();
            }
        }

        private void SetSpeed1x() => SetSpeed(1f);
        private void SetSpeed2x() => SetSpeed(2f);
        private void SetSpeed3x() => SetSpeed(3f);

        private void SetSpeed(float scale)
        {
            if (_dayManager == null) return;

            _dayManager.SetTimeScale(scale);
            RefreshTimeScaleVisuals();
            RefreshPrimaryButtonState();
        }

        private void RefreshTimeScaleVisuals()
        {
            if (_dayManager == null) return;

            float currentScale = _dayManager.TimeScale;
            SetButtonAlpha(_speed1XButton, currentScale == 1f ? 1.0f : 0.4f);
            SetButtonAlpha(_speed2XButton, currentScale == 2f ? 1.0f : 0.4f);
            SetButtonAlpha(_speed3XButton, currentScale == 3f ? 1.0f : 0.4f);
        }

        private void SetButtonAlpha(Button button, float alpha)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(parent, false);
            TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = alignment;
            return label;
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null) return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        public static string GetMandateLabel(MandateType augment)
        {
            return augment switch
            {
                MandateType.BlessingOfCoupledTrees => "연리지의 축복",
                MandateType.WideRoadPaving => "광폭 도로 포장",
                MandateType.NocturnalWorkers => "야행의 인력",
                MandateType.WhiteNightLabor => "백야의 노동",
                MandateType.ArtisansTouch => "장인의 손길",
                MandateType.MasterStrokeRoad => "도편수의 도로",
                _ => augment.ToString()
            };
        }

        public static string GetMandateDescription(MandateType augment)
        {
            return augment switch
            {
                MandateType.BlessingOfCoupledTrees => "가공 시설 처리 속도가 영구적으로 20% 증가합니다.",
                MandateType.WideRoadPaving => "일꾼 이동 속도가 영구적으로 2배가 됩니다.",
                MandateType.NocturnalWorkers => "낮에는 느려지지만 밤에는 일꾼 속도가 크게 증가합니다.",
                MandateType.WhiteNightLabor => "밤 시간 동안 일꾼 속도와 가공 속도가 증가합니다.",
                MandateType.ArtisansTouch => "새 가공 시설이 일정 확률로 2레벨부터 시작합니다.",
                MandateType.MasterStrokeRoad => "도로 건설 비용이 절반으로 줄어듭니다.",
                _ => "이번 판 동안 지속되는 어명입니다."
            };
        }
    }
}
