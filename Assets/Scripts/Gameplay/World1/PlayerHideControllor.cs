using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerHideController : MonoBehaviour
{
    [SerializeField] private GameObject graphicsRoot;
    [SerializeField] private Transform hideCheckPoint;
    [SerializeField] private HideableTilemap hideableTilemap;

    public bool IsHidden { get; private set; }

    private Vector3Int _currentHideCell;
    private Collider2D _bodyCollider;
    private Rigidbody2D _rigidbody;
    private RigidbodyConstraints2D _savedConstraints;

    private void Awake()
    {
        _bodyCollider = GetComponent<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    public Vector3Int GetCurrentHideCell() => _currentHideCell;

    public bool CanHideHere()
    {
        if (hideableTilemap == null || hideCheckPoint == null)
        {
            Debug.Log("인식안됨");
            return false;
        }    
        Debug.Log("CanHideHere 호출됨");
        _currentHideCell = hideableTilemap.WorldToCell(hideCheckPoint.position);
        return hideableTilemap.HasHideTile(_currentHideCell);
    }

    public void EnterHide()
    {
        if (IsHidden || !CanHideHere())
        {
            Debug.Log("EnterHide 인식되었으나 조건 안맞아 취소");
            return;
        }
            
        Debug.Log("EnterHide 호출됨");
        IsHidden = true;
        SetGraphicsVisible(false);
        SetBodyCollisionActive(false);
        hideableTilemap.ShowHighlight(_currentHideCell);
    }

    public void ExitHide()
    {
        if (!IsHidden)
            return;

        Debug.Log("ExitHide 호출됨");
        IsHidden = false;
        SetGraphicsVisible(true);
        SetBodyCollisionActive(true);
        hideableTilemap.ClearHighlight();
    }

    private void SetBodyCollisionActive(bool active)
    {
        if (_bodyCollider != null)
            _bodyCollider.enabled = active;

        if (_rigidbody == null)
            return;

        if (active)
        {
            _rigidbody.constraints = _savedConstraints;
            return;
        }

        _savedConstraints = _rigidbody.constraints;
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void SetGraphicsVisible(bool visible)
    {
        if (graphicsRoot != null)
            graphicsRoot.SetActive(visible);
    }
}