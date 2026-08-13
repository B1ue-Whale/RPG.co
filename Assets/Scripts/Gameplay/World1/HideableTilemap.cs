using UnityEngine;
using UnityEngine.Tilemaps;

public class HideableTilemap : MonoBehaviour
{
    [SerializeField] private Tilemap hideableTilemap;
    [SerializeField] private Tilemap highlightTilemap;
    [SerializeField] private TileBase highlightTile;

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return hideableTilemap.WorldToCell(worldPosition);
    }

    public bool HasHideTile(Vector3Int cell)
    {
        TileBase tile = hideableTilemap.GetTile(cell);

        Debug.Log("검사 Cell: " + cell);
        Debug.Log("찾은 Tile: " + tile);

        return tile != null;
    }

    public void ShowHighlight(Vector3Int cell)
    {
        if (highlightTilemap == null || highlightTile == null)
            return;

        highlightTilemap.SetTile(cell, highlightTile);
    }

    public void ClearHighlight()
    {
        if (highlightTilemap == null)
            return;

        highlightTilemap.ClearAllTiles();
    }
}