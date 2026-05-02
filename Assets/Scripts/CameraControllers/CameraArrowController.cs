using UnityEngine;
using UnityEngine.InputSystem;

public class CameraArrowController : MonoBehaviour
{
    public Transform ZoomHandler;
    public Transform YRotationHandler;
    public Transform InclineController;

    public Vector2 ZoomRange = new Vector2(-5f, 5f);
    public float ZoomSpeed = 5f;
    public float ZoomSmoothness = 5f;
    public float MouseWheelZoomSpeed = 3f;
    public float RotationSpeed = 100f;
    public float RotationSmoothness = 5f;
    public float MouseDragRotationSpeed = 0.3f;
    public Vector2 InclineRange = new Vector2(-80f, 0f);
    public float InclineMouseSpeed = 0.3f;
    public float InclineSmoothness = 5f;

    private float TargetZoomZ;
    private float CurrentYRotation;
    private float TargetInclineX;

    private void Start()
    {
        TargetZoomZ = ZoomHandler.localPosition.z;
        CurrentYRotation = YRotationHandler.localEulerAngles.y;
        TargetInclineX = InclineController.localEulerAngles.x;
    }

    private void Update()
    {
        HandleZoom();
        HandleYRotation();
        HandleIncline();
    }

    private void HandleZoom()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            if (keyboard.upArrowKey.isPressed)
            {
                TargetZoomZ += ZoomSpeed * Time.deltaTime;
            }

            if (keyboard.downArrowKey.isPressed)
            {
                TargetZoomZ -= ZoomSpeed * Time.deltaTime;
            }
        }

        if (mouse != null)
        {
            float scrollDelta = mouse.scroll.ReadValue().y;
            if (scrollDelta != 0f)
            {
                TargetZoomZ += scrollDelta * MouseWheelZoomSpeed * Time.deltaTime;
            }
        }

        TargetZoomZ = Mathf.Clamp(TargetZoomZ, ZoomRange.x, ZoomRange.y);

        Vector3 zoomPosition = ZoomHandler.localPosition;
        zoomPosition.z = Mathf.Lerp(zoomPosition.z, TargetZoomZ, ZoomSmoothness * Time.deltaTime);
        ZoomHandler.localPosition = zoomPosition;
    }

    private void HandleYRotation()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed)
            {
                CurrentYRotation -= RotationSpeed * Time.deltaTime;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                CurrentYRotation += RotationSpeed * Time.deltaTime;
            }
        }

        if (mouse != null && mouse.leftButton.isPressed)
        {
            float mouseDeltaX = mouse.delta.ReadValue().x;
            CurrentYRotation += mouseDeltaX * MouseDragRotationSpeed;
        }

        float smoothedY = Mathf.LerpAngle(YRotationHandler.localEulerAngles.y, CurrentYRotation, RotationSmoothness * Time.deltaTime);
        YRotationHandler.localEulerAngles = new Vector3(YRotationHandler.localEulerAngles.x, smoothedY, YRotationHandler.localEulerAngles.z);
    }

    private void HandleIncline()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            float mouseDeltaY = mouse.delta.ReadValue().y;
            TargetInclineX -= mouseDeltaY * InclineMouseSpeed;
        }

        TargetInclineX = Mathf.Clamp(TargetInclineX, InclineRange.x, InclineRange.y);

        float smoothedX = Mathf.LerpAngle(InclineController.localEulerAngles.x, TargetInclineX, InclineSmoothness * Time.deltaTime);
        InclineController.localEulerAngles = new Vector3(smoothedX, InclineController.localEulerAngles.y, InclineController.localEulerAngles.z);
    }
}
