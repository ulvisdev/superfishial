using UnityEngine;

public class Quest3MinigameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private GameObject minigamePanel;

    [Header("Minigame")]
    [SerializeField] private Quest3MiniPlayer miniPlayer;

    void Start()
    {
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(false);
    }

    public void OpenInstructions()
    {
        minigamePanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    public void PressGo()
    {
        instructionsPanel.SetActive(false);
        minigamePanel.SetActive(true);

        Canvas.ForceUpdateCanvases();
        miniPlayer.BeginMinigame();
    }

    public void EndMinigame()
    {
        miniPlayer.StopMinigame();
        minigamePanel.SetActive(false);
        PlayerFreeze.Instance.UnfreezePlayer();
    }
}