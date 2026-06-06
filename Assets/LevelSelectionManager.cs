using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectionManager : MonoBehaviour
{
    public void SelectUnterstützend()
    {
        LevelSelector.SelectedPromptVariant = "unterstützend";
        SceneManager.LoadScene(1);
    }

    public void SelectNeutral()
    {
        LevelSelector.SelectedPromptVariant = "neutral";
        SceneManager.LoadScene(1);
    }

    public void SelectStreng()
    {
        LevelSelector.SelectedPromptVariant = "streng";
        SceneManager.LoadScene(1);
    }
}
