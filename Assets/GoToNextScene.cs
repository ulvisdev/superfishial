using UnityEngine;
using UnityEngine.SceneManagement; 
using System.Collections;

public class GoToNextScene : MonoBehaviour
{

    void Start()
    {
        StartCoroutine(NextScene());
    }


    void Update()
    {
        
    }

    IEnumerator NextScene()
    {
        yield return new WaitForSeconds(32f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}