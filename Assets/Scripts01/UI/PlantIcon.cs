using UnityEngine;

public class PlantIcon : MonoBehaviour
{
    private Camera userCam;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        userCam = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        transform.LookAt(userCam.transform);
    }
}
