using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    [SerializeField] private float dragSpeed = 60f;
    [SerializeField] private Transform cameraTransform;
    
    [Header("이동 제한")]
    [SerializeField] private bool limitBoundaries = true;
    [SerializeField] private Vector2 minBoundary = new Vector2(-30f, -30f);
    [SerializeField] private Vector2 maxBoundary = new Vector2(30f, 30f);
    
    // 드래그 관련 변수
    private Vector3 dragOrigin;
    private Vector3 dragDifference;
    public bool isDragging = false;
    private Plane groundPlane;
    
    private void Start()
    {
        // 카메라가 설정되지 않았다면 현재 게임오브젝트의 카메라를 사용
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        
        // 지면 평면 초기화 (Y=0 기준)
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        // 디버깅용: 화면 크기와 카메라 설정 로깅
        Debug.Log("Screen dimensions: " + Screen.width + "x" + Screen.height);
        Debug.Log("Camera rect: " + Camera.main.rect);
    }

    // 이동 방향 벡터를 저장할 변수
    private Vector3 moveDirection = Vector3.zero;

    private void Update()
    {
        // 입력 처리는 Update에서 진행
        HandleDragInput();
    }
    
    private void FixedUpdate()
    {
        // 실제 카메라 이동은 FixedUpdate에서 처리
        if (moveDirection != Vector3.zero)
        {
            MoveCamera(moveDirection);
            
            // 이동 후 방향 벡터 초기화
            moveDirection = Vector3.zero;
        }
    }

    // 마우스 위치가 화면 내에 있는지 확인하는 함수
    private bool IsMouseWithinScreen()
    {
        Vector3 mousePos = Input.mousePosition;
        bool isWithin = (mousePos.x >= 0 && mousePos.x <= Screen.width && 
                         mousePos.y >= 0 && mousePos.y <= Screen.height);
        
        // 화면 밖일 경우 로그 출력 (디버깅용)
        if (!isWithin)
        {
            Debug.LogWarning("Mouse outside screen: " + mousePos + 
                            " Screen: " + Screen.width + "x" + Screen.height);
        }
        
        return isWithin;
    }
    
    // 안전한 마우스 위치를 반환하는 함수
    private Vector3 GetSafeMousePosition()
    {
        return new Vector3(
            Mathf.Clamp(Input.mousePosition.x, 0, Screen.width),
            Mathf.Clamp(Input.mousePosition.y, 0, Screen.height),
            Input.mousePosition.z
        );
    }

    private void HandleDragInput()
    {
        // 마우스 버튼을 누를 때 드래그 시작점 설정
        if (Input.GetMouseButtonDown(1) && IsMouseWithinScreen())
        {
            isDragging = true;
            Vector3 safeMousePos = GetSafeMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(safeMousePos);
            float entry;
            
            // 광선이 평면과 교차하는지 확인
            if (groundPlane.Raycast(ray, out entry))
            {
                // 광선과 평면의 교차점을 드래그 시작점으로 설정
                dragOrigin = ray.GetPoint(entry);
            }
        }
        
        if (Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }
        
        if (isDragging && Input.GetMouseButton(1) && IsMouseWithinScreen())
        {
            Vector3 safeMousePos = GetSafeMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(safeMousePos);
            float entry;
            
            if (groundPlane.Raycast(ray, out entry))
            {
                Vector3 dragCurrentPosition = ray.GetPoint(entry);
                
                // 이전 위치와 현재 위치가 너무 멀리 떨어져 있으면 보정 (텔레포트 방지)
                float distance = Vector3.Distance(dragCurrentPosition, dragOrigin);
                if (distance > 50f) // 적절한 값으로 조정
                {
                    Debug.LogWarning("거리가 너무 멀어 드래그 무시: " + distance);
                    return;
                }
                
                dragDifference = dragCurrentPosition - dragOrigin;
                
                moveDirection = new Vector3(-dragDifference.x, 0, -dragDifference.z);
                
                dragOrigin = dragCurrentPosition;
            }
        }
    }
    
    private void MoveCamera(Vector3 direction)
    {
        // 이동 방향의 크기가 비정상적으로 크면 보정
        float magnitude = direction.magnitude;
        if (magnitude > 10f) // 적절한 값으로 조정
        {
            Debug.LogWarning("비정상적인 이동 벡터 감지: " + magnitude);
            direction = direction.normalized * 10f;
        }
        
        Vector3 newPosition = cameraTransform.position + direction * dragSpeed * Time.fixedDeltaTime;
        
        if (limitBoundaries)
        {
            newPosition.x = Mathf.Clamp(newPosition.x, minBoundary.x, maxBoundary.x);
            newPosition.z = Mathf.Clamp(newPosition.z, minBoundary.y, maxBoundary.y);
        }
        
        newPosition.y = cameraTransform.position.y;
        
        // 이전 위치와 새 위치의 차이가 너무 크면 보정
        float positionDelta = Vector3.Distance(newPosition, cameraTransform.position);
        if (positionDelta > 5f) // 적절한 값으로 조정
        {
            Debug.LogWarning("비정상적인 위치 변화 감지: " + positionDelta);
            Vector3 direction_normalized = (newPosition - cameraTransform.position).normalized;
            newPosition = cameraTransform.position + direction_normalized * 5f;
        }
        
        cameraTransform.position = newPosition;
    }
}