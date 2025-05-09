using System;
using UnityEngine;

public class PlantInfo : MonoBehaviour
{
    public PlantCoordinate currentCoordinate;
    public PlantType plantType;
    public PlantLevel plantLevel;
    public PlantRank rank;

    private void Awake()
    {
        currentCoordinate = new PlantCoordinate(0, 0);
    }

    public string PlantStatusByRank()
    {
        switch (rank)
        {
            case PlantRank.A:
                return "건강하다...";
            case PlantRank.B:
                return "자라는 중이다...";
            case PlantRank.C:
                return "아직 어리다...";
            case PlantRank.D:
                return "병에 걸렸다...";
        }

        return "맛있겠다";
    }

    public Sprite PlantImageByInfo()
    {
        string type = WorldSingleton.instance.PlantTypeToString(plantType);
        string lv = WorldSingleton.instance.PlantLevelToString(plantLevel);

        string total = lv + "_" + type;
        Sprite result = Resources.Load<Sprite>("Crops/" + total);
        
        return result;
    }
}



public struct PlantCoordinate
{
    public int x;
    public int y;

    public PlantCoordinate(int x, int y) { this.x = x; this.y = y; }
    
    
}
