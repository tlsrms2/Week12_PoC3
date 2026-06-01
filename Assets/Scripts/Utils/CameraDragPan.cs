using UnityEngine;

namespace FactoryDelivery.Utils
{
    /// <summary>
    /// 마우스 우클릭 드래그를 이용해 카메라를 부드럽게 이동(Panning)시키고, 
    /// 마우스 휠을 이용해 크기(Zoom)를 조절하는 프리미엄 카메라 컨트롤러.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraDragPan : MonoBehaviour
    {
        [Header("줌 설정")]
        [Tooltip("최소 줌 크기 (가까이)")]
        [SerializeField] private float minZoom = 4f;

        [Tooltip("최대 줌 크기 (멀리)")]
        [SerializeField] private float maxZoom = 40f;

        [Tooltip("줌 속도 감도")]
        [SerializeField] private float zoomSensitivity = 10f;

        private Camera _camera;
        private Vector3 _dragOrigin;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            // 최대 줌 제한은 현재 카메라 크기에 맞게 동적으로 늘려줍니다 (맵 크기에 대응)
            maxZoom = Mathf.Max(maxZoom, _camera.orthographicSize * 2f);
        }

        private void Update()
        {
            HandlePanning();
            HandleZoom();
        }

        /// <summary>
        /// 마우스 우클릭을 이용한 월드 스페이스 드래그 Panning 기능.
        /// </summary>
        private void HandlePanning()
        {
            if (Input.GetMouseButtonDown(1))
            {
                // 마우스 우클릭 다운 시점의 마우스 월드 위치를 드래그 기준점으로 삼음
                _dragOrigin = _camera.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButton(1))
            {
                // 현재 마우스 위치와 드래그 기준점 간의 월드 격차를 계산
                Vector3 currentPos = _camera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 difference = _dragOrigin - currentPos;

                // 카메라 Z축 깊이는 유지하면서 카메라 위치를 월드 격차만큼 반대로 밀어줌 (Sticky Drag 효과)
                Vector3 targetPos = transform.position + difference;
                targetPos.z = transform.position.z;

                transform.position = targetPos;
            }
        }

        /// <summary>
        /// 마우스 휠을 이용한 정교한 줌 인 / 줌 아웃 기능.
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newSize = _camera.orthographicSize - scroll * zoomSensitivity;
                _camera.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
        }
    }
}
