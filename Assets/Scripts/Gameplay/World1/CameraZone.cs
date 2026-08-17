using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

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

    //구본환 8.16 존 전환 시 배경 교체 + 이전 배경 잠시 유지
    [Tooltip("Optional. Background shown while this zone is active. Other zone backgrounds are hidden.")]
    [SerializeField] private GameObject backgroundRoot;

    [Tooltip("How long the previous background stays visible after a zone switch.")]
    [FormerlySerializedAs("backgroundFadeDuration")]
    [SerializeField] private float backgroundHoldDuration = 1f;

    private static GameObject activeBackground;
    private static Coroutine pendingHide;
    private static CameraZone pendingHideHost;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        zoneCamera.Prioritize();
        SwitchBackground(); //구본환 8.16
    }

    //구본환 8.16
    private void SwitchBackground()
    {
        if (backgroundRoot == null)
            return;

        if (activeBackground == backgroundRoot)
        {
            // 숨기기 대기 중에 같은 존으로 돌아오면 숨기기를 취소한다
            StopPendingHide();
            ShowBackground(backgroundRoot);
            return;
        }

        GameObject previous = activeBackground;
        ShowBackground(backgroundRoot);
        activeBackground = backgroundRoot;

        if (previous != null)
        {
            StopPendingHide();
            pendingHideHost = this;
            pendingHide = StartCoroutine(HideAfterDelay(previous));
        }
    }

    //구본환 8.16
    private static void ShowBackground(GameObject root)
    {
        root.SetActive(true);

        Background[] layers = root.GetComponentsInChildren<Background>(true);
        for (int i = 0; i < layers.Length; i++)
            layers[i].gameObject.SetActive(true);
    }

    //구본환 8.16
    private IEnumerator HideAfterDelay(GameObject toHide)
    {
        yield return new WaitForSeconds(backgroundHoldDuration);

        if (toHide != activeBackground)
            toHide.SetActive(false);

        pendingHide = null;
        pendingHideHost = null;
    }

    //구본환 8.16
    private static void StopPendingHide()
    {
        if (pendingHide != null && pendingHideHost != null)
            pendingHideHost.StopCoroutine(pendingHide);

        pendingHide = null;
        pendingHideHost = null;
    }
}
