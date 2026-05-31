using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using FactoryDelivery.Block;
using FactoryDelivery.Data;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 하단 덱의 개별 블록 카드를 관리하고, 마우스 드래그 앤 드롭 조작을 통해
    /// 맵 상에 테트로미노 블록 배치를 트리거해 주는 고급 UI 카드 컴포넌트.
    /// </summary>
    public class BlockCardUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ─────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────

        [Header("카드 정보 GUI")]
        [SerializeField] private TextMeshProUGUI _cardNameText;
        [SerializeField] private Image _cardBackgroundImage;
        [SerializeField] private CanvasGroup _canvasGroup;

        // ─────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────

        private BlockInstance _blockInstance;
        private BlockPlacer _blockPlacer;
        private BlockDeckManager _deckManager;
        private Vector3 _originalPosition;
        private Transform _originalParent;
        private Canvas _parentCanvas;

        // ─────────────────────────────────────────────
        //  Setup & API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 카드 스펙을 바인딩하고 초기 부모를 캐싱한다.
        /// </summary>
        public void Initialize(BlockInstance block, BlockPlacer placer, BlockDeckManager deckMgr, Canvas canvas)
        {
            _blockInstance = block;
            _blockPlacer = placer;
            _deckManager = deckMgr;
            _parentCanvas = canvas;
            _originalParent = transform.parent;

            if (_cardNameText != null && _blockInstance != null)
            {
                _cardNameText.text = GetCardDisplayName(_blockInstance.PatternName);
                StyleCardName();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Draw block shape preview
            CreateBlockShapePreview();
        }

        // ─────────────────────────────────────────────
        //  Drag & Drop Interactions
        // ─────────────────────────────────────────────

        /// <summary>
        /// 마우스 좌클릭 시 작동. 오리지널 위치를 기억하고 드래그 모드를 시작한다.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_blockInstance == null || _blockPlacer == null) return;

            _originalPosition = transform.position;

            // 드래그 중인 카드는 일시적으로 캔버스 전면에 튀어나오게 하고 투명도 조절
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0.5f;
                _canvasGroup.blocksRaycasts = false; // 드롭 감지 방해 예방
            }

            // 캔버스 최상단에 렌더링되도록 부모 임시 변경
            transform.SetParent(_parentCanvas.transform);

            // BlockPlacer에 블록 넘겨 배치 프리뷰 모드 작동 시작!
            _blockPlacer.StartPlacing(_blockInstance);

            // 드래그 개시를 덱 매니저에 보고
            if (_deckManager != null)
            {
                _deckManager.OnCardDragStarted(this);
            }
        }

        /// <summary>
        /// 마우스 드래그 중. 마우스 포지션에 맞추어 카드 UI를 강제로 끌어다 이동시킨다.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            // 드래그 중인 카드를 캔버스의 로컬 공간에 맞추어 마우스를 졸졸 따라다니게 함
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentCanvas.transform as RectTransform,
                eventData.position,
                _parentCanvas.worldCamera,
                out Vector2 localPoint))
            {
                transform.localPosition = localPoint;
            }
        }

        /// <summary>
        /// 마우스 클릭 해제 시(드롭 시). 배치가 성공했는지 판정하여 카드를 파괴하거나 돌려보낸다.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_blockPlacer == null) return;

            bool placementSuccess = false;

            // 1. 만약 드롭된 마우스 포인터가 하단 덱 영역(_originalParent) 안에 있다면,
            // 배치를 아예 시도하지 않고 즉각 취소 처리하여 제자리로 복귀하게 합니다.
            RectTransform deckRect = _originalParent as RectTransform;
            bool droppedInDeck = false;
            if (deckRect != null)
            {
                // UI가 ScreenSpaceOverlay인 경우 카메라는 null, 그렇지 않으면 Canvas의 worldCamera
                Camera cam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) 
                    ? _parentCanvas.worldCamera 
                    : null;
                
                droppedInDeck = RectTransformUtility.RectangleContainsScreenPoint(deckRect, eventData.position, cam);
            }

            if (!droppedInDeck)
            {
                // 2. 하단 덱 영역 밖에서 마우스를 뗀 경우에만 실제 배치 유효성 검사 및 진행
                var validField = _blockPlacer.GetType().GetField("_currentPreviewValid", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (validField != null && (bool)validField.GetValue(_blockPlacer))
                {
                    // 성공적인 배치!
                    // BlockPlacer.OnClick 은 내부에서 TryPlaceTile을 하고, OnBlockPlaced 이벤트를 호출함
                    var onClickMethod = _blockPlacer.GetType().GetMethod("OnClick", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (onClickMethod != null)
                    {
                        onClickMethod.Invoke(_blockPlacer, null);
                        placementSuccess = true;
                    }
                }
            }
            else
            {
                Debug.Log("[BlockCardUI] 카드가 하단 덱 영역 위에 놓여 배치가 취소되었습니다.");
            }

            if (placementSuccess)
            {
                // 배치 대성공! 이 카드는 제 몫을 다했으므로 파괴하고 덱에서 완전히 지운다.
                if (_deckManager != null)
                {
                    _deckManager.RemoveCard(this);
                }
                Destroy(gameObject);
            }
            else
            {
                // 배치 실패 또는 취소! 카드를 하단 슬롯 오리지널 위치로 정중하게 복귀시킴
                _blockPlacer.CancelPlacing();
                ReturnToOriginalPosition();
            }
        }

        private void ReturnToOriginalPosition()
        {
            transform.SetParent(_originalParent);
            transform.position = _originalPosition;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1.0f;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_deckManager != null)
            {
                _deckManager.OnCardDragEnded(this);
            }
        }

        /// <summary>
        /// 카드 상부에 블록의 2D 테트로미노 형태를 색상별 타일 그리드로 그려서 미리 보여줍니다.
        /// </summary>
        private void CreateBlockShapePreview()
        {
            if (_blockInstance == null || _blockInstance.Cells == null) return;

            Transform existing = transform.Find("ShapePreview");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject previewContainer = new GameObject("ShapePreview");
            previewContainer.transform.SetParent(transform, false);

            var rectTrans = previewContainer.AddComponent<RectTransform>();
            // 카드의 중앙/상단부에 배치 (Y 오프셋 조정)
            rectTrans.anchorMin = new Vector2(0.5f, 0.58f);
            rectTrans.anchorMax = new Vector2(0.5f, 0.58f);
            rectTrans.anchoredPosition = Vector2.zero;
            rectTrans.sizeDelta = Vector2.zero;

            var cells = _blockInstance.Cells;
            if (cells == null || cells.Count == 0) return;

            // 바운딩 박스 오프셋 연산으로 타일 중심 정렬
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (var cell in cells)
            {
                Vector2Int pos = cell.LocalPosition;
                if (pos.x < minX) minX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y > maxY) maxY = pos.y;
            }

            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;

            RectTransform cardRect = transform as RectTransform;
            float cardWidth = cardRect != null && cardRect.rect.width > 1f ? cardRect.rect.width : 110f;
            float cardHeight = cardRect != null && cardRect.rect.height > 1f ? cardRect.rect.height : 130f;
            float titleReserve = 34f;
            float maxPreviewWidth = cardWidth * 0.82f;
            float maxPreviewHeight = Mathf.Max(44f, (cardHeight - titleReserve) * 0.78f);
            int gridWidth = Mathf.Max(1, maxX - minX + 1);
            int gridHeight = Mathf.Max(1, maxY - minY + 1);
            float uiCellSize = Mathf.Min(maxPreviewWidth / gridWidth, maxPreviewHeight / gridHeight, 36f);

            foreach (var cell in cells)
            {
                GameObject cellGo = new GameObject("CellPreview");
                cellGo.transform.SetParent(previewContainer.transform, false);
                
                var cellRect = cellGo.AddComponent<RectTransform>();
                cellRect.sizeDelta = new Vector2(uiCellSize * 0.9f, uiCellSize * 0.9f);
                
                // 중심점 오프셋 적용
                float posX = (cell.LocalPosition.x - centerX) * uiCellSize;
                float posY = (cell.LocalPosition.y - centerY) * uiCellSize;
                cellRect.anchoredPosition = new Vector2(posX, posY);

                var img = cellGo.AddComponent<Image>();
                
                // 흰색 사각형 및 타일 고유 색상 바인딩
                if (cell.TileData != null)
                {
                    if (cell.TileData.Sprite != null)
                    {
                        img.sprite = cell.TileData.Sprite;
                    }
                    
                    img.color = Color.white;
                }
            }
        }

        private static string GetCardDisplayName(string patternName)
        {
            if (string.IsNullOrEmpty(patternName)) return string.Empty;
            if (patternName.Contains("자원")) return "자원 블록";
            if (patternName.Contains("시설")) return "시설 블록";
            return patternName.Replace("무작위 ", string.Empty);
        }

        private void StyleCardName()
        {
            RectTransform nameRect = _cardNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 8f);
            nameRect.sizeDelta = new Vector2(-12f, 28f);

            _cardNameText.alignment = TextAlignmentOptions.Center;
            _cardNameText.fontSize = 13f;
            _cardNameText.enableAutoSizing = false;
        }
    }
}
