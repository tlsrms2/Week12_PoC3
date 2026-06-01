using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FactoryDelivery.UI
{
    public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        [SerializeField] private string _tooltipText;

        private static GameObject _tooltipRoot;
        private static TextMeshProUGUI _tooltipLabel;
        private static RectTransform _tooltipRect;

        public void SetText(string tooltipText)
        {
            _tooltipText = tooltipText;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_tooltipText))
            {
                return;
            }

            EnsureTooltipRoot(transform);
            if (_tooltipRoot == null || _tooltipLabel == null)
            {
                return;
            }

            _tooltipLabel.text = _tooltipText;
            _tooltipRoot.transform.SetAsLastSibling();
            _tooltipRoot.SetActive(true);

            PositionTooltipFixed();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltipRoot != null)
            {
                _tooltipRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 호버 대상 UI 요소의 우측 가장자리 스크린 좌표를 가져와 툴팁을 오른쪽 끝에 살짝 띄워 배치합니다.
        /// </summary>
        private void PositionTooltipFixed()
        {
            if (_tooltipRoot == null || _tooltipRect == null)
            {
                return;
            }

            RectTransform ownerRect = GetComponent<RectTransform>();
            if (ownerRect == null)
            {
                return;
            }

            // UI 요소의 4개 코너 스크린 좌표 가져오기
            Vector3[] corners = new Vector3[4];
            ownerRect.GetWorldCorners(corners);

            // corners[2]는 우측 상단, corners[3]는 우측 하단. 그 중간 지점이 우측 끝 가장자리 중앙(오른쪽 끝)입니다.
            Vector3 rightCenter = (corners[2] + corners[3]) * 0.5f;

            // 툴팁의 피벗을 좌측 중앙(0, 0.5)으로 설정하여, UI 요소의 우측 옆으로 딱 붙어 뜨도록 만듭니다.
            _tooltipRect.pivot = new Vector2(0f, 0.5f);

            // 우측 가장자리로부터 오른쪽으로 살짝(10 픽셀) 띄워서 안정적으로 배치합니다.
            _tooltipRect.position = rightCenter + new Vector3(10f, 0f, 0f);
        }

        private static void EnsureTooltipRoot(Transform owner)
        {
            if (_tooltipRoot != null)
            {
                return;
            }

            Canvas canvas = owner.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            _tooltipRoot = new GameObject("HoverTooltip");
            _tooltipRoot.transform.SetParent(canvas.transform, false);
            _tooltipRoot.transform.SetAsLastSibling();

            _tooltipRect = _tooltipRoot.AddComponent<RectTransform>();
            _tooltipRect.sizeDelta = new Vector2(280f, 104f);

            CanvasGroup canvasGroup = _tooltipRoot.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            Image background = _tooltipRoot.AddComponent<Image>();
            background.color = new Color(0.06f, 0.05f, 0.04f, 0.96f);

            Outline outline = _tooltipRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.82f, 0.25f, 0.65f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(_tooltipRoot.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);

            _tooltipLabel = textGo.AddComponent<TextMeshProUGUI>();
            _tooltipLabel.fontSize = 12f;
            _tooltipLabel.color = Color.white;
            _tooltipLabel.alignment = TextAlignmentOptions.TopLeft;
            _tooltipLabel.textWrappingMode = TextWrappingModes.Normal;

            _tooltipRoot.SetActive(false);
        }
    }
}
