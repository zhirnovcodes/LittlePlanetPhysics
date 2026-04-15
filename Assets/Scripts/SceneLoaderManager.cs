using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneLoaderManager : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            LoadNextScene();
        }
    }

    private void LoadNextScene()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        int next = (current + 1) % SceneManager.sceneCountInBuildSettings;
        SceneManager.LoadScene(next);
    }
}
