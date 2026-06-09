using UnityEngine;
using UnityEngine.SceneManagement;

public class BriefingSceneManager : MonoBehaviour
{
    public void OnWeiterClicked()
    {
        SceneManager.LoadScene(1);
    }
}
