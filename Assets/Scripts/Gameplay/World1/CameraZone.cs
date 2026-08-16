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

    [Tooltip("Optional. Background shown while this zone is active. Other zone backgrounds are hidden.")]
    [SerializeField] private GameObject backgroundRoot;

    [Tooltip("How long the previous background stays visible after a zone switch.")]
    [FormerlySerializedAs("backgroundFadeDuration")]
    [SerializeField] private float backgroundHoldDuration = 1f;

    // 현재 켜져 있는 배경 (모든 CameraZone이 공유)
    private static GameObject activeBackground;
    private static Coroutine pendingHide;
    private static CameraZone pendingHideHost;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        zoneCamera.Prioritize();
        SwitchBackground();
    }

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

    private static void ShowBackground(GameObject root)
    {
        root.SetActive(true);

        // 이전 페이드가 레이어를 개별로 꺼 둔 상태가 남아 있으면 복구
        Background[] layers = root.GetComponentsInChildren<Background>(true);
        for (int i = 0; i < layers.Length; i++)
            layers[i].gameObject.SetActive(true);
    }

    private IEnumerator HideAfterDelay(GameObject toHide)
    {
        yield return new WaitForSeconds(backgroundHoldDuration);

        if (toHide != activeBackground)
            toHide.SetActive(false);

        pendingHide = null;
        pendingHideHost = null;
    }

    private static void StopPendingHide()
    {
        if (pendingHide != null && pendingHideHost != null)
            pendingHideHost.StopCoroutine(pendingHide);

        pendingHide = null;
        pendingHideHost = null;
    }
}
