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

    /// <summary>Whether the given cell (already selected by the caller, e.g. the nearest
    /// infected tile) is a valid hide/interact tile - the same check CanHideHere() does
    /// for the fixed hideCheckPoint cell, but for an arbitrary cell.</summary>
    public bool CanHideAt(Vector3Int cell)
    {
        return hideableTilemap != null && hideableTilemap.HasHideTile(cell);
    }

    public void EnterHide()
    {
        if (IsHidden || !CanHideHere())
        {
            Debug.Log("EnterHide 인식되었으나 조건 안맞아 취소");
            return;
        }

        EnterHideAt(_currentHideCell);
    }

    /// <summary>
    /// Same positioning/occupation logic as EnterHide() (hide the sprite, disable body
    /// collision, highlight the cell) but for an already-selected cell rather than the
    /// fixed hideCheckPoint cell below the player. Used for auto-targeted interactions
    /// (e.g. E-only nearest-bug targeting) that pick their own cell.
    /// </summary>
    public void EnterHideAt(Vector3Int cell)
    {
        if (IsHidden)
        {
            return;
        }

        Debug.Log("EnterHideAt 호출됨");
        _currentHideCell = cell;
        IsHidden = true;
        SetGraphicsVisible(false);
        SetBodyCollisionActive(false);
        hideableTilemap.ShowHighlight(cell);
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