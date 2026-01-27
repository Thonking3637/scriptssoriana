using UnityEngine;

public class ProductAndCameraController : MonoBehaviour
{
    public float rotationSpeed = 100f;
    public SmoothCameraController cameraController;

    private bool rotatingLeft, rotatingRight;

    private enum CameraView { Front, Top }
    private CameraView currentView = CameraView.Front;

    void Update()
    {
        if (rotatingLeft)
            transform.Rotate(0, -rotationSpeed * Time.deltaTime, 0, Space.Self);

        if (rotatingRight)
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.Self);
    }

    public void SetRotateLeft(bool state)
    {
        rotatingLeft = state;

        if (state)
            FindObjectOfType<VerificacionMercanciaActivity>().OnVistaLeftPressed();
    }

    public void SetRotateRight(bool state)
    {
        rotatingRight = state;

        if (state)
            FindObjectOfType<VerificacionMercanciaActivity>().OnVistaRightPressed();
    }

    public void OnUpButtonPressed()
    {
        if (currentView != CameraView.Top)
        {
            cameraController.MoveToPosition("A5 C1 VistaTop", () => {
                currentView = CameraView.Top;
                FindObjectOfType<VerificacionMercanciaActivity>().OnVistaTopPressed();
            });
        }
    }

    public void OnDownButtonPressed()
    {
        if (currentView == CameraView.Top)
        {
            cameraController.MoveToPosition("A5 C1 VistaFrontal", () => {
                currentView = CameraView.Front;
                FindObjectOfType<VerificacionMercanciaActivity>().OnVistaFrontPressed();
            });
        }
    }
}
