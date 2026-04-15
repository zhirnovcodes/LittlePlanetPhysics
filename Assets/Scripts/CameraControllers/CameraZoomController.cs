using UnityEngine;

public class CameraZoomController : MonoBehaviour
{
    [Range(0, 1)]
    public float Position;
    public bool IsLogarythmic;

    public Transform CameraDepthHandle;

    public float PlanetScale = 10;
    public float MinDepth = 3;
    public float MaxVisibleEdge = 1;
    public float MaxDepth = 20;

    private float MinZ = 3 + 5;
    private float MaxZ = 20 + 5;

    private void Update()
    {
        var t = IsLogarythmic ? Mathf.Sqrt( Position ) : Position;
        var z = Mathf.Lerp(MaxDepth, MinDepth, t) + PlanetScale / 2f; 
        //var z = Mathf.Lerp(MaxZ, MinZ, t); 

        CameraDepthHandle.localPosition = new Vector3(0, 0, -z);
    }


    public void SetPlanetScale(float scale)
    {
        PlanetScale = scale;
        MinZ = PlanetScale / 2f + MinDepth;
        
        var camera = CameraDepthHandle.GetComponentInChildren<Camera>();
        var angle = camera.fieldOfView / 2f;

        MaxZ = PlanetScale / 2f + (PlanetScale / 2f + MaxVisibleEdge) / Mathf.Sin(Mathf.Deg2Rad * angle / 2f);
    }
}