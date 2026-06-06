using UnityEngine;

public class BriefingManager : MonoBehaviour
{
    [SerializeField] private GameObject briefingPanel;
    private NegotiationManager negotiationManager;

    void Start()
    {
      negotiationManager = FindFirstObjectByType<NegotiationManager>();
        if (briefingPanel != null)
            briefingPanel.SetActive(true);
    }

    public void OnWeiterClicked()
    {
        if (briefingPanel != null)
            briefingPanel.SetActive(false);
        negotiationManager.StartConversation();
    }
}
