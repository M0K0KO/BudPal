// SimpleRegistrationManager.cs

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text; // JSON ���� ó���� ���� �ʿ��� �� ����
using System; // Action �ݹ��� ���� �߰�
using UnityEngine.UI;

// FastAPI �����κ��� �޴� ������ ���� ������ ����
[System.Serializable]
public class UserRegistrationResponse // �� Ŭ������ ���� ����
{
    public string id;
    public string nickname;
    public string profile_picture_url;
}

public class SimpleRegistrationManager : MonoBehaviour
{
    private string serverUrl = "http://127.0.0.1:8001"; // ���� ���� �ּҷ� ����
    public Text statusText; // 결과 메시지를 표시할 UI Text 요소
    public Button registerButton; // 회원가입 테스트를 시작할 버튼

    /// <summary>
    /// ����� ����� �����ϴ� ���� �޼ҵ� (������ ���� ����).
    /// �Ϸ� �� ȣ��� �ݹ��� �޽��ϴ�.
    /// </summary>
    /// <param name="userId">����� ID</param>
    /// <param name="password">��й�ȣ</param>
    /// <param name="nickname">�г���</param>
    /// <param name="onRegistrationComplete">�ݹ� �Լ� (��� �޽���, ���� ����)</param>
    public void RegisterUser(string userId, string password, string nickname, Action<string, bool> onRegistrationComplete)
    {
        StartCoroutine(RegisterUserCoroutine(userId, password, nickname, onRegistrationComplete));
    }

    private IEnumerator RegisterUserCoroutine(string userId, string password, string nickname, Action<string, bool> onRegistrationComplete)
    {
        WWWForm form = new WWWForm();
        form.AddField("id", userId);
        form.AddField("password", password);
        form.AddField("nickname", nickname);

        string registerUrl = serverUrl + "/register/";
        Debug.Log($"ȸ������ ��û URL: {registerUrl} | ID: {userId}, Nickname: {nickname}");

        using (UnityWebRequest www = UnityWebRequest.Post(registerUrl, form))
        {
            yield return www.SendWebRequest();

            string message;
            bool isError;

            if (www.result == UnityWebRequest.Result.ConnectionError ||
                www.result == UnityWebRequest.Result.ProtocolError ||
                www.result == UnityWebRequest.Result.DataProcessingError)
            {
                message = $"ȸ������ ����: {www.error}\n���� �ڵ�: {www.responseCode}\n";
                if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    message += $"���� ����: {www.downloadHandler.text}";
                }
                Debug.LogError(message);
                isError = true;
            }
            else
            {
                string serverResponseText = (www.downloadHandler != null) ? www.downloadHandler.text : "No response text";
                message = $"ȸ������ ����!\n���� �ڵ�: {www.responseCode}\n���� ����: {serverResponseText}";
                Debug.Log(message);
                isError = false;

                try
                {
                    UserRegistrationResponse responseData = JsonUtility.FromJson<UserRegistrationResponse>(serverResponseText);
                    // ���� �� �޽����� �߰� ���� ���� ����
                    message = $"��� ����! ID: {responseData.id}, Nickname: {responseData.nickname}";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"JSON ���� �Ľ� ����: {ex.Message}. ���� ����: {serverResponseText}");
                    // �Ľ� �����ص� �⺻���� ���� �޽����� ������ �� ����
                    message = $"ȸ������ ��û�� ���������� ���� �Ľ� �� ���� �߻�.\n���� ����: {serverResponseText}";
                    // isError�� true�� ���� ���δ� ��å�� ���� ���� (���⼭�� false ����)
                }
            }
            onRegistrationComplete?.Invoke(message, isError); // �ݹ� ȣ��
        }
    }

    public void HandleRegistrationResult(string message, bool isError)
    {
        if (statusText != null)
        {
            statusText.text = message; // 콜백에서 받은 메시지 표시
            if (isError)
            {
                statusText.color = Color.red;
            }
            else
            {
                statusText.color = Color.green; // 또는 기본색상 (예: Color.black)
            }
        }

        if (registerButton != null)
        {
            registerButton.interactable = true; // 버튼 다시 활성화
        }
    }

    
}