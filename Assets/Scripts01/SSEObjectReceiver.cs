using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json; // Newtonsoft.Json ��Ű�� �ʿ�
using System.Net.Http;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
// using UnityEngine.PlayerLoop; // ���ʿ��Ͽ� ����

// ������ UserBase, UserResponse �𵨿� �ش� (id, nickname, level, exp)
[System.Serializable]
public class UserAuthResponseData
{
    public string id;
    public string nickname;
    public int level;
    public int exp;

    public override string ToString()
    {
        return $"ID: {id}, Nickname: {nickname}, Level: {level}, Exp: {exp}";
    }
}

// ������ NewDetectedObjectInfo �𵨿� �ش�
[System.Serializable]
public class NewDetectedObjectInfo
{
    public int sector_row;
    public int sector_col;
    [JsonProperty("Lv")]
    public string Level;
    [JsonProperty("type")]
    public string ObjectType;
}

// FastAPI ������ UserDataResponse �� (YOLO ������ /user_data/ �����)
[System.Serializable]
public class UserDataResponse // Combined data from main server's /user_data/
{
    public string id;
    public string nickname;
    public int? level; // ���� ���信 ���� Nullable ó��
    public int? exp;   // ���� ���信 ���� Nullable ó��
    public Dictionary<string, List<NewDetectedObjectInfo>> detection_data;

    public override string ToString()
    {
        return $"ID: {id}, Nickname: {nickname}, Level: {level?.ToString() ?? "N/A"}, Exp: {exp?.ToString() ?? "N/A"}, HasDetectionData: {detection_data != null && detection_data.Count > 0}";
    }
}


public class SSEObjectReceiver : MonoBehaviour
{
    [Header("Server Base Addresses")]
    public string mainServerBaseAddress = "http://localhost:8000"; // SSE �� /user_data/ ��������Ʈ �� (YOLO ����)
    public string authServerBaseAddress = "http://localhost:8001"; // ���� ���� ��������Ʈ �� (Auth ����)

    [Header("Endpoint Paths (Main Server - Port 8000)")]
    public string sseStreamPath = "/detection_stream";
    public string mainServerUserDataPath = "/user_data/";

    [Header("Auth Endpoint Paths (Auth Server - Port 8001)")]
    public string registerPath = "/register/";
    public string loginPath = "/login/";
    public string userUpdatePath = "/users/update/";
    public string userGetMePath = "/users/me/";
    public string allUsersPath = "/users/all/";

    [Header("SSE Connection Settings")]
    public float reconnectDelaySeconds = 5.0f;
    [Tooltip("This ID is used for establishing the SSE connection. Set it after successful login or from game settings/PlayerPrefs.")]
    public string sseTargetUserId = "KTH"; // SSE ���� ��� ����� ID


    private HttpClient httpClient;
    private CancellationTokenSource sseCancellationTokenSource;
    private bool isTryingToConnectSse = false;

    private List<UserAuthResponseData> allUsers;
    private UserDataResponse userData;

    private void Awake()
    {
        allUsers = WorldSingleton.instance.allUsers;
        userData = WorldSingleton.instance.userData;
    }

    void Start()
    {
        if (UnityMainThreadDispatcher.Instance() == null)
        {
            Debug.LogError("[SSEObjectReceiver] UnityMainThreadDispatcher instance could not be initialized. SSEObjectReceiver will not function correctly and is now disabled.");
            this.enabled = false;
            return;
        }

        httpClient = new HttpClient();
        httpClient.Timeout = Timeout.InfiniteTimeSpan;

        if (!string.IsNullOrEmpty(this.sseTargetUserId) && this.sseTargetUserId != "defaultUser")
        {
            Debug.Log($"[SSEObjectReceiver] Initializing SSE connection for pre-set user: {this.sseTargetUserId}");
            StartSseConnectionForCurrentUser();
        }
    }

    public void StartSseConnectionForCurrentUser()
    {
        if (string.IsNullOrEmpty(this.sseTargetUserId) || this.sseTargetUserId == "defaultUser")
        {
            Debug.LogError($"[SSEObjectReceiver] Cannot start SSE: sseTargetUserId is not set to a specific user ('{this.sseTargetUserId}'). Please login or set it appropriately.");
            return;
        }
        if (httpClient == null)
        {
            Debug.LogError("[SSEObjectReceiver] HttpClient not initialized. Cannot start SSE connection.");
            return;
        }

        string fullSseUrl = $"{mainServerBaseAddress}{sseStreamPath}/{this.sseTargetUserId}";

        if (sseCancellationTokenSource != null && !sseCancellationTokenSource.IsCancellationRequested)
        {
            Debug.Log($"[SSEObjectReceiver] Cancelling existing SSE connection/attempt before starting new one for user '{this.sseTargetUserId}'.");
            sseCancellationTokenSource.Cancel();
        }
        AttemptSseConnection(fullSseUrl);
    }

    #region SSE Connection Logic
    void AttemptSseConnection(string streamUrl)
    {
        isTryingToConnectSse = true;
        if (sseCancellationTokenSource != null)
        {
            if (!sseCancellationTokenSource.IsCancellationRequested) sseCancellationTokenSource.Cancel();
            sseCancellationTokenSource.Dispose();
        }
        sseCancellationTokenSource = new CancellationTokenSource();

        Debug.Log($"[SSEObjectReceiver] Attempting to connect to SSE stream for user '{this.sseTargetUserId}' at: {streamUrl}");
        Task.Run(() => ConnectToSseStream(streamUrl, sseCancellationTokenSource.Token), sseCancellationTokenSource.Token);
    }

    private async Task ConnectToSseStream(string streamUrl, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, streamUrl);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                Debug.Log($"[SSEObjectReceiver] Successfully connected to SSE stream for user '{this.sseTargetUserId}' at {streamUrl}. Waiting for data...");
                isTryingToConnectSse = false;

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        if (cancellationToken.IsCancellationRequested) { Debug.Log("[SSEObjectReceiver] SSE Stream reading cancelled by token before ReadLine."); break; }
                        string line = await reader.ReadLineAsync();
                        if (cancellationToken.IsCancellationRequested) { Debug.Log("[SSEObjectReceiver] SSE Stream reading cancelled by token after ReadLine."); break; }
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (!line.StartsWith("data:")) continue;

                        string jsonData = line.Substring("data:".Length).Trim();
                        if (string.IsNullOrEmpty(jsonData)) continue;

                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            if (this == null || !Application.isPlaying) return;
                            try
                            {
                                Dictionary<string, List<NewDetectedObjectInfo>> detectionData =
                                    JsonConvert.DeserializeObject<Dictionary<string, List<NewDetectedObjectInfo>>>(jsonData);
                                if (detectionData != null) UpdateDetectionInfoInUnity(detectionData);
                                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized SSE data (Dictionary<string, List<NewDetectedObjectInfo>>) is null. JSON: {jsonData}");
                            }
                            catch (JsonException ex) { Debug.LogError($"[SSEObjectReceiver] SSE JSON Deserialization Error: {ex.Message}\nJSON: {jsonData}"); }
                            catch (Exception ex) { Debug.LogError($"[SSEObjectReceiver] Error processing SSE data on main thread: {ex.Message}\nStackTrace: {ex.StackTrace}"); }
                        });
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            if (!cancellationToken.IsCancellationRequested) Debug.LogError($"[SSEObjectReceiver] SSE Connection Error (HttpRequestException) for URL '{streamUrl}': {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Debug.Log($"[SSEObjectReceiver] SSE stream reading task for user '{this.sseTargetUserId}' at '{streamUrl}' was explicitly canceled or timed out.");
        }
        catch (ObjectDisposedException ode)
        {
            Debug.LogWarning($"[SSEObjectReceiver] SSE Stream operation attempted on a disposed object for URL '{streamUrl}': {ode.Message}. Likely due to shutdown.");
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested) Debug.LogError($"[SSEObjectReceiver] SSE Stream Error for URL '{streamUrl}' (General Exception): {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
        finally
        {
            isTryingToConnectSse = false;
            if (!cancellationToken.IsCancellationRequested && Application.isPlaying &&
                !string.IsNullOrEmpty(this.sseTargetUserId) && this.sseTargetUserId != "defaultUser")
            {
                Debug.Log($"[SSEObjectReceiver] SSE connection for user '{this.sseTargetUserId}' at '{streamUrl}' lost or ended. Attempting to reconnect in {reconnectDelaySeconds} seconds...");
                UnityMainThreadDispatcher.Instance().Enqueue(() => StartCoroutine(DelayedReconnectCoroutine(streamUrl)));
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"[SSEObjectReceiver] SSE Task for user '{this.sseTargetUserId}' at '{streamUrl}' was cancelled by request. No auto-reconnect.");
            }
        }
    }

    System.Collections.IEnumerator DelayedReconnectCoroutine(string streamUrl)
    {
        yield return new WaitForSeconds(reconnectDelaySeconds);
        if (Application.isPlaying && (sseCancellationTokenSource == null || !sseCancellationTokenSource.IsCancellationRequested))
        {
            AttemptSseConnection(streamUrl);
        }
        else
        {
            Debug.Log("[SSEObjectReceiver] Delayed reconnect skipped as cancellation was requested or application is not playing.");
        }
    }
    #endregion

    #region User Data and Auth Client Methods

    public async Task<UserDataResponse> RequestCombinedUserDataAsync(string userIdToRequestDataFor)
    {
        if (httpClient == null) { Debug.LogError("[SSEObjectReceiver] HttpClient not initialized."); return null; }
        if (string.IsNullOrEmpty(userIdToRequestDataFor)) { Debug.LogError("[SSEObjectReceiver] userIdToRequestDataFor cannot be null or empty."); return null; }

        string endpointUrl = $"{mainServerBaseAddress}{mainServerUserDataPath}";
        Debug.Log($"[SSEObjectReceiver] Requesting combined data for user '{userIdToRequestDataFor}' from: {endpointUrl}");

        try
        {
            var formData = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("user_id", userIdToRequestDataFor) };
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var formContent = new FormUrlEncodedContent(formData))
            {
                HttpResponseMessage response = await httpClient.PostAsync(endpointUrl, formContent, cts.Token);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SSEObjectReceiver] HTTP Error {response.StatusCode} for user '{userIdToRequestDataFor}' from {endpointUrl}: {jsonResponse}");
                    return null;
                }

                Debug.Log($"[SSEObjectReceiver] Received combined data for '{userIdToRequestDataFor}'. Deserializing... (JSON sample: {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 200))}...)");
                userData = JsonConvert.DeserializeObject<UserDataResponse>(jsonResponse);

                if (userData != null)
                {
                    Debug.Log($"[SSEObjectReceiver] Successfully fetched combined data: {userData}");
                }
                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized combined data for '{userIdToRequestDataFor}' is null. JSON: {jsonResponse}");
                return userData;
            }
        }
        catch (Exception ex) { HandleRequestException("RequestCombinedUserDataAsync", userIdToRequestDataFor, endpointUrl, ex); return null; }
    }

    public async Task<UserAuthResponseData> RegisterUserAsync(string id, string password, string nickname, int level = 1, int exp = 0)
    {
        if (httpClient == null) { Debug.LogError("[SSEObjectReceiver] HttpClient not initialized."); return null; }

        string endpointUrl = $"{authServerBaseAddress}{registerPath}";
        Debug.Log($"[SSEObjectReceiver] Registering user '{id}' at: {endpointUrl}");

        try
        {
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id", id),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("nickname", nickname),
                new KeyValuePair<string, string>("level", level.ToString()),
                new KeyValuePair<string, string>("exp", exp.ToString())
            };
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var formContent = new FormUrlEncodedContent(formData))
            {
                HttpResponseMessage response = await httpClient.PostAsync(endpointUrl, formContent, cts.Token);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SSEObjectReceiver] HTTP Error {response.StatusCode} during registration for '{id}': {jsonResponse}");
                    return null;
                }

                UserAuthResponseData responseData = JsonConvert.DeserializeObject<UserAuthResponseData>(jsonResponse);
                if (responseData != null) Debug.Log($"[SSEObjectReceiver] User '{id}' registered successfully: {responseData}");
                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized registration response for '{id}' is null. JSON: {jsonResponse}");
                return responseData;
            }
        }
        catch (Exception ex) { HandleRequestException("RegisterUserAsync", id, endpointUrl, ex); return null; }
    }

    public async Task<UserAuthResponseData> LoginUserAsync(string id, string password)
    {
        if (httpClient == null) { Debug.LogError("[SSEObjectReceiver] HttpClient not initialized."); return null; }

        string endpointUrl = $"{authServerBaseAddress}{loginPath}";
        Debug.Log($"[SSEObjectReceiver] Logging in user '{id}' at: {endpointUrl}");

        try
        {
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("id", id),
                new KeyValuePair<string, string>("password", password)
            };
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var formContent = new FormUrlEncodedContent(formData))
            {
                HttpResponseMessage response = await httpClient.PostAsync(endpointUrl, formContent, cts.Token);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SSEObjectReceiver] HTTP Error {response.StatusCode} during login for '{id}': {jsonResponse}");
                    return null;
                }

                UserAuthResponseData responseData = JsonConvert.DeserializeObject<UserAuthResponseData>(jsonResponse);
                if (responseData != null)
                {
                    Debug.Log($"[SSEObjectReceiver] User '{id}' logged in successfully: {responseData}");
                    this.sseTargetUserId = responseData.id;
                    UnityMainThreadDispatcher.Instance().Enqueue(StartSseConnectionForCurrentUser);
                }
                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized login response for '{id}' is null. JSON: {jsonResponse}");
                return responseData;
            }
        }
        catch (Exception ex) { HandleRequestException("LoginUserAsync", id, endpointUrl, ex); return null; }
    }

    public async Task<UserAuthResponseData> UpdateUserDataAsync(string id, string newNickname = null, int? newLevel = null, int? newExp = null)
    {
        if (httpClient == null) { Debug.LogError("[SSEObjectReceiver] HttpClient not initialized."); return null; }

        string endpointUrl = $"{authServerBaseAddress}{userUpdatePath}";
        Debug.Log($"[SSEObjectReceiver] Updating data for user '{id}' at: {endpointUrl}");

        try
        {
            var formData = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("id", id) };
            if (!string.IsNullOrEmpty(newNickname)) formData.Add(new KeyValuePair<string, string>("nickname", newNickname));
            if (newLevel.HasValue) formData.Add(new KeyValuePair<string, string>("level", newLevel.Value.ToString()));
            if (newExp.HasValue) formData.Add(new KeyValuePair<string, string>("exp", newExp.Value.ToString()));

            if (formData.Count <= 1 && string.IsNullOrEmpty(newNickname) && !newLevel.HasValue && !newExp.HasValue)
            {
                Debug.LogWarning($"[SSEObjectReceiver] UpdateUserDataAsync called for user '{id}' but no actual update fields provided (only ID).");
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var formContent = new FormUrlEncodedContent(formData))
            {
                HttpResponseMessage response = await httpClient.PostAsync(endpointUrl, formContent, cts.Token);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SSEObjectReceiver] HTTP Error {response.StatusCode} during data update for '{id}': {jsonResponse}");
                    return null;
                }

                UserAuthResponseData responseData = JsonConvert.DeserializeObject<UserAuthResponseData>(jsonResponse);
                if (responseData != null) Debug.Log($"[SSEObjectReceiver] Data for user '{id}' updated successfully: {responseData}");
                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized update response for '{id}' is null. JSON: {jsonResponse}");
                return responseData;
            }
        }
        catch (Exception ex) { HandleRequestException("UpdateUserDataAsync", id, endpointUrl, ex); return null; }
    }

    public async Task<UserAuthResponseData> GetMyUserInfoAsync(string userIdToFetch)
    {
        if (httpClient == null) { Debug.LogError("[SSEObjectReceiver] HttpClient not initialized."); return null; }
        if (string.IsNullOrEmpty(userIdToFetch)) { Debug.LogError("[SSEObjectReceiver] userIdToFetch cannot be null or empty."); return null; }

        // FastAPI /users/me/�� current_user_id�� Query Parameter�� ����
        string endpointUrl = $"{authServerBaseAddress}{userGetMePath}?current_user_id={Uri.EscapeDataString(userIdToFetch)}";
        Debug.Log($"[SSEObjectReceiver] Fetching info for user '{userIdToFetch}' from: {endpointUrl}");

        try
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpointUrl))
            {
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage, cts.Token);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SSEObjectReceiver] HTTP Error {response.StatusCode} fetching info for '{userIdToFetch}': {jsonResponse}");
                    return null;
                }

                UserAuthResponseData responseData = JsonConvert.DeserializeObject<UserAuthResponseData>(jsonResponse);
                if (responseData != null) Debug.Log($"[SSEObjectReceiver] Info for user '{userIdToFetch}' fetched successfully: {responseData}");
                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized user info for '{userIdToFetch}' is null. JSON: {jsonResponse}");
                return responseData;
            }
        }
        catch (Exception ex) { HandleRequestException("GetMyUserInfoAsync", userIdToFetch, endpointUrl, ex); return null; }
    }

    public async Task<List<UserAuthResponseData>> GetAllUsersAsync()
    {
        if (httpClient == null) { Debug.LogError("[SSEObjectReceiver] HttpClient not initialized."); return null; }

        string endpointUrl = $"{authServerBaseAddress}{allUsersPath}";
        Debug.Log($"[SSEObjectReceiver] Fetching all users from: {endpointUrl}");

        try
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60))) 
            {
                HttpResponseMessage response = await httpClient.GetAsync(endpointUrl, cts.Token);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[SSEObjectReceiver] HTTP Error {response.StatusCode} fetching all users: {jsonResponse}");
                    return null;
                }

                allUsers = JsonConvert.DeserializeObject<List<UserAuthResponseData>>(jsonResponse);
                if (allUsers != null) Debug.Log($"[SSEObjectReceiver] Successfully fetched {allUsers.Count} users.");
                else Debug.LogWarning($"[SSEObjectReceiver] Deserialized list of all users is null. JSON: {jsonResponse}");
                return allUsers;
                
            }
        }
        catch (Exception ex) { HandleRequestException("GetAllUsersAsync", "N/A (all users)", endpointUrl, ex); return null; }
    }

    private void HandleRequestException(string methodName, string targetInfo, string url, Exception ex)
    {
        string logMessage;
        if (ex is TaskCanceledException tce)
        {
            logMessage = tce.CancellationToken.IsCancellationRequested ?
                $"[SSEObjectReceiver] {methodName} for '{targetInfo}' to {url} was canceled by token." :
                $"[SSEObjectReceiver] {methodName} for '{targetInfo}' to {url} timed out: {ex.Message}";
        }
        else if (ex is HttpRequestException) { logMessage = $"[SSEObjectReceiver] {methodName} HTTP Request Error for '{targetInfo}' to {url}: {ex.Message}"; }
        else if (ex is JsonException) { logMessage = $"[SSEObjectReceiver] {methodName} JSON Error for '{targetInfo}' from {url}: {ex.Message}. Response was likely not valid JSON."; }
        else { logMessage = $"[SSEObjectReceiver] {methodName} General Error for '{targetInfo}' to {url}: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}"; }

        if (ex is TaskCanceledException && ((TaskCanceledException)ex).CancellationToken.IsCancellationRequested)
            Debug.LogWarning(logMessage);
        else
            Debug.LogError(logMessage);
    }

    #endregion

    // ����ڰ� ������ UpdateDetectionInfoInUnity �޼��� (Farm ���� �ּ� ����)
    void UpdateDetectionInfoInUnity(Dictionary<string, List<NewDetectedObjectInfo>> detectionData)
    {
        // Example: Log the received data. Replace with your actual game logic.
        // This logging can be very verbose if updates are frequent.
        // Debug.Log($"[SSEObjectReceiver] Processing {detectionData.Count} sectors in UpdateDetectionInfoInUnity.");

        foreach (var sectorEntry in detectionData)
        {
            string sectorKey = sectorEntry.Key; // e.g., "0-0", "1-2"
            List<NewDetectedObjectInfo> objectsInSector = sectorEntry.Value;

            if (objectsInSector.Count > 0)
            {
                // Example: Log details for sectors that have objects
                // Debug.Log($"Sector [{sectorKey}]: Found {objectsInSector.Count} object(s).");
                foreach (NewDetectedObjectInfo objInfo in objectsInSector)
                {
                    /**
                    if (objInfo == null) continue;
                    else
                    {
                        if (Farm.instance.plantsManager.plantList[objInfo.sector_row, objInfo.sector_col] == null)
                        {
                            // plant�� �����µ� ���� ���
                            Farm.instance.plantsManager.AddPlant(objInfo.sector_row, objInfo.sector_col);
                        }

                        Farm.instance.plantsManager.visitedPlant[objInfo.sector_row, objInfo.sector_col] = true;
                    }

                    PlantType type = PlantType.Cabbage;
                    if (objInfo.ObjectType == "cabbage") type = PlantType.Cabbage;
                    else if (objInfo.ObjectType == "tomato") type = PlantType.Tomato;
                    else if (objInfo.ObjectType == "eggplant") type = PlantType.Eggplant;

                    PlantLevel lv = PlantLevel.Lv1;
                    if (objInfo.Level == "v1") lv = PlantLevel.Lv1;
                    else if (objInfo.Level == "v2") lv = PlantLevel.Lv2;
                    else if (objInfo.Level == "v3") lv = PlantLevel.Lv3;
                    else if (objInfo.Level == "v4") lv = PlantLevel.Lv4;
                    
                    Farm.instance.plantsManager.UpdatePlant(
                        Farm.instance.plantsManager.plantList[objInfo.sector_row, objInfo.sector_col],
                        objInfo.sector_row,
                        objInfo.sector_col,
                        type,
                        lv
                    );

                    // This loop seems to be part of a specific game logic,
                    // likely iterating over the entire farm grid after each object update,
                    // which might be inefficient if not intended.
                    // Consider if this should be outside the objectsInSector loop or after all sectors are processed.
                    for (int x = 0; x < (int)(Farm.instance.farmWidth / Farm.instance.cellSize); x++)
                    {
                        for (int z = 0; z < (int)(Farm.instance.farmBreadth / Farm.instance.cellSize); z++)
                        {
                            bool isVisited = Farm.instance.plantsManager.visitedPlant[x, z];
                            bool plantExistence = (Farm.instance.plantsManager.plantList[x, z] == null) ? false : true;
                            if (!isVisited && plantExistence)
                            {
                                Farm.instance.plantsManager.HarvestPlant(x, z);
                            }
                        }
                    }
                    **/
                    // Access objInfo.sector_row, objInfo.sector_col, objInfo.Level, objInfo.ObjectType
                    // Example:
                    Debug.Log($"  - Row: {objInfo.sector_row}, Col: {objInfo.sector_col}, Lv: {objInfo.Level}, Type: {objInfo.ObjectType}");
                    //
                    // YOUR UNITY UPDATE LOGIC GOES HERE:
                    // - Find GameObjects representing these sectors or objects.
                    // - Update UI elements (Text, Images, etc.).
                    // - Trigger game events.
                    // - Visualize bounding boxes (if you were to add coordinates back to server response).
                    //
                }
            }
            // else: This sector has no detected objects. You might want to clear previous visuals for this sector.
        }
        // After processing all sectors from a detection message, you might want to reset visited flags
        // or perform actions on plants that were not visited in this update.
        /*
        if(Farm.instance != null && Farm.instance.plantsManager != null) // Ensure instances exist
        {
            for (int x = 0; x < (int)(Farm.instance.farmWidth / Farm.instance.cellSize); x++)
            {
                for (int z = 0; z < (int)(Farm.instance.farmBreadth / Farm.instance.cellSize); z++)
                {
                    // Example: if Farm.instance.plantsManager.visitedPlant[x, z] was used to mark visited plants in this update cycle
                    // if (!Farm.instance.plantsManager.visitedPlant[x, z] && Farm.instance.plantsManager.plantList[x,z] != null)
                    // {
                    //     // This plant existed but was not in the current detection update - perhaps remove it or mark it as withered
                    //     Farm.instance.plantsManager.HandleUnvisitedPlant(x,z); 
                    // }
                    // Reset for next update cycle
                    // Farm.instance.plantsManager.visitedPlant[x, z] = false;
                }
            }
        }
        */
    }


    void OnApplicationQuit()
    {
        Debug.Log("[SSEObjectReceiver] Application quitting. Cleaning up SSE and HttpClient.");
        if (sseCancellationTokenSource != null)
        {
            sseCancellationTokenSource.Cancel();
            sseCancellationTokenSource.Dispose();
            sseCancellationTokenSource = null;
        }
        if (httpClient != null)
        {
            httpClient.Dispose();
            httpClient = null;
        }
    }

    void OnDestroy()
    {
        Debug.Log("[SSEObjectReceiver] GameObject is being destroyed. Cleaning up SSE and HttpClient.");
        if (sseCancellationTokenSource != null && !sseCancellationTokenSource.IsCancellationRequested)
        {
            sseCancellationTokenSource.Cancel();
        }
        // sseCancellationTokenSource will be disposed in OnApplicationQuit or when new one is created

        if (httpClient != null)
        {
            httpClient.Dispose(); // Dispose HttpClient here too if object is destroyed before app quit
            httpClient = null;
        }
        StopAllCoroutines();
    }
}