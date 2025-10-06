using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadNewScene : MonoBehaviour
{

    public string sceneName;

    private bool triggered = false;



    void Start()
    {
    

    }

    // Update is called once per frame
    void Update()
    {
        
    }


   
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !triggered)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                triggered = true;
            }
            else
            {
                Debug.LogWarning("Scene name is not set in LoadNewScene script.");
            }
        }
    }
}
