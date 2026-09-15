using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;

public class GoToNextScene : MonoBehaviour
{

    void Start()
    {
        StartCoroutine(NextScene());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator NextScene()
    {
        yield return new WaitForSeconds(8.3f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}