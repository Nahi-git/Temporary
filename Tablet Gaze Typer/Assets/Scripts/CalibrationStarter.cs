using UnityEngine;

public class CalibrationStarter : MonoBehaviour
{
    public UnityGazeCalibrator calibrator;
    
    void Start()
    {
        //auto-find calibrator if not assigned
        if (calibrator == null)
        {
            calibrator = FindObjectOfType<UnityGazeCalibrator>();
        }
    }
    
    public void StartCalibration()
    {
        if (calibrator == null)
        {
            calibrator = FindObjectOfType<UnityGazeCalibrator>();
        }
        
        if (calibrator != null)
        {
            calibrator.StartCalibration();
            gameObject.SetActive(false);
        }
    }
}
