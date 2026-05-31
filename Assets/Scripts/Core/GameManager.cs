using System;
using UnityEngine;
using FactoryDelivery.Events;
using FactoryDelivery.Resource;

namespace FactoryDelivery.Core
{
    /// <summary>
    /// 게임의 최상위 상태를 정의하는 열거형.
    /// </summary>
    public enum GameState
    {
        /// <summary>메인 메뉴 화면.</summary>
        MainMenu,
        /// <summary>인게임 (Day 사이클 진행 중).</summary>
        InGame,
        /// <summary>게임 오버 화면.</summary>
        GameOver
    }

    /// <summary>
    /// 게임의 최상위 라이프사이클을 관리하는 싱글톤 매니저.
    /// 메인 메뉴, 인게임, 게임 오버 상태 전환을 제어한다.
    /// </summary>
    [DefaultExecutionOrder(-9999)]
    public class GameManager : MonoBehaviour
    {
        // =========================================================================
        //  싱글톤
        // =========================================================================

        private static GameManager _instance;

        /// <summary>
        /// GameManager 싱글톤 인스턴스.
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        Debug.LogError("[GameManager] 씬에 GameManager가 존재하지 않습니다.");
                    }
                }
                return _instance;
            }
        }

        // =========================================================================
        //  인스펙터
        // =========================================================================

        [Header("글로벌 설정")]
        [Tooltip("상수 설정을 관리하는 ScriptableObject")]
        [SerializeField] private FactoryDelivery.Data.GameSettingsSO _gameSettings;

        [Header("매니저 참조")]
        [Tooltip("Day 사이클 관리자")]
        [SerializeField] private DayManager _dayManager;

        [Tooltip("할당량 관리자")]
        [SerializeField] private QuotaManager _quotaManager;

        [Tooltip("진상품/총애 관리자")]
        [SerializeField] private TributeManager _tributeManager;

        [Tooltip("총애 상점 관리자")]
        [SerializeField] private FavorShopManager _favorShopManager;

        [Tooltip("판매 계산/보정 관리자")]
        [SerializeField] private SaleModifierManager _saleModifierManager;

        [Header("이벤트 채널")]
        [Tooltip("게임 시작 시 발행")]
        [SerializeField] private VoidEventChannelSO _onGameStarted;

        [Tooltip("게임 오버 시 발행")]
        [SerializeField] private VoidEventChannelSO _onGameOver;

        // =========================================================================
        //  런타임 상태
        // =========================================================================

        private GameState _currentState = GameState.MainMenu;
        private readonly ResourceInventory _inventory = new ResourceInventory();
        private readonly GiftInventory _giftInventory = new GiftInventory();

        // =========================================================================
        //  프로퍼티
        // =========================================================================

        /// <summary>현재 게임 상태.</summary>
        public GameState CurrentState => _currentState;

        /// <summary>플레이어의 글로벌 자원 인벤토리.</summary>
        public ResourceInventory Inventory => _inventory;

        public GiftInventory Gifts => _giftInventory;

        public DayManager Day => _dayManager;

        public TributeManager Tribute => _tributeManager;

        public FavorShopManager FavorShop => _favorShopManager;

        public SaleModifierManager SaleModifiers => _saleModifierManager;

        // =========================================================================
        //  이벤트
        // =========================================================================

        /// <summary>게임 상태가 전환될 때 발생.</summary>
        public event Action<GameState> OnGameStateChanged;

        // =========================================================================
        //  유니티 생명주기
        // =========================================================================

        private void Awake()
        {
            // 게임 중요 상수 에디터 주입
            if (_gameSettings != null)
            {
                _gameSettings.ApplyToConstants();
            }

            // 싱글톤 설정
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_tributeManager == null)
            {
                _tributeManager = GetComponent<TributeManager>();
                if (_tributeManager == null)
                {
                    _tributeManager = gameObject.AddComponent<TributeManager>();
                }
            }

            if (_favorShopManager == null)
            {
                _favorShopManager = GetComponent<FavorShopManager>();
                if (_favorShopManager == null)
                {
                    _favorShopManager = gameObject.AddComponent<FavorShopManager>();
                }
            }

            if (_saleModifierManager == null)
            {
                _saleModifierManager = GetComponent<SaleModifierManager>();
                if (_saleModifierManager == null)
                {
                    _saleModifierManager = gameObject.AddComponent<SaleModifierManager>();
                }
            }
        }

        private void Start()
        {
            // PoC 테스트의 신속성을 위해 플레이 시 자동으로 새 게임을 시작합니다.
            StartNewGame();
        }

        // =========================================================================
        //  공개 API
        // =========================================================================

        /// <summary>
        /// 새 게임을 시작한다.
        /// 그리드를 초기화하고 Day 1을 시작한다.
        /// </summary>
        public void StartNewGame()
        {
            _inventory.Clear();
            _giftInventory.Clear();
            TransitionTo(GameState.InGame);

            _onGameStarted?.RaiseEvent();

            if (_dayManager != null)
            {
                _dayManager.StartDay();
            }

            Debug.Log("[GameManager] 새 게임 시작.");
        }

        /// <summary>
        /// 게임을 종료한다.
        /// </summary>
        /// <param name="finalDay">도달한 마지막 일차.</param>
        /// <param name="metaCurrency">획득한 메타 재화.</param>
        public void EndGame(int finalDay, int metaCurrency)
        {
            TransitionTo(GameState.GameOver);

            _onGameOver?.RaiseEvent();

            Debug.Log($"[GameManager] 게임 오버. 도달 일차: {finalDay}, 메타 재화: {metaCurrency}");
        }

        /// <summary>
        /// 메인 메뉴로 돌아간다.
        /// </summary>
        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            TransitionTo(GameState.MainMenu);

            Debug.Log("[GameManager] 메인 메뉴로 복귀.");
        }

        // =========================================================================
        //  내부 로직
        // =========================================================================

        /// <summary>
        /// 게임 상태를 전환하고 이벤트를 발행한다.
        /// </summary>
        private void TransitionTo(GameState newState)
        {
            GameState previous = _currentState;
            _currentState = newState;

            Debug.Log($"[GameManager] 상태 전환: {previous} → {newState}");
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
