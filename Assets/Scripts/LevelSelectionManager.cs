using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectionManager : MonoBehaviour
{
    public void SelectUnterstützend()
    {
        LevelSelector.SelectedPromptVariant = "unterstützend";
        SceneManager.LoadScene(2);
    }

    public void SelectNeutral()
    {
        LevelSelector.SelectedPromptVariant = "neutral";
        SceneManager.LoadScene(2);
    }

    public void SelectStreng()
    {
        LevelSelector.SelectedPromptVariant = "streng";
        SceneManager.LoadScene(2);
    }

    public void ToggleAnswerLength(bool isOn)
    {
        LevelSelector.LongAnswers = isOn;
        Debug.Log("Long answers: " + isOn);
    }
}
