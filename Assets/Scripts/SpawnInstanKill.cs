using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnInstanKill : MonoBehaviour
{
    private bool triggered = false;

    public GameObject boulder;

    public Transform spawner;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //if (!triggered) { Spawn(); }
        
    }

    void Spawn() {
        Instantiate(boulder, spawner.position, boulder.transform.rotation);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !triggered)
        {
            triggered = true;
            Spawn();
        }

    }
}
