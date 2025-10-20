using System.Collections;
using UnityEngine;
using TMPro;

public class Dialogue : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI textComponent;
    public GameObject dialogueBox; // ? reference to your background canvas

    [Header("Dialogue Settings")]
    public float textSpeed = 0.05f;

    private string[] lines;
    private int index;
    private bool isPlaying = false;

    void Start()
    {
        textComponent.text = string.Empty;
        textComponent.gameObject.SetActive(false);

        // ? Make sure background starts hidden
        if (dialogueBox != null)
            dialogueBox.SetActive(false);
    }

    void Update()
    {
        if (!isPlaying) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (textComponent.text == lines[index])
            {
                NextLine();
            }
            else
            {
                StopAllCoroutines();
                textComponent.text = lines[index];
            }
        }
    }

    public void StartDialogue(string[] newLines)
    {
        lines = newLines;
        index = 0;
        textComponent.text = string.Empty;

        // ? Enable text and background
        textComponent.gameObject.SetActive(true);
        if (dialogueBox != null)
            dialogueBox.SetActive(true);

        isPlaying = true;
        StartCoroutine(TypeLine());
    }

    IEnumerator TypeLine()
    {
        textComponent.text = "";
        foreach (char c in lines[index].ToCharArray())
        {
            textComponent.text += c;
            yield return new WaitForSeconds(textSpeed);
        }
    }

    void NextLine()
    {
        if (index < lines.Length - 1)
        {
            index++;
            textComponent.text = string.Empty;
            StartCoroutine(TypeLine());
        }
        else
        {
            EndDialogue();
        }
    }

    void EndDialogue()
    {
        textComponent.text = "";
        textComponent.gameObject.SetActive(false);

        // ? Disable background once dialogue is finished
        if (dialogueBox != null)
            dialogueBox.SetActive(false);

        isPlaying = false;
    }
}
