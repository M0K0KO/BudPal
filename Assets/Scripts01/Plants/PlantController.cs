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

        MeshRenderer targetMesh = transform.Find("Meshes").Find(currentType + "_" + currentLevel).GetComponent<MeshRenderer>();
        currentActiveRenderer = targetMesh;
        currentOutline = targetMesh.GetComponent<Outline>();
        targetMesh.enabled = true;
    }

    private void HandlePlantLevelChange(PlantLevel prevLevel)
    {
        DisableAllActiveMeshRenderers();
        
        PlantType currentType = plant.plantInfo.plantType;
        PlantLevel currentLevel = plant.plantInfo.plantLevel;

        MeshRenderer targetMesh = transform.Find("Meshes").Find(currentType + "_" + currentLevel).GetComponent<MeshRenderer>();
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
        MeshRenderer[] childRenderers = transform.Find("Meshes").GetComponentsInChildren<MeshRenderer>(true);
        
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
        MeshRenderer deadIcon = transform.Find("PlantDeadIcon").GetComponentInChildren<MeshRenderer>();
        MeshRenderer readyIcon = transform.Find("PlantReadyIcon").GetComponentInChildren<MeshRenderer>();
        
        if (plantLevel == PlantLevel.Lv4)
        {
            deadIcon.enabled = true;
            readyIcon.enabled = false;
        }
        else if (plantLevel == PlantLevel.Lv3)
        {
            deadIcon.enabled = false;
            readyIcon.enabled = true;
        }
        else
        {
            deadIcon.enabled = false;
            readyIcon.enabled = false;
        }
        
        PlantLevel prevLevel = plant.plantInfo.plantLevel;
        plant.plantInfo.plantLevel = plantLevel;
        HandlePlantLevelChange(prevLevel);
    }
}
