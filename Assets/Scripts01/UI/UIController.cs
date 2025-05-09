using System;
using System.Collections;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public static UIController instance;
    
    public Canvas canvas;
    
    public AudioSource audioSource;
    public AudioClip buttonSound;

    public GameObject loginPanel;
    public GameObject loginFrame;
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;

    public GameObject signUpPanel;
    public GameObject signUpFrame;
    public TMP_InputField signUpNicknameInput;
    public TMP_InputField signUpUsernameInput;
    public TMP_InputField signUpPasswordInput;

    public GameObject mainPanel;

    public GameObject plantInfoPanel;
    public float prevOrthoSize;
    public Vector3 prevCamPos;

    public GameObject visitPanel;
    
    public SSEObjectReceiver sseReceiver;

    public GameObject plantReadyIconPrefab;
    public GameObject plantDeadIconPrefab;

    public Sprite firstLevelItem;
    public Sprite secondLevelItem;
    public Sprite thirdLevelItem;
    public Sprite fourthLevelItem;

    public GameObject mainShopPanel;

    public GameObject visitButton;
    public GameObject returnButton;


    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);
        
        visitPanel.SetActive(false);
        plantInfoPanel.SetActive(false);
        canvas = GetComponent<Canvas>();
        loginPanel.SetActive(true);
        mainPanel.SetActive(false);
        signUpPanel.SetActive(false);
        signUpPanel.GetComponent<CanvasGroup>().alpha = 0;
        sseReceiver = FindFirstObjectByType<SSEObjectReceiver>();
    }


    public async void OnLoginClick()
    {
        audioSource.PlayOneShot(buttonSound);

        UserAuthResponseData response = await sseReceiver.LoginUserAsync(loginUsernameInput.text, password: loginPasswordInput.text);

        if (response != null)
        {
            mainPanel.GetComponent<CanvasGroup>().alpha = 0;
            mainPanel.SetActive(true);
            mainPanel.GetComponent<CanvasGroup>().DOFade(1, 0.5f);

            loginFrame.GetComponent<RectTransform>().DOScale(1.4f, 0.3f);
            loginPanel.GetComponent<CanvasGroup>().DOFade(0, 0.4f).OnComplete(() => loginPanel.SetActive(false));
        }

        
        UserDataResponse r = await sseReceiver.RequestCombinedUserDataAsync(loginUsernameInput.text);
        sseReceiver.UpdateDetectionInfoInUnity(r.detection_data);

        sseReceiver.GetAllUsersAsync();
    }

    private IEnumerator IEwait()
    {
        yield return new WaitForSeconds(1f);
    }

    public void OnSignUpClick()
    {
        audioSource.PlayOneShot(buttonSound);
        loginFrame.GetComponent<CanvasGroup>().blocksRaycasts = false;
        loginFrame.GetComponent<CanvasGroup>().DOFade(0, 0.4f).OnComplete((() =>
        {
            loginFrame.GetComponent<CanvasGroup>().blocksRaycasts = true;
        }));
        signUpPanel.SetActive(true);
        signUpPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        signUpPanel.GetComponent<CanvasGroup>().DOFade(1, 0.4f).OnComplete((() =>
        {
            signUpPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
        }));

    }

    public async void OnSignUpCompleteClick()
    {
        audioSource.PlayOneShot(buttonSound);

        UserAuthResponseData response = await sseReceiver.RegisterUserAsync(
            signUpUsernameInput.text, signUpPasswordInput.text, signUpNicknameInput.text);
        
        
        loginFrame.SetActive(true);
        loginFrame.GetComponent<CanvasGroup>().alpha = 1;
        loginFrame.GetComponent<CanvasGroup>().blocksRaycasts = false;
        signUpPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        signUpPanel.GetComponent<CanvasGroup>().DOFade(0, 0.4f).OnComplete((() =>
        {
            loginFrame.GetComponent<CanvasGroup>().blocksRaycasts = true;
            signUpPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
            signUpPanel.SetActive(false);
        }));
        
        

    }

    public void OnSignUpExitClick()
    {
        audioSource.PlayOneShot(buttonSound);
        loginFrame.SetActive(true);
        loginFrame.GetComponent<CanvasGroup>().alpha = 1;
        loginFrame.GetComponent<CanvasGroup>().blocksRaycasts = false;
        signUpPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        signUpPanel.GetComponent<CanvasGroup>().DOFade(0, 0.4f).OnComplete((() =>
        {
            loginFrame.GetComponent<CanvasGroup>().blocksRaycasts = true;
            signUpPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
            signUpPanel.SetActive(false);
        }));

    }

    public void OnPlantStatusShopClick()
    {
        audioSource.PlayOneShot(buttonSound);
        plantInfoPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        
        
    }

    public void OnShopExitClick()
    {
        audioSource.PlayOneShot(buttonSound);
        
    }

    public void OnPlantInfoExitClick()
    {
        audioSource.PlayOneShot(buttonSound);
        Camera cam = Camera.main;
        try
        {
            cam.DOOrthoSize(prevOrthoSize, 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => Debug.Log("Camera zoom reset completed"));
                
            cam.transform.DOMove(prevCamPos, 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => Debug.Log("Camera move reset completed"));
            
            if (WorldSingleton.instance != null && WorldSingleton.instance.plantDetailWindow != null)
            {
                WorldSingleton.instance.plantDetailWindow.SetActive(false);
            }

            GetComponent<CanvasGroup>().blocksRaycasts = true;
            
            Debug.Log("Detail view forcibly reset");
        }
        catch (Exception e)
        {
            Debug.LogError("Force reset error: " + e.Message);
        }
    }

    public void OnVisitClick()
    {
        Debug.Log("ONVISITCLICK");
        
        audioSource.PlayOneShot(buttonSound);
        mainPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        visitPanel.GetComponent<CanvasGroup>().alpha = 0f;
        visitPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        visitPanel.SetActive(true);
        visitPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.5f).OnComplete(() =>
        {
            visitPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
        });
    }

    public void OnVisitExitClick()
    {
        audioSource.PlayOneShot(buttonSound);
        visitPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        visitPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.5f).OnComplete(() =>
        {
            visitPanel.SetActive(false);
            mainPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
        });
    }

    public void OnMainShopClick()
    {
        audioSource.PlayOneShot(buttonSound);
        mainPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        mainShopPanel.GetComponent<CanvasGroup>().alpha = 0f;
        mainShopPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        mainShopPanel.SetActive(true);
        mainShopPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.5f).OnComplete(() =>
        {
            mainShopPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
        });
    }

    public void OnMainShopExitClick()
    {
        audioSource.PlayOneShot(buttonSound);
        mainShopPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        mainShopPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.5f).OnComplete(() =>
        {
            mainShopPanel.SetActive(false);
            mainShopPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
        });
    }




}
