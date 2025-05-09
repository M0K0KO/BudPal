using System;
using cakeslice;
using DG.Tweening;
using UnityEngine;

public class PlantController : MonoBehaviour
{
    private Plant plant;
    public MeshRenderer currentActiveRenderer;
    public Outline currentOutline;
    
    private void Awake()
    {
        plant = GetComponent<Plant>();
    }

    ////////////////////////////
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ChangeLevel(PlantLevel.Lv1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ChangeLevel(PlantLevel.Lv2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ChangeLevel(PlantLevel.Lv3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ChangeType(PlantType.Cabbage);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ChangeType(PlantType.Tomato);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            ChangeType(PlantType.Eggplant);
        }
    }
    ////////////////////////////

    private void HandlePlantTypeChange()
    {
        DisableAllActiveMeshRenderers();
        
        PlantType currentType = plant.plantInfo.plantType;
        PlantLevel currentLevel = plant.plantInfo.plantLevel;

        MeshRenderer targetMesh = transform.Find(currentType + "_" + currentLevel).GetComponent<MeshRenderer>();
        currentActiveRenderer = targetMesh;
        currentOutline = targetMesh.GetComponent<Outline>();
        targetMesh.enabled = true;
    }

    private void HandlePlantLevelChange(PlantLevel prevLevel)
    {
        DisableAllActiveMeshRenderers();
        
        PlantType currentType = plant.plantInfo.plantType;
        PlantLevel currentLevel = plant.plantInfo.plantLevel;

        MeshRenderer targetMesh = transform.Find(currentType + "_" + currentLevel).GetComponent<MeshRenderer>();
        currentActiveRenderer = targetMesh;
        currentOutline = targetMesh.GetComponent<Outline>();
        targetMesh.enabled = true;

        if (prevLevel != plant.plantInfo.plantLevel)
        {
            plant.transform.DOScaleX(1f / Farm.instance.farmWidth, 0f);
            plant.transform.DOScaleY(1f, 0f);
            plant.transform.DOScaleZ(1f / Farm.instance.farmBreadth, 0f);

            plant.transform.DOScaleX(3f / Farm.instance.farmWidth, 0.5f).SetEase(Ease.InOutExpo);
            plant.transform.DOScaleY(3f, 0.5f).SetEase(Ease.InOutExpo);
            plant.transform.DOScaleZ(3f / Farm.instance.farmBreadth, 0.5f).SetEase(Ease.InOutExpo);
        }
    }
    
    public void DisableAllActiveMeshRenderers()
    {
        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>(true);
        
        foreach (MeshRenderer renderer in childRenderers)
        { 
            renderer.enabled = false;
        }
    }

    public void ChangeType(PlantType plantType)
    {
        plant.plantInfo.plantType = plantType;
        HandlePlantTypeChange();
    }

    public void ChangeLevel(PlantLevel plantLevel)
    {
        if (plantLevel == PlantLevel.Lv4 && plant.plantInfo.plantLevel != PlantLevel.Lv4)
        {
            Debug.Log("이새끼 아픔;;");
        }

        if (plantLevel == PlantLevel.Lv3 && plant.plantInfo.plantLevel != PlantLevel.Lv3)
        {
            Debug.Log("다 컸다!!");
        }
        
        PlantLevel prevLevel = plant.plantInfo.plantLevel;
        plant.plantInfo.plantLevel = plantLevel;
        HandlePlantLevelChange(prevLevel);
    }
}
