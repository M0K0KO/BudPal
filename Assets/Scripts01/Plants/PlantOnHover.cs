using System;
using DG.Tweening;
using UnityEngine;

public class PlantOnHover : MonoBehaviour
{
    private Plant plant;
    private bool isHovering = false;
    private bool isDetailViewActive = false;

    private Camera camera;
    private Vector3 prevPos;
    private float prevOrthoSize;
    
    private void Awake()
    {
        plant = GetComponent<Plant>();
        camera = Camera.main;
    }

    private void Update()
    {
        // 마우스가 화면 내에 있고, 호버링 중이며, 상세 뷰가 비활성화 상태일 때만 클릭 처리
        if (Input.GetMouseButtonDown(0) && isHovering && !isDetailViewActive && IsMouseWithinScreen())
        {
            isDetailViewActive = true;
            
            // 카메라 컨트롤러의 드래그 상태 초기화
            CameraController cameraController = camera.GetComponent<CameraController>();
            if (cameraController != null)
            {
                cameraController.isDragging = false;
            }
            
            // 현재 카메라 상태 저장
            prevPos = camera.transform.position;
            prevOrthoSize = camera.orthographicSize;
            
            // 안전한 트윈 애니메이션 실행
            try
            {
                // 카메라 줌 애니메이션
                camera.DOOrthoSize(1.4f, 0.5f)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() => Debug.Log("Camera zoom completed"));
                
                // 카메라 이동 애니메이션
                Vector3 targetPos = new Vector3(transform.position.x - 5, transform.position.y + 5, transform.position.z - 5);
                Vector3 offSet = new Vector3(0.6f, 1.3f, -1.3f);
                targetPos += offSet;
                
                camera.transform.DOMove(targetPos, 0.5f)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() => Debug.Log("Camera move completed"));

                // UI 창 활성화 및 애니메이션
                if (WorldSingleton.instance != null && WorldSingleton.instance.plantDetailWindow != null)
                {
                    PlantDetailWindow plantDetailWindow = WorldSingleton.instance.plantDetailWindow.GetComponent<PlantDetailWindow>();
                    plantDetailWindow.plantName.text = plant.plantInfo.plantType.ToString();
                    plantDetailWindow.plantRank.text = plant.plantInfo.rank.ToString();
                    plantDetailWindow.plantStatus.text = plant.plantInfo.PlantStatusByRank();
                    plantDetailWindow.plantImage.sprite = plant.plantInfo.PlantImageByInfo();
                    
                    /////////////////////////////////////////////////// 바꿔야함
                    plantDetailWindow.plantDescription.text = plant.plantInfo.PlantStatusByRank();
                    /////////////////////////////////////////////////// 바꿔야함
                    
                    WorldSingleton.instance.plantDetailWindow.SetActive(true);
                    WorldSingleton.instance.plantDetailWindow.GetComponent<RectTransform>()
                        .DOAnchorPosX(-565, 0.5f)
                        .SetEase(Ease.OutCubic)
                        .OnComplete(() => Debug.Log("UI animation completed"));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Animation error: " + e.Message);
                ResetDetailView(); // 오류 발생 시 상태 초기화
            }
        }

        // Escape 키로 상세 뷰 닫기
        if (Input.GetKeyDown(KeyCode.Escape) && isDetailViewActive)
        {
            CloseDetailView();
        }
    }

    // 상세 뷰 닫기 함수 (재사용을 위해 분리)
    private void CloseDetailView()
    {
        // 이미 닫는 중이라면 중복 실행 방지
        if (!isDetailViewActive) return;
        
        try
        {
            // 카메라 컨트롤러의 드래그 상태 초기화
            CameraController cameraController = camera.GetComponent<CameraController>();
            if (cameraController != null)
            {
                cameraController.isDragging = false;
            }
            
            // 카메라 원래 상태로 복원
            camera.DOOrthoSize(prevOrthoSize, 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => Debug.Log("Camera zoom reset completed"));
                
            camera.transform.DOMove(prevPos, 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => Debug.Log("Camera move reset completed"));

            // UI 창 닫기 애니메이션
            if (WorldSingleton.instance != null && WorldSingleton.instance.plantDetailWindow != null)
            {
                WorldSingleton.instance.plantDetailWindow.GetComponent<RectTransform>()
                    .DOAnchorPosX(500, 0.5f)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() => {
                        WorldSingleton.instance.plantDetailWindow.SetActive(false);
                        isDetailViewActive = false; // 상세 뷰 상태 업데이트
                        Debug.Log("Detail view closed");
                    });
            }
            else
            {
                isDetailViewActive = false; // UI가 없더라도 상태 업데이트
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Animation reset error: " + e.Message);
            ResetDetailView(); // 오류 발생 시 강제 초기화
        }
    }
    
    // 강제 초기화 (오류 발생 시)
    private void ResetDetailView()
    {
        try
        {
            // 카메라 설정 즉시 복원
            camera.orthographicSize = prevOrthoSize;
            camera.transform.position = prevPos;
            
            // UI 상태 초기화
            if (WorldSingleton.instance != null && WorldSingleton.instance.plantDetailWindow != null)
            {
                WorldSingleton.instance.plantDetailWindow.SetActive(false);
            }
            
            // 상태 변수 초기화
            isDetailViewActive = false;
            
            // 모든 DOTween 애니메이션 중지 (이 오브젝트와 관련된 것만)
            DOTween.Kill(camera);
            if (WorldSingleton.instance != null && WorldSingleton.instance.plantDetailWindow != null)
            {
                DOTween.Kill(WorldSingleton.instance.plantDetailWindow.GetComponent<RectTransform>());
            }
            
            Debug.Log("Detail view forcibly reset");
        }
        catch (Exception e)
        {
            Debug.LogError("Force reset error: " + e.Message);
        }
    }

    // 마우스가 화면 영역 내에 있는지 확인
    private bool IsMouseWithinScreen()
    {
        Vector3 mousePos = Input.mousePosition;
        bool isWithin = (mousePos.x >= 0 && mousePos.x <= Screen.width && 
                         mousePos.y >= 0 && mousePos.y <= Screen.height);
        
        if (!isWithin)
        {
            Debug.LogWarning("Mouse outside screen in PlantOnHover: " + mousePos);
        }
        
        return isWithin;
    }

    private void OnMouseEnter()
    {
        // 마우스가 화면 내에 있을 때만 호버링 처리
        if (IsMouseWithinScreen())
        {
            isHovering = true;
            if (plant != null && plant.plantController != null && plant.plantController.currentOutline != null)
            {
                plant.plantController.currentOutline.enabled = true;
            }
        }
    }
    
    private void OnMouseExit()
    {
        isHovering = false;
        if (plant != null && plant.plantController != null && plant.plantController.currentOutline != null)
        {
            plant.plantController.currentOutline.enabled = false;
        }
    }
    
    // 가능한 에러 확인을 위한 OnDisable
    private void OnDisable()
    {
        // 비활성화될 때 상세 뷰가 열려있으면 닫기
        if (isDetailViewActive)
        {
            ResetDetailView();
        }
    }
}