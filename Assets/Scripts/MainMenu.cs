using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("Stage1.2");
    }

    // Call this when the Credits button is pressed
    public void LoadCredits()
    {
        SceneManager.LoadScene("Credits");
    }

    // Optional: Exit the game
    public void QuitGame()
    {
        SceneManager.LoadScene("Menu");
    }

}
