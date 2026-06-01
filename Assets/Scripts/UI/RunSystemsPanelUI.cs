using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FactoryDelivery.Core;

namespace FactoryDelivery.UI
{
    public class RunSystemsPanelUI : MonoBehaviour
    {
        [SerializeField] private Transform _edictContainer;
        [SerializeField] private Transform _giftContainer;

        private void Awake()
        {
            EnsureScenePanelFallback();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Refresh()
        {
            RefreshEdicts();
            RefreshGifts();
        }

        private void EnsureScenePanelFallback()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -62f);
            root.sizeDelta = new Vector2(560f, 72f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.075f, 0.06f, 0.78f);

            HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            if (_edictContainer != null && _giftContainer != null)
            {
                return;
            }

            ClearAllChildren(transform);
            _edictContainer = CreateIconSection("교지");
            _giftContainer = CreateIconSection("하사품");
        }

        private Transform CreateIconSection(string title)
        {
            GameObject sectionGo = new GameObject(title);
            sectionGo.transform.SetParent(transform, false);

            Image image = sectionGo.AddComponent<Image>();
            image.color = new Color(0.14f, 0.115f, 0.08f, 0.95f);

            VerticalLayoutGroup layout = sectionGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 5, 5);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI label = CreateText(sectionGo.transform, title, 11f, FontStyles.Bold, TextAlignmentOptions.Center);
            label.color = new Color(1f, 0.84f, 0.22f, 1f);
            label.gameObject.name = "Title";
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            GameObject iconsGo = new GameObject("Icons");
            iconsGo.transform.SetParent(sectionGo.transform, false);
            HorizontalLayoutGroup iconsLayout = iconsGo.AddComponent<HorizontalLayoutGroup>();
            iconsLayout.spacing = 5f;
            iconsLayout.childAlignment = TextAnchor.MiddleCenter;
            iconsLayout.childControlWidth = false;
            iconsLayout.childControlHeight = false;
            iconsLayout.childForceExpandWidth = false;
            iconsLayout.childForceExpandHeight = false;
            iconsGo.AddComponent<LayoutElement>().preferredHeight = 38f;

            return iconsGo.transform;
        }

        private void Subscribe()
        {
            if (EdictManager.Instance != null)
            {
                EdictManager.Instance.OnEdictsChanged += Refresh;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gifts.OnGiftCountChanged += OnGiftCountChanged;
            }
        }

        private void Unsubscribe()
        {
            if (EdictManager.Instance != null)
            {
                EdictManager.Instance.OnEdictsChanged -= Refresh;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gifts.OnGiftCountChanged -= OnGiftCountChanged;
            }
        }

        private void OnGiftCountChanged(GiftType giftType, int count)
        {
            Refresh();
        }

        private void RefreshEdicts()
        {
            ClearChildren(_edictContainer);

            List<EdictType> edicts = EdictManager.Instance != null
                ? EdictManager.Instance.GetEquippedEdicts()
                : null;

            if (edicts == null || edicts.Count == 0)
            {
                CreateEmptyIcon(_edictContainer, "없음");
                return;
            }

            foreach (EdictType edict in edicts)
            {
                CreateIcon(_edictContainer, GetEdictShortLabel(edict), GetEdictTooltip(edict));
            }
        }

        private void RefreshGifts()
        {
            ClearChildren(_giftContainer);

            if (GameManager.Instance == null)
            {
                CreateEmptyIcon(_giftContainer, "없음");
                return;
            }

            bool hasGift = false;
            foreach (GiftType giftType in System.Enum.GetValues(typeof(GiftType)))
            {
                int count = GameManager.Instance.Gifts.GetCount(giftType);
                if (count <= 0) continue;

                hasGift = true;
                CreateIcon(_giftContainer, count.ToString(), $"{GetGiftLabel(giftType)}\n{GetGiftDescription(giftType)}\n보유: {count}");
            }

            if (!hasGift)
            {
                CreateEmptyIcon(_giftContainer, "없음");
            }
        }

        private void CreateIcon(Transform parent, string label, string tooltip)
        {
            if (parent == null) return;

            GameObject iconGo = new GameObject("Icon_" + label);
            iconGo.transform.SetParent(parent, false);

            RectTransform rect = iconGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(34f, 34f);

            Image image = iconGo.AddComponent<Image>();
            image.color = new Color(0.23f, 0.18f, 0.11f, 1f);

            Outline outline = iconGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.82f, 0.25f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);

            TextMeshProUGUI text = CreateText(iconGo.transform, label, 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.enableAutoSizing = true;
            text.fontSizeMin = 7f;
            text.fontSizeMax = 10f;

            iconGo.AddComponent<HoverTooltip>().SetText(tooltip);
        }

        private void CreateEmptyIcon(Transform parent, string label)
        {
            CreateIcon(parent, "-", label);
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

        private void ClearAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        public static string GetEdictShortLabel(EdictType edict)
        {
            return edict switch
            {
                EdictType.RiceBagsOfHojo => "쌀",
                EdictType.SpiritOfStraightPath => "직",
                EdictType.CleanOfficialsYard => "빈",
                EdictType.ExtremeDecision => "극",
                EdictType.MoonlightSmuggler => "밤",
                EdictType.SpringHungerRelief => "구",
                EdictType.AestheticsOfOverload => "특",
                EdictType.HojoInventoryClearing => "정",
                _ => "교"
            };
        }

        public static string GetEdictLabel(EdictType edict)
        {
            return edict switch
            {
                EdictType.RiceBagsOfHojo => "호조판서의 쌀가마니",
                EdictType.SpiritOfStraightPath => "직진의 기개",
                EdictType.CleanOfficialsYard => "청백리의 텅 빈 마당",
                EdictType.ExtremeDecision => "어전회의의 극단적 결단",
                EdictType.MoonlightSmuggler => "달빛 상단의 밀수품",
                EdictType.SpringHungerRelief => "보릿고개의 구휼",
                EdictType.AestheticsOfOverload => "과적의 미학",
                EdictType.HojoInventoryClearing => "호조의 재고 정리",
                _ => edict.ToString()
            };
        }

        public static string GetEdictTooltip(EdictType edict)
        {
            string description = edict switch
            {
                EdictType.RiceBagsOfHojo => "쌀을 충분히 보유하면 메주와 무명천 판매가가 크게 증가합니다.",
                EdictType.SpiritOfStraightPath => "창고 직전 이동 경로가 직선이면 납품 효율이 오릅니다.",
                EdictType.CleanOfficialsYard => "비어 있는 부지를 유지하면 판매가에 보너스를 받습니다.",
                EdictType.ExtremeDecision => "원자재는 싸지고 가공품은 더 비싸게 팔립니다.",
                EdictType.MoonlightSmuggler => "밤 시간에 판매하는 자원의 가치가 크게 증가합니다.",
                EdictType.SpringHungerRelief => "비어 있는 자원 종류가 많을수록 다른 자원의 가치가 오릅니다.",
                EdictType.AestheticsOfOverload => "특상품 판매 가치가 2배가 됩니다.",
                EdictType.HojoInventoryClearing => "정산 직전 남은 원자재를 보너스 가격으로 자동 판매합니다.",
                _ => "교지 효과입니다."
            };

            return GetEdictLabel(edict) + "\n" + description;
        }

        public static string GetGiftLabel(GiftType giftType)
        {
            return giftType switch
            {
                GiftType.WarehouseFoundation => "물류창고 기반",
                GiftType.WinterCold => "겨울철의 추위",
                GiftType.MidnightTorch => "심야의 횃불",
                GiftType.ExecutionersSword => "망나니의 큰 칼",
                GiftType.PeddlersPack => "보부상의 봇짐",
                GiftType.ScholarsBook => "실학자의 서책",
                GiftType.RoyalInspectorToken => "암행어사의 마패",
                GiftType.CarpentersMasterstroke => "도편수의 묘수",
                GiftType.MagistrateShout => "포도청의 호통",
                _ => giftType.ToString()
            };
        }

        public static string GetGiftDescription(GiftType giftType)
        {
            return giftType switch
            {
                GiftType.WarehouseFoundation => "창고 블록 카드를 즉시 지급합니다.",
                GiftType.WinterCold => "오늘의 밤 시간을 10초 늘립니다.",
                GiftType.MidnightTorch => "이후 매일 밤 시간을 5초 늘립니다.",
                GiftType.ExecutionersSword => "블록 카드에서 원하는 1칸을 제거합니다.",
                GiftType.PeddlersPack => "창고 수납 한도를 일시적으로 늘리는 하사품입니다.",
                GiftType.ScholarsBook => "가장 적게 보유한 자원의 판매 단가를 영구히 올립니다.",
                GiftType.RoyalInspectorToken => "오늘 일꾼 이동 속도를 2배로 올립니다.",
                GiftType.CarpentersMasterstroke => "선택한 타일을 즉시 1레벨 올립니다.",
                GiftType.MagistrateShout => "막힌 일꾼을 회수하고 들고 있던 자원을 수납합니다.",
                _ => "하사품 효과입니다."
            };
        }
    }
}
