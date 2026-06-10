using UnityEngine;
using UnityEngine.SceneManagement;

public class BriefingSceneManager : MonoBehaviour
{
  [SerializeField]
  public Scene LoadScene;
    public void OnWeiterClicked()
    {
        SceneManager.LoadScene(1);
    }
}
