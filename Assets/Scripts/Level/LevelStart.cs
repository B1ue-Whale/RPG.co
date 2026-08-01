using UnityEngine;

public class LevelStart : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private Transform spawnPoint;

    private void Start()
    {
        player.transform.position = spawnPoint.position;
       // PlayEntranceSequence(); 아직 안 만듬 
    }
}