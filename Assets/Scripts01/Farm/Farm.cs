using System;
using UnityEngine;

public class Farm : MonoBehaviour
{
    public static Farm instance;
    
    public MeshRenderer meshRenderer;
    public PlantsManager plantsManager;
    
    public float farmBreadth;
    public float farmWidth;

    public float cellSize = 2f;
    
    public GameObject groundPiece;

    public bool isInitialized = false;

    private void Awake()
    {
        if (null == instance)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
        
        meshRenderer = GetComponent<MeshRenderer>();
        plantsManager = GetComponent<PlantsManager>();
    }

    private void Start()
    {
        meshRenderer.enabled = false;
    }

    private Vector3 groundPosition(int x, int z)
    {
        Vector3 topLeftCorner = new Vector3(
            transform.position.x - (farmWidth / 2f), 
            transform.position.y,
            transform.position.z - (farmBreadth / 2f));

        float normalizedX = (topLeftCorner.x + ((cellSize / 2f) + (x * cellSize)));
        float normalizedZ = (topLeftCorner.z + ((cellSize / 2f) + (z * cellSize)));

        return new Vector3(normalizedX, transform.position.y + 0.01f, normalizedZ);
    }
    
    public void InitializeFarm()
    {
        for (int x = 0; x < (int)(farmWidth / cellSize); x++)
        {
            for (int z = 0; z < (int)(farmBreadth / cellSize); z++)
            {
                GameObject instantiatedGround = Instantiate(groundPiece, groundPosition(x, z), Quaternion.identity);
            }
        }
    }
}
