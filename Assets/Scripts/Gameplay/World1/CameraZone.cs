using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Place on a trigger collider covering one camera region.
/// When the player enters, the assigned CinemachineCamera becomes live
/// and the CinemachineBrain blends to it (using the Brain's Default Blend).
/// All zone cameras must share the same Priority value for this to work.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CameraZone : MonoBehaviour
{
    [SerializeField] private CinemachineCamera zoneCamera;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            zoneCamera.Prioritize();
        }
    }
}
