using System;
using UnityEngine;
using TMPro;
using FactoryDelivery.Utils;

namespace FactoryDelivery.UI
{
    /// <summary>
    /// 일꾼이 판매소에 납품을 성공하거나 보상을 획득했을 때, 
    /// 월드 공간 상에 "+5 엽전" 등의 수치를 위로 띄워 보내며 
    /// 페이드아웃시키는 오브젝트 풀(Object Pool) 기반 플로팅 텍스트 컴포넌트입니다.
    /// </summary>
    public class FloatingText : MonoBehaviour, IPoolable
    {
        [SerializeField] private TextMeshPro _textMesh;
        [SerializeField] private float _moveSpeed = 1f;
        [SerializeField] private float _duration = 0.5f;

        private float _timer;
        private Color _originalColor;
        private FloatingTextManager _manager;

        /// <summary>
        /// 플로팅 텍스트를 관리하는 매니저를 설정합니다.
        /// </summary>
        public void SetManager(FloatingTextManager mgr)
        {
            _manager = mgr;
        }

        /// <summary>
        /// 텍스트 내용과 색상을 설정하고 활성화합니다. 기본 지속 시간을 사용합니다.
        /// </summary>
        public void Setup(string text, Color color)
        {
            Setup(text, color, _duration);
        }

        /// <summary>
        /// 텍스트 내용, 색상 및 지속 시간을 설정하고 활성화합니다.
        /// </summary>
        public void Setup(string text, Color color, float duration)
        {
            EnsureTextMesh();

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
            float dt = Time.unscaledDeltaTime;
            _timer -= dt;
            
            // 위로 흐르는 연출
            transform.Translate(Vector3.up * _moveSpeed * dt, Space.World);

            // 페이드아웃 연출
            if (_textMesh != null)
            {
                float ratio = Mathf.Clamp01(_timer / _duration);
                Color c = _originalColor;
                c.a = ratio;
                _textMesh.color = c;
            }

            if (_timer <= 0f)
            {
                ReturnToPool();
            }
        }

        /// <summary>
        /// 오브젝트 풀에서 꺼내질 때 호출됩니다.
        /// </summary>
        public void OnSpawnFromPool()
        {
            // 초기화가 필요한 경우 여기에 작성
        }

        /// <summary>
        /// 오브젝트 풀로 돌아갈 때 호출되어 상태를 정리합니다.
        /// </summary>
        public void OnReturnToPool()
        {
            EnsureTextMesh();
            if (_textMesh != null)
            {
                Color hidden = _textMesh.color;
                hidden.a = 0f;
                _textMesh.color = hidden;
                _textMesh.text = string.Empty;
            }

            if (_manager != null)
            {
                _manager.ReturnText(this);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 텍스트 사용이 완료되어 풀로 반환합니다.
        /// </summary>
        private void ReturnToPool()
        {
            OnReturnToPool();
        }

        /// <summary>
        /// TextMeshPro 컴포넌트가 참조되어 있는지 확인하고 없으면 가져옵니다.
        /// </summary>
        private void EnsureTextMesh()
        {
            if (_textMesh != null) return;

            _textMesh = GetComponent<TextMeshPro>();
            if (_textMesh == null)
            {
                _textMesh = GetComponentInChildren<TextMeshPro>(true);
            }
        }
    }
}
