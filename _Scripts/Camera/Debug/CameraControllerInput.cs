using UnityEngine;

public class CameraControllerInput: MonoBehaviour
{
    public SmoothCameraController cameraController;

    private void Update()
    {

        if (Input.GetKey(KeyCode.A))
        {
            cameraController.MoveToNext();
            Debug.Log("Presionando 1 ");
        }
        else if (Input.GetKey(KeyCode.S))
        {
            cameraController.MoveToPrevious();
            Debug.Log("Presionando 2 ");
        } 
    }
}
