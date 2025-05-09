using System;
using UnityEngine;

public class Plant : MonoBehaviour
{
    public PlantInfo plantInfo;
    public PlantController plantController;
    
    // Plant Coordinate은 PlantsManager에서 관리하는 2차원 plant list에서 관리하는 인덱스.
    
    private void Awake()
    {
        plantInfo = GetComponent<PlantInfo>();
        plantController = GetComponent<PlantController>();
    }
}
