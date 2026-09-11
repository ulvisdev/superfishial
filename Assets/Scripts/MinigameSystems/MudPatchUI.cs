using UnityEngine;
using UnityEngine.UI;

public class MudPatchUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image mudImage;
    [SerializeField] private Image buriedObjectImage;

    [Header("Mud")]
    [SerializeField] private int clicksToClear = 3;
    [SerializeField] private Sprite[] mudStageSprites;

    private int currentClicks = 0;
    private bool isCleared = false;

    private bool containsRing = false;
    private string resultName = "";
    private Sprite resultSprite;

    private MudSearchManager manager;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (mudImage == null)
            mudImage = GetComponent<Image>();
    }

    public void Setup(MudSearchManager newManager, bool newContainsRing, string newResultName, Sprite newResultSprite)
    {
        manager = newManager;

        containsRing = newContainsRing;
        resultName = newResultName;
        resultSprite = newResultSprite;

        currentClicks = 0;
        isCleared = false;

        button.interactable = true;
        mudImage.enabled = true;

        if (mudStageSprites.Length > 0)
            mudImage.sprite = mudStageSprites[0];

        buriedObjectImage.enabled = false;
        buriedObjectImage.sprite = null;
    }

    public void Dig()
    {
        if (isCleared)
            return;

        SoundEffectManager.Play("MudDig", true);
        
        currentClicks++;

        if (currentClicks >= clicksToClear)
        {
            Reveal();
            return;
        }

        UpdateMudVisual();
    }

    private void UpdateMudVisual()
    {
        if (mudStageSprites.Length == 0)
            return;

        int spriteIndex = currentClicks;
        spriteIndex = Mathf.Clamp(spriteIndex, 0, mudStageSprites.Length - 1);
        mudImage.sprite = mudStageSprites[spriteIndex];
    }

    private void Reveal()
    {
        isCleared = true;
        button.interactable = false;
        mudImage.enabled = false;

        if (resultSprite != null)
        {
            buriedObjectImage.sprite = resultSprite;
            buriedObjectImage.enabled = true;
        }
        else
            buriedObjectImage.enabled = false;

        manager.PatchCleared(containsRing, resultName);
    }
}