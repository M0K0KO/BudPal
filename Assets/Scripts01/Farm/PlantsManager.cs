using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using InvalidOperationException = System.InvalidOperationException;

public class PlantsManager : MonoBehaviour
{
    private int rowSize = 0;
    private int colSize = 0;
    public Plant[,] plantList;
    public bool[,] visitedPlant;
    private Farm farm;

    private float cellSize;

    public GameObject plantPrefab;
    
    private void Awake()
    {
        farm = GetComponent<Farm>();
        cellSize = farm.cellSize;
    }

    private void Start()
    {
    }

    ////////////////////////////
    private void Update()
    {

    }
    ////////////////////////////

    public void MakePlantsList()
    {
        rowSize = (int)(farm.farmWidth / cellSize);
        colSize = (int)(farm.farmBreadth / cellSize);
        plantList = new Plant[rowSize, colSize];
        visitedPlant = new bool[rowSize, colSize];
    }
    
    private Vector3 plantPosition(int x, int z)
    {
        Vector3 topLeftCorner = new Vector3(
            transform.position.x - (farm.farmWidth / 2f), 
            transform.position.y,
            transform.position.z - (farm.farmBreadth / 2f));

        float normalizedX = (topLeftCorner.x + ((cellSize / 2f) + (x * cellSize))) / farm.farmWidth;
        float normalizedZ = (topLeftCorner.z + ((cellSize / 2f) + (z * cellSize))) / farm.farmBreadth;

        Debug.Log(topLeftCorner);
        
        return new Vector3(normalizedX, transform.position.y + 0.5f, normalizedZ);
    }

    
    
    //Instantiate 될 때 자동으로 호출
    public void AddPlant(int row, int col)
    {
        GameObject instantiatedPlant = Instantiate(plantPrefab, transform.position, Quaternion.identity);
        Plant plant = instantiatedPlant.GetComponent<Plant>();
        
        Debug.Log(row + "," + col);
        
        if (plantList[row, col] == null)
        {
            plantList[row, col] = plant;
            Vector3 localPosition = plantPosition(row, col);
            localPosition.x *= farm.farmWidth;
            localPosition.z *= farm.farmBreadth;
            
            plant.transform.localPosition = localPosition;
            plant.plantInfo.currentCoordinate = new PlantCoordinate(row, col);

            plant.transform.DOScaleX(3f / farm.farmWidth, 0.5f).SetEase(Ease.InOutExpo);
            plant.transform.DOScaleY(3f, 0.5f).SetEase(Ease.InOutExpo);
            plant.transform.DOScaleZ(3f / farm.farmBreadth, 0.5f).SetEase(Ease.InOutExpo);
            
            plant.transform.SetParent(transform);


            return;
        }
        return;
    }

    //
    public void UpdatePlant(Plant plant, int row, int col, PlantType plantType, PlantLevel plantLevel)
    {
        if (plantList[row, col] != null)
        {
            plantList[row, col].plantController.ChangeType(plantType);
            plantList[row, col].plantController.ChangeLevel(plantLevel);

            switch (plantLevel)
            {
                case PlantLevel.Lv1:
                    plantList[row, col].plantInfo.rank = PlantRank.C;
                    break;
                case PlantLevel.Lv2:
                    plantList[row, col].plantInfo.rank = PlantRank.B;
                    break;
                case PlantLevel.Lv3:
                    plantList[row, col].plantInfo.rank = PlantRank.A;
                    break;
                case PlantLevel.Lv4:
                    plantList[row, col].plantInfo.rank = PlantRank.D;
                    break;
            }

            plant.plantController.currentOutline.enabled = false;
        }
    }

    public void HarvestPlant(int row, int col)
    {
        Plant targetPlant = plantList[row, col];
        WorldSingleton.instance.harvestedPlants.Add(targetPlant);
        
        Material material = targetPlant.plantController.currentActiveRenderer.material;
        Sequence sequence = DOTween.Sequence();

        sequence.Append(material.DOFade(0f, 0.5f).SetEase(Ease.Linear));
        sequence.Join(targetPlant.transform.DOMoveY(10f, 0.5f).SetEase(Ease.Linear));

        sequence.OnComplete(() => {
            Destroy(targetPlant.gameObject);
        });
    }
}
