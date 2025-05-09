using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text; // For Encoding
using TMPro; // TextMeshPro ��� ��, �Ϲ� UI Text ��� �� UnityEngine.UI
using System.Collections.Generic; // For List, if needed, though array is used by JsonUtility

// --- ������ ���� ���� ---

[System.Serializable]
public class ItemTransactionPayload // /purchase �� /sell (�Ϲ�) ��û��
{
    public string id;
    public string item_name;
    public int count;
}

[System.Serializable]
public class PurchaseLogEntryResponse // ������ PurchaseLogEntry �𵨿� ����
{
    public string log_id; // << NEW FIELD
    public string user_id;
    public int purchased_count;
    public string timestamp; // JSON������ ���ڿ��� ����
}

[System.Serializable]
public class ItemStockResponse // ������ ItemStock �𵨿� ���� (���� ��� ����)
{
    public string item_name;
    public int current_stock;
    public PurchaseLogEntryResponse[] purchase_history; // ���� ��� �迭
    public string error; // /stocks ��������Ʈ���� ���� ������ ������ ���� ���
}

// /stocks ��������Ʈ�� JSON �迭�� ��ȯ�ϹǷ�, �̸� JsonUtility�� �Ľ��ϱ� ���� ���� Ŭ����
[System.Serializable]
public class AllStocksResponseWrapper
{
    public ItemStockResponse[] stocks; // JsonUtility�� ��Ʈ �迭�� �Ľ��ϱ� ���� �ʵ� �̸� "stocks" ���
}

[System.Serializable]
public class SellSpecificRecordPayloadCSharp // /sell_specific_record ��û��
{
    public string item_name;
    public string log_id;
    // public string selling_user_id; // �ʿ�� �߰� (�������� ���� ��� ����)
}


public class InventoryApiClient : MonoBehaviour
{
    public string baseUrl = "http://127.0.0.1:8002"; // FastAPI ���� �ּ� (Inspector���� ���� ����)

    // --- UI ��� ���� (Inspector���� ����) ---
    public TMP_InputField idInputField;
    public TMP_InputField itemNameInputField;
    public TMP_InputField countInputField;
    public TMP_InputField logIdInputField; // << NEW: Ư�� ���� ��� ID �Է¿�
    public TMP_Text resultText;

    // --- �׽�Ʈ�� ���� �޼ҵ� (UI ��ư�� ����) ---

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
            SetResultText("������ �̸��� �Է����ּ���.");
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
        string itemName = itemNameInputField.text; // ���� ������ �̸� �Է� �ʵ� ��Ȱ��
        string logId = logIdInputField.text;     // ���� �߰��� Log ID �Է� �ʵ� ���

        if (string.IsNullOrWhiteSpace(itemName))
        {
            SetResultText("������ �̸��� �Է����ּ���.");
            return;
        }
        if (string.IsNullOrWhiteSpace(logId))
        {
            SetResultText("������ ���� ����� Log ID�� �Է����ּ���.");
            return;
        }
        StartCoroutine(SellSpecificRecordCoroutine(itemName, logId));
    }

    // --- �Է� ��ȿ�� �˻� ---
    private bool ValidateGeneralInputs(out string userId, out string itemName, out int count)
    {
        userId = idInputField.text;
        itemName = itemNameInputField.text;

        count = 0; // �⺻�� �ʱ�ȭ
        if (string.IsNullOrWhiteSpace(userId))
        {
            SetResultText("����� ID�� �Է����ּ���.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(itemName))
        {
            SetResultText("������ �̸��� �Է����ּ���.");
            return false;
        }
        if (!int.TryParse(countInputField.text, out count) || count <= 0)
        {
            SetResultText("������ 0���� ū ���ڷ� �Է����ּ���.");
            return false;
        }
        return true;
    }

    // --- ���� ��û �ڷ�ƾ (���� �� �Ϲ� �Ǹ�) ---
    private IEnumerator SendTransactionRequest(string endpoint, string userId, string itemName, int countValue)
    {
        SetResultText($"��û ��... {endpoint}");
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

    // --- Ư�� ���� ��� �Ǹ�/���� ��û �ڷ�ƾ --- // << NEW COROUTINE
    private IEnumerator SellSpecificRecordCoroutine(string itemName, string logId)
    {
        SetResultText($"Ư�� ���� ��� �Ǹ�/���� ��... (Item: {itemName}, LogID: {logId})");
        string url = baseUrl + "/sell_specific_record"; // FastAPI�� ���ǵ� �� ��������Ʈ

        SellSpecificRecordPayloadCSharp payload = new SellSpecificRecordPayloadCSharp
        {
            item_name = itemName,
            log_id = logId
        };
        string jsonPayload = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST")) // POST ��û
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();
            HandleResponse(request, "/sell_specific_record", (responseText) => JsonUtility.FromJson<ItemStockResponse>(responseText));
        }
    }

    // --- ��� Ȯ�� ��û �ڷ�ƾ�� ---
    private IEnumerator GetItemStockRequest(string itemName)
    {
        SetResultText($"��� Ȯ�� ��... ({itemName})");
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
        SetResultText("��� ��� Ȯ�� ��...");
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

                // JsonUtility�� ��Ʈ�� �迭�� JSON�� ���� �Ľ����� ���ϹǷ�, ���� Ŭ������ ���.
                string wrappedJson = "{\"stocks\":" + responseJson + "}";
                try
                {
                    AllStocksResponseWrapper responseWrapper = JsonUtility.FromJson<AllStocksResponseWrapper>(wrappedJson);
                    StringBuilder sb = new StringBuilder("��ü ��� ���:\n");
                    if (responseWrapper.stocks != null && responseWrapper.stocks.Length > 0)
                    {
                        foreach (var stockItem in responseWrapper.stocks)
                        {
                            AppendStockDetailsToStringBuilder(sb, stockItem, "  ");
                        }
                    }
                    else
                    {
                        sb.AppendLine("������ ����");
                    }
                    SetResultText(sb.ToString());
                    Debug.Log("GetAllStocks Success.");
                }
                catch (System.Exception ex)
                {
                    SetResultText($"JSON �Ľ� ���� (GetAllStocks): {ex.Message}\nRaw: {responseJson}");
                    Debug.LogError($"JSON Parse Error (GetAllStocks): {ex.Message} \nRaw response: {responseJson}");
                }
            }
        }
    }

    // --- ���� ���� ó�� �� ��� ǥ�� ---
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
                StringBuilder sb = new StringBuilder($"{operationName} ����!\n");
                AppendStockDetailsToStringBuilder(sb, response);

                SetResultText(sb.ToString());
                Debug.Log($"{operationName} Success: Item: {response.item_name}, Stock: {response.current_stock}");
            }
            catch (System.Exception ex)
            {
                SetResultText($"JSON �Ľ� ���� ({operationName}): {ex.Message}\nRaw: {responseJson}");
                Debug.LogError($"JSON Parse Error ({operationName}): {ex.Message} \nRaw response: {responseJson}");
            }
        }
    }

    private void HandleError(UnityWebRequest request)
    {
        string errorMessage = $"Error: {request.error}\nResponse Code: {request.responseCode}\n";
        if (!string.IsNullOrEmpty(request.downloadHandler.text))
        {
            // �������� ���� JSON ������ ���� �޽����� �Ľ� �õ�
            try
            {
                // FastAPI�� HTTPException detail�� �Ľ� �õ� (�ܼ� ���ڿ��� ���� ����)
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
                errorMessage += $"Server Message: {request.downloadHandler.text}"; // �Ľ� ���н� ���� �ؽ�Ʈ
            }
        }
        SetResultText(errorMessage);
        Debug.LogError(errorMessage);
    }

    [System.Serializable]
    private class ErrorResponseDetail // FastAPI HTTPException detail �Ľ̿�
    {
        public string detail;
    }


    private void AppendStockDetailsToStringBuilder(StringBuilder sb, ItemStockResponse stockData, string prefix = "")
    {
        sb.AppendLine($"{prefix}��ǰ��: {stockData.item_name}");
        sb.AppendLine($"{prefix}���� ���: {stockData.current_stock}��");
        if (stockData.purchase_history != null && stockData.purchase_history.Length > 0)
        {
            sb.AppendLine($"{prefix}���� ���:");
            foreach (var entry in stockData.purchase_history)
            {
                // LogID ǥ�� �߰�
                sb.AppendLine($"{prefix}  - LogID: {entry.log_id}, User: {entry.user_id}, Qty: {entry.purchased_count}, Time: {entry.timestamp}");
            }
        }
        else
        {
            sb.AppendLine($"{prefix}���� ��� ����.");
        }
        if (!string.IsNullOrEmpty(stockData.error))
        {
            sb.AppendLine($"{prefix}  ����: {stockData.error}");
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