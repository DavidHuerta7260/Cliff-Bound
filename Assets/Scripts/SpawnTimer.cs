using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnTimer : MonoBehaviour
{

    public GameObject boulder;

    public Transform spawner;

    //private bool going = true;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(SpawnRandomPrefabWithCoroutine());

    }

    // Update is called once per frame
    void Update()
    {

    }

    void Spawn()
    {
        Instantiate(boulder, spawner.position, boulder.transform.rotation);
    }

    IEnumerator SpawnRandomPrefabWithCoroutine()
    {
        //add a 3 second dealy before first spawning objects
        yield return new WaitForSeconds(3f);

        while (true)
        {
            Spawn();
            float randomDelay = Random.Range(6.0f, 10.0f);
            yield return new WaitForSeconds(6f);
        }
    }
}
