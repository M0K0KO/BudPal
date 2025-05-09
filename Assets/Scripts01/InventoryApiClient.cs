using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text; // For Encoding
using TMPro; // TextMeshPro 사용 시, 일반 UI Text 사용 시 UnityEngine.UI
using System.Collections.Generic; // For List, if needed, though array is used by JsonUtility

// --- 데이터 구조 정의 ---

[System.Serializable]
public class ItemTransactionPayload // /purchase 및 /sell (일반) 요청용
{
    public string id;
    public string item_name;
    public int count;
}

[System.Serializable]
public class PurchaseLogEntryResponse // 서버의 PurchaseLogEntry 모델에 대응
{
    public string log_id; // << NEW FIELD
    public string user_id;
    public int purchased_count;
    public string timestamp; // JSON에서는 문자열로 받음
}

[System.Serializable]
public class ItemStockResponse // 서버의 ItemStock 모델에 대응 (구매 기록 포함)
{
    public string item_name;
    public int current_stock;
    public PurchaseLogEntryResponse[] purchase_history; // 구매 기록 배열
    public string error; // /stocks 엔드포인트에서 개별 아이템 오류가 있을 경우
}

// /stocks 엔드포인트는 JSON 배열을 반환하므로, 이를 JsonUtility로 파싱하기 위한 래퍼 클래스
[System.Serializable]
public class AllStocksResponseWrapper
{
    public ItemStockResponse[] stocks; // JsonUtility가 루트 배열을 파싱하기 위해 필드 이름 "stocks" 사용
}

[System.Serializable]
public class SellSpecificRecordPayloadCSharp // /sell_specific_record 요청용
{
    public string item_name;
    public string log_id;
    // public string selling_user_id; // 필요시 추가 (서버에서 현재 사용 안함)
}


public class InventoryApiClient : MonoBehaviour
{
    public string baseUrl = "http://127.0.0.1:8002"; // FastAPI 서버 주소 (Inspector에서 설정 가능)

    // --- UI 요소 연결 (Inspector에서 연결) ---
    public TMP_InputField idInputField;
    public TMP_InputField itemNameInputField;
    public TMP_InputField countInputField;
    public TMP_InputField logIdInputField; // << NEW: 특정 구매 기록 ID 입력용
    public TMP_Text resultText;

    // --- 테스트용 공개 메소드 (UI 버튼에 연결) ---

    public void OnPurchaseButtonClicked()
    {
        if (ValidateGeneralInputs(out string userId, out string itemName, out int count))
        {
            StartCoroutine(SendTransactionRequest("/purchase", userId, itemName, count));
        }
    }

    public void OnSellButtonClicked()
    {
        if (ValidateGeneralInputs(out string userId, out string itemName, out int count))
        {
            StartCoroutine(SendTransactionRequest("/sell", userId, itemName, count));
        }
    }

    public void OnGetItemStockClicked()
    {
        string itemName = itemNameInputField.text;
        if (string.IsNullOrWhiteSpace(itemName))
        {
            SetResultText("아이템 이름을 입력해주세요.");
            return;
        }
        StartCoroutine(GetItemStockRequest(itemName));
    }

    public void OnGetAllStocksClicked()
    {
        StartCoroutine(GetAllStocksRequest());
    }

    public void OnSellSpecificRecordClicked() // << NEW METHOD
    {
        string itemName = itemNameInputField.text; // 기존 아이템 이름 입력 필드 재활용
        string logId = logIdInputField.text;     // 새로 추가된 Log ID 입력 필드 사용

        if (string.IsNullOrWhiteSpace(itemName))
        {
            SetResultText("아이템 이름을 입력해주세요.");
            return;
        }
        if (string.IsNullOrWhiteSpace(logId))
        {
            SetResultText("삭제할 구매 기록의 Log ID를 입력해주세요.");
            return;
        }
        StartCoroutine(SellSpecificRecordCoroutine(itemName, logId));
    }

    // --- 입력 유효성 검사 ---
    private bool ValidateGeneralInputs(out string userId, out string itemName, out int count)
    {
        userId = idInputField.text;
        itemName = itemNameInputField.text;

        count = 0; // 기본값 초기화
        if (string.IsNullOrWhiteSpace(userId))
        {
            SetResultText("사용자 ID를 입력해주세요.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(itemName))
        {
            SetResultText("아이템 이름을 입력해주세요.");
            return false;
        }
        if (!int.TryParse(countInputField.text, out count) || count <= 0)
        {
            SetResultText("수량은 0보다 큰 숫자로 입력해주세요.");
            return false;
        }
        return true;
    }

    // --- 공통 요청 코루틴 (구매 및 일반 판매) ---
    private IEnumerator SendTransactionRequest(string endpoint, string userId, string itemName, int countValue)
    {
        SetResultText($"요청 중... {endpoint}");
        string url = baseUrl + endpoint;

        ItemTransactionPayload payload = new ItemTransactionPayload
        {
            id = userId,
            item_name = itemName,
            count = countValue
        };
        string jsonPayload = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            HandleResponse(request, endpoint, (responseText) => JsonUtility.FromJson<ItemStockResponse>(responseText));
        }
    }

    // --- 특정 구매 기록 판매/삭제 요청 코루틴 --- // << NEW COROUTINE
    private IEnumerator SellSpecificRecordCoroutine(string itemName, string logId)
    {
        SetResultText($"특정 구매 기록 판매/삭제 중... (Item: {itemName}, LogID: {logId})");
        string url = baseUrl + "/sell_specific_record"; // FastAPI에 정의된 새 엔드포인트

        SellSpecificRecordPayloadCSharp payload = new SellSpecificRecordPayloadCSharp
        {
            item_name = itemName,
            log_id = logId
        };
        string jsonPayload = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST")) // POST 요청
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            HandleResponse(request, "/sell_specific_record", (responseText) => JsonUtility.FromJson<ItemStockResponse>(responseText));
        }
    }

    // --- 재고 확인 요청 코루틴들 ---
    private IEnumerator GetItemStockRequest(string itemName)
    {
        SetResultText($"재고 확인 중... ({itemName})");
        string encodedItemName = UnityWebRequest.EscapeURL(itemName);
        string url = $"{baseUrl}/stock/{encodedItemName}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            HandleResponse(request, $"/stock/{itemName}", (responseText) => JsonUtility.FromJson<ItemStockResponse>(responseText));
        }
    }

    private IEnumerator GetAllStocksRequest()
    {
        SetResultText("모든 재고 확인 중...");
        string url = baseUrl + "/stocks";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                HandleError(request);
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                Debug.Log($"Raw Response (GetAllStocks): {responseJson}");

                // JsonUtility는 루트가 배열인 JSON을 직접 파싱하지 못하므로, 래퍼 클래스를 사용.
                string wrappedJson = "{\"stocks\":" + responseJson + "}";
                try
                {
                    AllStocksResponseWrapper responseWrapper = JsonUtility.FromJson<AllStocksResponseWrapper>(wrappedJson);
                    StringBuilder sb = new StringBuilder("전체 재고 목록:\n");
                    if (responseWrapper.stocks != null && responseWrapper.stocks.Length > 0)
                    {
                        foreach (var stockItem in responseWrapper.stocks)
                        {
                            AppendStockDetailsToStringBuilder(sb, stockItem, "  ");
                        }
                    }
                    else
                    {
                        sb.AppendLine("데이터 없음");
                    }
                    SetResultText(sb.ToString());
                    Debug.Log("GetAllStocks Success.");
                }
                catch (System.Exception ex)
                {
                    SetResultText($"JSON 파싱 오류 (GetAllStocks): {ex.Message}\nRaw: {responseJson}");
                    Debug.LogError($"JSON Parse Error (GetAllStocks): {ex.Message} \nRaw response: {responseJson}");
                }
            }
        }
    }

    // --- 공통 응답 처리 및 결과 표시 ---
    private void HandleResponse(UnityWebRequest request, string operationName, System.Func<string, ItemStockResponse> parseAction)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError ||
            request.result == UnityWebRequest.Result.DataProcessingError)
        {
            HandleError(request);
        }
        else
        {
            string responseJson = request.downloadHandler.text;
            Debug.Log($"Raw Response ({operationName}): {responseJson}");
            try
            {
                ItemStockResponse response = parseAction(responseJson);
                StringBuilder sb = new StringBuilder($"{operationName} 성공!\n");
                AppendStockDetailsToStringBuilder(sb, response);

                SetResultText(sb.ToString());
                Debug.Log($"{operationName} Success: Item: {response.item_name}, Stock: {response.current_stock}");
            }
            catch (System.Exception ex)
            {
                SetResultText($"JSON 파싱 오류 ({operationName}): {ex.Message}\nRaw: {responseJson}");
                Debug.LogError($"JSON Parse Error ({operationName}): {ex.Message} \nRaw response: {responseJson}");
            }
        }
    }

    private void HandleError(UnityWebRequest request)
    {
        string errorMessage = $"Error: {request.error}\nResponse Code: {request.responseCode}\n";
        if (!string.IsNullOrEmpty(request.downloadHandler.text))
        {
            // 서버에서 보낸 JSON 형태의 오류 메시지를 파싱 시도
            try
            {
                // FastAPI의 HTTPException detail을 파싱 시도 (단순 문자열일 수도 있음)
                var errorDetail = JsonUtility.FromJson<ErrorResponseDetail>(request.downloadHandler.text);
                if (errorDetail != null && !string.IsNullOrEmpty(errorDetail.detail))
                {
                    errorMessage += $"Server Message: {errorDetail.detail}";
                }
                else
                {
                    errorMessage += $"Server Message: {request.downloadHandler.text}";
                }
            }
            catch
            {
                errorMessage += $"Server Message: {request.downloadHandler.text}"; // 파싱 실패시 원본 텍스트
            }
        }
        SetResultText(errorMessage);
        Debug.LogError(errorMessage);
    }

    [System.Serializable]
    private class ErrorResponseDetail // FastAPI HTTPException detail 파싱용
    {
        public string detail;
    }


    private void AppendStockDetailsToStringBuilder(StringBuilder sb, ItemStockResponse stockData, string prefix = "")
    {
        sb.AppendLine($"{prefix}물품명: {stockData.item_name}");
        sb.AppendLine($"{prefix}현재 재고: {stockData.current_stock}개");
        if (stockData.purchase_history != null && stockData.purchase_history.Length > 0)
        {
            sb.AppendLine($"{prefix}구매 기록:");
            foreach (var entry in stockData.purchase_history)
            {
                // LogID 표시 추가
                sb.AppendLine($"{prefix}  - LogID: {entry.log_id}, User: {entry.user_id}, Qty: {entry.purchased_count}, Time: {entry.timestamp}");
            }
        }
        else
        {
            sb.AppendLine($"{prefix}구매 기록 없음.");
        }
        if (!string.IsNullOrEmpty(stockData.error))
        {
            sb.AppendLine($"{prefix}  오류: {stockData.error}");
        }
    }

    private void SetResultText(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }
        Debug.Log($"API Client: {message}");
    }
}