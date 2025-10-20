using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [TextArea(3, 10)]
    public string[] dialogueLines;
    private bool hasPlayed = false;



    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Something entered the trigger: " + other.name);

        if (other.CompareTag("Player") && !hasPlayed)
        {
            Debug.Log("Player entered trigger, attempting to start dialogue...");

            Dialogue dialogueManager = FindObjectOfType<Dialogue>();
            if (dialogueManager != null)
            {
                Debug.Log("Dialogue manager found! Starting dialogue.");
                dialogueManager.StartDialogue(dialogueLines);
                hasPlayed = true;
            }
            else
            {
                Debug.LogWarning("No Dialogue Manager found in scene!");
            }
        }
    }
}
