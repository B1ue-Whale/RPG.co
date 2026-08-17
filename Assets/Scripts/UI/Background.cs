using Unity.Cinemachine;
using UnityEngine;

//구본환 8.16 패럴랙스 배경 레이어
/// <summary>
/// One component per background layer sprite. The layer follows the camera
/// at parallaxFactor (0 = fixed in the world / closest, 1 = glued to the
/// camera / infinitely far).
/// <para>
/// Updates via CinemachineCore.CameraUpdatedEvent so it always runs after
/// the CinemachineBrain has positioned the camera for the frame.
/// Anchor positions are captured once and reused after a zone switch, so
/// re-enabling a background snaps it to the correct place for the current
/// camera instead of leaving it stranded off-screen.
/// </para>
/// </summary>
public class Background : MonoBehaviour
{
    [Header("Parallax")]
    [SerializeField, Range(0f, 1f)] private float parallaxFactor = 0.8f;

    [Tooltip("If true, the layer only scrolls horizontally and keeps its Y position.")]
    [SerializeField] private bool lockY = true;

    [Tooltip("Repeat the sprite endlessly along X. The sprite must tile seamlessly.")]
    [SerializeField] private bool repeatX = false;

    private Transform cam;
    private Vector3 layerStartPos;
    private Vector3 camStartPos;
    private float spriteWidth;
    private bool anchored;

    private void Awake()
    {
        CaptureAnchor();
    }

    private void OnEnable()
    {
        if (!anchored)
            CaptureAnchor();

        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        ApplyParallax();
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    private void CaptureAnchor()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        layerStartPos = transform.position;
        camStartPos = cam != null ? cam.position : Vector3.zero;
        anchored = true;

        // 렌더러 전체 폭이 아니라 '타일 한 장' 폭 기준으로 감아야
        // Draw Mode: Tiled로 늘려도 한 타일씩 이음새 없이 순환한다
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            spriteWidth = sr.sprite.bounds.size.x * transform.lossyScale.x;
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        ApplyParallax();
    }

    private void ApplyParallax()
    {
        if (cam == null)
        {
            if (Camera.main == null)
                return;
            cam = Camera.main.transform;
        }

        Vector3 camDelta = cam.position - camStartPos;

        float x = layerStartPos.x + camDelta.x * parallaxFactor;
        float y = lockY ? layerStartPos.y : layerStartPos.y + camDelta.y * parallaxFactor;

        if (repeatX && spriteWidth > 0f)
        {
            // 카메라가 스프라이트 한 장 폭 이상 벗어나면 그만큼 앞으로 당겨 무한 반복
            float relativeDistance = camDelta.x * (1f - parallaxFactor);
            x += Mathf.Round(relativeDistance / spriteWidth) * spriteWidth;
        }

        transform.position = new Vector3(x, y, layerStartPos.z);
    }
}
