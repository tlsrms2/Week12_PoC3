using System;
using UnityEngine;
using TMPro;
using FactoryDelivery.Utils;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 배달부가 판매소에 납품을 성공했을 때, 월드 공간 상에 "+25원 엽전" 등의 수치를 
    /// 위로 퐁퐁 띄워 보내며 페이드아웃시키는 풀링(Object Pool) 기반 플로팅 텍스트 컴포넌트.
    /// </summary>
    public class FloatingText : MonoBehaviour, IPoolable
    {
        [SerializeField] private TextMeshPro _textMesh;
        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private float _duration = 1.0f;

        private float _timer;
        private Color _originalColor;
        private FloatingTextManager _manager;

        public void SetManager(FloatingTextManager mgr)
        {
            _manager = mgr;
        }

        public void Setup(string text, Color color)
        {
            Setup(text, color, _duration);
        }

        public void Setup(string text, Color color, float duration)
        {
            if (_textMesh == null)
                _textMesh = GetComponent<TextMeshPro>();

            if (_textMesh != null)
            {
                _textMesh.text = text;
                _textMesh.color = color;
                _originalColor = color;
            }

            _duration = Mathf.Max(0.1f, duration);
            _timer = _duration;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            
            // 위로 흐르는 연출
            transform.Translate(Vector3.up * _moveSpeed * Time.deltaTime, Space.World);

            // 페이드아웃 연출
            if (_textMesh != null)
            {
                float ratio = _timer / _duration;
                Color c = _originalColor;
                c.a = ratio;
                _textMesh.color = c;
            }

            if (_timer <= 0f)
            {
                ReturnToPool();
            }
        }

        public void OnSpawnFromPool()
        {
            // 초기화
        }

        public void OnReturnToPool()
        {
            gameObject.SetActive(false);
            if (_manager != null)
            {
                _manager.ReturnText(this);
            }
        }

        private void ReturnToPool()
        {
            OnReturnToPool();
        }
    }
}
