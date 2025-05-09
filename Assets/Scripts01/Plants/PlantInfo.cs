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

    public Sprite ItemImageByInfo()
    {
        switch (plantLevel)
        {
            case PlantLevel.Lv1:
                return UIController.instance.firstLevelItem;
            case PlantLevel.Lv2:
                return UIController.instance.secondLevelItem;
            case PlantLevel.Lv3:
                return UIController.instance.thirdLevelItem;
            case PlantLevel.Lv4:
                return UIController.instance.fourthLevelItem;
            default:
                return null;
        }
    }

    public string ItemNameByInfo()
    {
        switch (plantLevel)
        {
            case PlantLevel.Lv1:
                return "성장촉진제";
            case PlantLevel.Lv2:
                return "영양보충제";
            case PlantLevel.Lv3:
                return "품질향상제";
            case PlantLevel.Lv4:
                return "회복살충제";
            default:
                return null;
        }
    }

    public string PlantDescByInfo()
    {
        switch (plantType)
        {
            case PlantType.Tomato:
                return "상큼하고 과즙 풍부한 토마토는 다양한 요리에 활용되는 건강한 붉은 과일입니다.";
            case PlantType.Eggplant:
                return "보라색 광택의 가지는 부드러운 식감으로 다양한 조리법에 활용되는 영양 채소입니다.";
            case PlantType.Cabbage:
                return "아삭한 양배추는 영양소가 풍부하여 다양한 요리에 활용되는 건강 채소입니다.";
            default:
                return null;
        }
    }
    
}



public struct PlantCoordinate
{
    public int x;
    public int y;

    public PlantCoordinate(int x, int y) { this.x = x; this.y = y; }
    
    
}
