using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class WorldSingleton : MonoBehaviour
{
    public static WorldSingleton instance;
    public GameObject plantDetailWindow;
    public List<Plant> harvestedPlants;
    public SSEObjectReceiver sseReceiver;

    public List<UserAuthResponseData> allUsers;
    public UserDataResponse userData;
    
    public Sprite v1_cabbage;
    public Sprite v2_cabbage;
    public Sprite v3_cabbage;
    public Sprite v4_cabbage;
    
    public Sprite v1_tomato;
    public Sprite v2_tomato;
    public Sprite v3_tomato;
    public Sprite v4_tomato;

    public Sprite v1_eggplant;
    public Sprite v2_eggplant;
    public Sprite v3_eggplant;
    public Sprite v4_eggplant;
    
    private string serverUrl = "";


    public GameObject userScene;
    public GameObject friendScene;
    
    [FormerlySerializedAs("authUITesterr")] [FormerlySerializedAs("authUITesert")] public AuthUITester authUITester;
    
    private void Awake()
    {
        if (instance == null) { instance = this; }
        else if (instance != this) { Destroy(gameObject); }
        
        harvestedPlants = new List<Plant>();
        
        userScene.SetActive(true);
        friendScene.SetActive(false);
    }

    private void Start()
    {
        v1_cabbage = Resources.Load<Sprite>("Crops/v1_cabbage");
        v2_cabbage = Resources.Load<Sprite>("Crops/v2_cabbage");
        v3_cabbage = Resources.Load<Sprite>("Crops/v3_cabbage");
        v4_cabbage = Resources.Load<Sprite>("Crops/v4_cabbage");
        
        v1_tomato = Resources.Load<Sprite>("Crops/v1_tomato");
        v2_tomato = Resources.Load<Sprite>("Crops/v2_tomato");
        v3_tomato = Resources.Load<Sprite>("Crops/v3_tomato");
        v4_tomato = Resources.Load<Sprite>("Crops/v4_tomato");
        
        v1_eggplant = Resources.Load<Sprite>("Crops/v1_eggplant");
        v2_eggplant = Resources.Load<Sprite>("Crops/v2_eggplant");
        v3_eggplant = Resources.Load<Sprite>("Crops/v3_eggplant");
        v4_eggplant = Resources.Load<Sprite>("Crops/v4_eggplant");
    }

    #region JSONProcessing
    private void DumpHarvestedPlantsIntoJson()
    {
        HarvestedPlantListWrapper wrapper = new HarvestedPlantListWrapper
        {
            plants = harvestedPlants
        };

        string json = JsonUtility.ToJson(wrapper, true);
        
        string path = Application.persistentDataPath + "/harvestedPlants.json";
        File.WriteAllText(path, json);
        
        Debug.Log("Plants saved to: " + path);
        Debug.Log("JSON content: " + json);
    }
    
    public void ExportHarvestedPlants()
    {
        StartCoroutine(SendHarvestedPlantsToServer());
    }

    private IEnumerator SendHarvestedPlantsToServer()
    {
        List<JsonPlant> jsonPlants = new List<JsonPlant>();

        foreach (Plant plant in harvestedPlants)
        {
            string plantType = PlantTypeToString(plant.plantInfo.plantType);
            string plantLevel = PlantLevelToString(plant.plantInfo.plantLevel);
            string plantRank = PlantRankToString(plant.plantInfo.rank);
            
            jsonPlants.Add(new JsonPlant
            {
                type = plantType,
                status = plantLevel,
                rank = plantRank,
            });
        }
        
        string jsonData = JsonConvert.SerializeObject(jsonPlants);
        
        UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
        
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Error: {request.error}");
        }
        
        request.Dispose();
    }

    public string PlantTypeToString(PlantType plantType)
    {
        switch (plantType)
        {
            case PlantType.Cabbage:
                return "cabbage";
            case PlantType.Eggplant:
                return "eggplant";
            case PlantType.Tomato:
                return "tomato";
        }
        return "error";
    }

    public string PlantLevelToString(PlantLevel plantLevel)
    {
        switch (plantLevel)
        {
            case PlantLevel.Lv1:
                return "v1";
            case PlantLevel.Lv2:
                return "v2";
            case PlantLevel.Lv3:
                return "v3";
            case PlantLevel.Lv4:
                return "v4";
        }
        return "error";
    }

    public string PlantRankToString(PlantRank plantRank)
    {
        switch (plantRank)
        {
            case PlantRank.A:
                return "a";
            case PlantRank.B:
                return "b";
            case PlantRank.C:
                return "c";
            case PlantRank.D:
                return "d"; //병든거
        }
        return "error";
    }
    #endregion


    public void SwitchSceneToFriendScene()
    {
        Farm.instance.gameObject.transform.SetParent(friendScene.transform);
        userScene.SetActive(false);
        friendScene.SetActive(true);
        Farm.instance.ClearFarm();
        
        LoadFarmData(allUsers[1].id);
    }
    
    public void SwitchSceneToUserScene()
    {    
        Farm.instance.gameObject.transform.SetParent(userScene.transform);
        userScene.SetActive(true);
        friendScene.SetActive(false);
        Farm.instance.ClearFarm();
        
        LoadFarmData(allUsers[2].id);
    }
    
    public async void LoadFarmData(string userId)
    {
        // 1. 사용자 ID 설정
        sseReceiver.sseTargetUserId = userId;
        
        // 2. 농장 데이터 요청
        UserDataResponse response = await sseReceiver.RequestCombinedUserDataAsync(userId);
        sseReceiver.UpdateDetectionInfoInUnity(response.detection_data);
    public float baseRequiredExp = 10;
    public float requiredExp = 10;
    public float expGrowthRate = 1.3f;
    public float userExp = 0f;
    public int userLv = 0;

    public void levelUp()
    {
        requiredExp *= 1.3f;
        userLv++;
    }

    public int levelUpdate()
    {
        if (userExp >= requiredExp)
        {
            levelUp();
            userExp -= requiredExp;
            return userLv;
        }

        return userLv;
    }
}

public class JsonPlant
{
    [JsonProperty("type")] public string type;
    [JsonProperty("status")] public string status;
    [JsonProperty("rank")] public string rank; 
}

public class HarvestedPlantListWrapper
{
    public List<Plant> plants;
}
