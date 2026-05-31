using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using FactoryDelivery.Events;
using FactoryDelivery.Utils;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 플로팅 텍스트들을 관리하고 이벤트를 구독하여 월드 상에 띄워주는 매니저 클래스.
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        [Header("Event Channels")]
        [Tooltip("자원이 판매되어 수입이 발생할 때 수신하는 채널")]
        [SerializeField] private IntEventChannelSO _onResourceSoldChannel;

        [Header("Floating Text Setup")]
        [SerializeField] private FloatingText _textPrefab;
        [SerializeField] private Color _goldColor = new Color(1.0f, 0.84f, 0f); // 조선 황실 엽전 골드 컬러

        private readonly Queue<FloatingText> _pool = new Queue<FloatingText>();

        private void OnEnable()
        {
            if (_onResourceSoldChannel != null)
            {
                _onResourceSoldChannel.OnEventRaised += SpawnSoldText;
            }
        }

        private void OnDisable()
        {
            if (_onResourceSoldChannel != null)
            {
                _onResourceSoldChannel.OnEventRaised -= SpawnSoldText;
            }
        }

        /// <summary>
        /// 판매가 발생했을 때 물류 창고 또는 맵의 중심 위치 근처에서 숫자를 스폰한다.
        /// </summary>
        private void SpawnSoldText(int value)
        {
            // 씬의 물류 창고 또는 배달 완료 지점의 위치를 찾아서 스폰
            // 만약 찾지 못하면 맵의 중심 근처인 (15, 15, 0)에서 스폰 폴백
            Vector3 spawnPos = new Vector3(15f, 15f, -0.5f); // 2D 씬 오더링을 위해 Z축 약간 앞으로

            var warehouse = FindFirstObjectByType<FactoryDelivery.Facility.Warehouse>();
            if (warehouse != null)
            {
                spawnPos = warehouse.transform.position;
                spawnPos.z = -0.5f; // 타일 비주얼보다 앞에 오도록 설정
                
                // 마구 겹쳐서 나오지 않도록 약간의 무작위 오프셋 제공
                spawnPos.x += Random.Range(-0.3f, 0.3f);
                spawnPos.y += Random.Range(-0.3f, 0.3f);
            }

            FloatingText ft = GetText();
            ft.transform.position = spawnPos;
            ft.Setup($"+{value} 엽전", _goldColor);
        }

        private FloatingText GetText()
        {
            if (_pool.Count > 0)
            {
                FloatingText ft = _pool.Dequeue();
                ft.OnSpawnFromPool();
                return ft;
            }

            if (_textPrefab == null)
            {
                // 프리팹 누락 시 동적 생성 폴백
                GameObject go = new GameObject("DynamicFloatingText");
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 4;
                
                FloatingText ft = go.AddComponent<FloatingText>();
                ft.SetManager(this);
                return ft;
            }

            FloatingText newFt = Instantiate(_textPrefab, transform);
            newFt.SetManager(this);
            return newFt;
        }

        /// <summary>
        /// 지정된 월드 좌표에 커스텀 텍스트와 색상의 플로팅 텍스트를 띄웁니다.
        /// </summary>
        public void ShowText(Vector3 position, string text, Color color)
        {
            FloatingText ft = GetText();
            ft.transform.position = position;
            ft.Setup(text, color);
        }

        public void ShowText(Vector3 position, string text, Color color, float duration)
        {
            FloatingText ft = GetText();
            ft.transform.position = position;
            ft.Setup(text, color, duration);
        }

        public void ReturnText(FloatingText text)
        {
            if (!_pool.Contains(text))
            {
                _pool.Enqueue(text);
            }
        }
    }
}
