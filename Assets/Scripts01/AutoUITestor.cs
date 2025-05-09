using UnityEngine;
using TMPro; // TextMeshPro UI�� ����ϴ� ���
using System.Collections.Generic;
// using Newtonsoft.Json; // ��� �α뿡 �ʿ��ϴٸ� ��� (SSEObjectReceiver�� �̹� ��� ��)

public class AuthUITester : MonoBehaviour
{
    [Header("References")]
    public SSEObjectReceiver sseReceiver; // �ν����Ϳ��� SSEObjectReceiver ������Ʈ �Ҵ�

    [Header("UI Input Fields (Optional)")]
    public TMP_InputField idInput;
    public TMP_InputField passwordInput;
    public TMP_InputField nicknameInput;
    public TMP_InputField levelInput;
    public TMP_InputField expInput;
    public TMP_InputField userIdToQueryInput; // GetMyUserInfo, UpdateUser, CombinedUserData �� ��� ID

    [Header("UI Output")]
    public TextMeshProUGUI resultText; // ��� ǥ�ÿ�

    void Start()
    {
        if (sseReceiver == null)
        {
            Debug.LogError("[AuthUITester] SSEObjectReceiver is not assigned in the Inspector!");
            this.enabled = false;
            if (resultText != null) resultText.text = "Error: SSEObjectReceiver not assigned.";
        }
    }

    // �Է� �ʵ忡�� �� �������� (������ �⺻�� ���)
    private string GetId() => (idInput != null && !string.IsNullOrEmpty(idInput.text)) ? idInput.text : "defaultUser";
    private string GetPassword() => (passwordInput != null && !string.IsNullOrEmpty(passwordInput.text)) ? passwordInput.text : "defaultUser";
    private string GetNickname() => (nicknameInput != null && !string.IsNullOrEmpty(nicknameInput.text)) ? nicknameInput.text : "defaultUser";
    private int GetLevel(int defaultVal = 1) => (levelInput != null && int.TryParse(levelInput.text, out int l)) ? l : defaultVal;
    private int GetExp(int defaultVal = 0) => (expInput != null && int.TryParse(expInput.text, out int e)) ? e : defaultVal;
    public string GetUserIdToQuery() => (userIdToQueryInput != null && !string.IsNullOrEmpty(userIdToQueryInput.text)) ? userIdToQueryInput.text : GetId();


    private void DisplayResult(string header, object data)
    {
        string message;
        if (data == null)
        {
            message = $"{header}: Failed or no data returned. Check console logs.";
        }
        else if (data is string dataStr)
        {
            message = $"{header}: {dataStr}";
        }
        else if (data is List<UserAuthResponseData> list)
        {
            message = $"{header}: Fetched {list.Count} users.\n";
            int count = 0;
            foreach (var item in list)
            {
                message += item.ToString() + "\n";
                count++;
                if (count >= 3)
                { // �ʹ� ������ ���� 3���� ǥ��
                    message += $"... (and {list.Count - count} more)";
                    break;
                }
            }
        }
        else
        {
            // UserAuthResponseData, UserDataResponse ���� .ToString() ���
            message = $"{header}:\n{data.ToString()}";
        }

        Debug.Log($"[AuthUITester] Result - {header}:\n{message.Replace($"{header}:", "").Trim()}");
        if (resultText != null) resultText.text = message;
    }

    // --- �׽�Ʈ ��ư �ڵ鷯 ---

    public async void OnRegisterButtonClick()
    {
        if (sseReceiver == null) return;
        string id = GetId();
        string pass = GetPassword();
        string nick = GetNickname();
        int lvl = GetLevel();
        int xp = GetExp();
        DisplayResult($"Registering '{id}'", "Processing...");
        UserAuthResponseData response = await sseReceiver.RegisterUserAsync(id, pass, nick, lvl, xp);
        DisplayResult($"Register User '{id}'", response);
    }

    public async void OnLoginButtonClick()
    {
        if (sseReceiver == null) return;
        string id = GetId();
        string pass = GetPassword();
        DisplayResult($"Logging in '{id}'", "Processing...");
        UserAuthResponseData response = await sseReceiver.LoginUserAsync(id, pass);
        DisplayResult($"Login User '{id}'", response);
        if (response != null)
        {
            DisplayResult("Login Info", $"SSE connection will now target: {sseReceiver.sseTargetUserId}. Press 'Start SSE' if not auto-connected.");
            Debug.Log(response);
        }
    }

    public async void OnUpdateUserButtonClick()
    {
        if (sseReceiver == null) return;
        string idToUpdate = WorldSingleton.instance.userData.id;

        string newNick = (nicknameInput != null && !string.IsNullOrEmpty(nicknameInput.text)) ? nicknameInput.text : null;
        int? newLevel = null;
        if (levelInput != null && !string.IsNullOrEmpty(levelInput.text) && int.TryParse(levelInput.text, out int l)) newLevel = l;
        int? newExp = null;
        if (expInput != null && !string.IsNullOrEmpty(expInput.text) && int.TryParse(expInput.text, out int e)) newExp = e;

        DisplayResult($"Updating user '{idToUpdate}'", "Processing...");
        UserAuthResponseData response = await sseReceiver.UpdateUserDataAsync(idToUpdate, newNick, newLevel, newExp);
        DisplayResult($"Update User '{idToUpdate}'", response);
    }

    public async void AddUserExp(PlantLevel plantLevel)
    {
        UserDataResponse user = WorldSingleton.instance.userData;
        string idToUpdate = user.id;

        int exp = 0;

        switch (plantLevel)
        {
            case PlantLevel.Lv1:
                exp = 1;
                break;
            case PlantLevel.Lv2:
                exp = 2;
                break;
            case PlantLevel.Lv3:
                exp = 3;
                break;
            case PlantLevel.Lv4:
                exp = -1;
                break;
        }

        user.exp += exp;
        user.exp = user.exp >= 0 ? user.exp : 0;
        WorldSingleton.instance.levelUpdate();
        
        
        await sseReceiver.UpdateUserDataAsync(idToUpdate, user.nickname, WorldSingleton.instance.userLv, user.exp);
    }

    public async void OnGetMyUserInfoButtonClick()
    {
        if (sseReceiver == null) return;
        string idToFetch = GetUserIdToQuery();
        DisplayResult($"Fetching Auth Info for '{idToFetch}'", "Processing...");
        UserAuthResponseData response = await sseReceiver.GetMyUserInfoAsync(idToFetch);
        DisplayResult($"Get User Auth Info '{idToFetch}'", response);
    }

    public async void OnGetAllUsersButtonClick()
    {
        if (sseReceiver == null) return;
        DisplayResult("Fetching All Users (Auth)", "Processing...");
        List<UserAuthResponseData> response = await sseReceiver.GetAllUsersAsync();
        DisplayResult("Get All Users (Auth)", response);
    }

    public async void OnRequestCombinedDataButtonClick()
    {
        if (sseReceiver == null) return;
        string idToFetch = "KTH";
        DisplayResult($"Fetching Combined Data for '{idToFetch}' (Main Server)", "Processing...");
        UserDataResponse response = await sseReceiver.RequestCombinedUserDataAsync(idToFetch);
        DisplayResult($"Get Combined Data '{idToFetch}' (Main Server)", response);
    }

    public void OnSetSseTargetIdFromInputButtonClick()
    {
        if (sseReceiver == null || userIdToQueryInput == null || string.IsNullOrEmpty(userIdToQueryInput.text))
        {
            string msg = "[AuthUITester] SSE Receiver or UserID Input field is not set, or ID is empty for setting SSE target.";
            Debug.LogError(msg);
            DisplayResult("Set SSE Target ID", msg);
            return;
        }
        sseReceiver.sseTargetUserId = userIdToQueryInput.text; // UserIDToQueryInput�� ���� ���
        string successMsg = $"SSE Target User ID manually set to: {sseReceiver.sseTargetUserId}. Press 'Start SSE for Current User' button to connect/reconnect.";
        Debug.Log($"[AuthUITester] {successMsg}");
        DisplayResult("Set SSE Target ID", successMsg);
    }

    public void OnStartSseForCurrentUserButtonClick()
    {
        if (sseReceiver == null) return;
        DisplayResult($"Starting SSE for '{sseReceiver.sseTargetUserId}'", "Processing...");
        sseReceiver.StartSseConnectionForCurrentUser();
        // SSE ������ �񵿱� ��׶���� �����, ����� SSEObjectReceiver�� �α׸� ���� Ȯ��
        DisplayResult($"Start SSE for '{sseReceiver.sseTargetUserId}'", "SSE connection attempt initiated. Check console for status.");
    }
}