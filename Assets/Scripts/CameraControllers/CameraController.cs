using UnityEngine;

public class CameraController : MonoBehaviour
{
    public CameraKeyboardController KeyboardController;
    public CameraZoomController ZoomController;
    public CameraMouseController MouseController;
    public CameraFollowController FollowController;

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    public void Enable()
    {
        KeyboardController.enabled = true;
        MouseController.enabled = true;
    }

    public void Disable()
    {
        KeyboardController.enabled = false;
        MouseController.enabled = false;
    }

    public void SetPlanetScale(float scale)
    {
        ZoomController.SetPlanetScale(scale);
    }


    public void SetFollowingPosition(Vector3 position)
    {
        FollowController.SetFollowingPosition(position);
    }
}
