using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public static UIController instance;
    
    public Canvas canvas;
    
    public SimpleRegistrationManager registrationManager;

    public AudioSource audioSource;
    public AudioClip buttonSound;

    public GameObject loginPanel;
    public GameObject loginFrame;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;

    public GameObject signUpPanel;
    public GameObject signUpFrame;
    public TMP_InputField signUpNicknameInput;
    public TMP_InputField signUpUsernameInput;
    public TMP_InputField signUpPasswordInput;

    public GameObject mainPanel;

    public GameObject plantInfoPanel;
    public float prevOrthoSize;
    public Vector3 prevCamPos;

    public GameObject shopPanel;



    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);
        
        plantInfoPanel.SetActive(false);
        canvas = GetComponent<Canvas>();
        loginPanel.SetActive(true);
        mainPanel.SetActive(false);
        signUpPanel.SetActive(false);
        signUpPanel.GetComponent<CanvasGroup>().alpha = 0;
        shopPanel.SetActive(false);
    }


    public void OnLoginClick()
    {
        audioSource.PlayOneShot(buttonSound);
        mainPanel.SetActive(true);

        loginFrame.GetComponent<RectTransform>().DOScale(1.4f, 0.3f);
        loginPanel.GetComponent<CanvasGroup>().DOFade(0, 0.4f).OnComplete(() => loginPanel.SetActive(false));

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

    public void OnSignUpCompleteClick()
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

        registrationManager.RegisterUser(signUpUsernameInput.text, signUpPasswordInput.text,
            signUpNicknameInput.text, registrationManager.HandleRegistrationResult);

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
        shopPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        shopPanel.GetComponent<RectTransform>().anchoredPosition += new Vector2(canvas.GetComponent<RectTransform>().rect.width, 0);
        shopPanel.SetActive(true);
        shopPanel.GetComponent<RectTransform>().DOAnchorPosX(0, 0.4f, true).SetEase(Ease.OutSine).OnComplete(()=>shopPanel.GetComponent<CanvasGroup>().blocksRaycasts = true);
        
    }

    public void OnShopExitClick()
    {
        audioSource.PlayOneShot(buttonSound);
        shopPanel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        Vector2 targetPos = shopPanel.GetComponent<RectTransform>().anchoredPosition + new Vector2(canvas.GetComponent<RectTransform>().rect.width, 0);
        shopPanel.GetComponent<RectTransform>().DOAnchorPosX(targetPos.x, 0.4f, true).SetEase(Ease.OutSine).OnComplete(() =>
        {
            shopPanel.SetActive(false);
            plantInfoPanel.GetComponent<CanvasGroup>().blocksRaycasts = true;
        });
    }

    public void OnPlantInfoExitClick()
    {
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
            
            Debug.Log("Detail view forcibly reset");
        }
        catch (Exception e)
        {
            Debug.LogError("Force reset error: " + e.Message);
        }
    }




}
