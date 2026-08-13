using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class BugZone : MonoBehaviour
{
    [SerializeField] private int bugCount = 3;

    
    [SerializeField] private float range = 5f;


    [SerializeField] private Tilemap targetTilemap;
    

    private BoxCollider2D bugArea;
    List<Vector3Int> infectedTiles = new List<Vector3Int>();
    
    // 오염 전 타일 색 저장
    private Dictionary<Vector3Int, Color> originalColors
        = new Dictionary<Vector3Int, Color>();
    private void Awake()
    {
        bugArea = GetComponent<BoxCollider2D>();

        // range가 5라면 전체 크기는 10 x 10
        bugArea.size = new Vector2(range * 2f, range * 2f);
        bugArea.isTrigger = true;
    }

    private void Start()
    {
        InfectTiles();
    }

    private void InfectTiles()
    {
        List<Vector3Int> tilesInArea = GetTilesInArea();
        int infectCount = Mathf.Min(bugCount, tilesInArea.Count);

        for (int i = 0; i < infectCount; i++)
        {
            int randomIndex = Random.Range(0, tilesInArea.Count);

            Vector3Int cellPosition = tilesInArea[randomIndex];

            originalColors[cellPosition] = targetTilemap.GetColor(cellPosition);

            targetTilemap.SetTileFlags(cellPosition, TileFlags.None);
            targetTilemap.SetColor(cellPosition, Color.magenta);
            infectedTiles.Add(cellPosition);
            // 같은 타일이 또 뽑히지 않게 제거
            tilesInArea.RemoveAt(randomIndex);
        }
    }

    private List<Vector3Int> GetTilesInArea()
    {
        List<Vector3Int> tiles = new List<Vector3Int>();

        Bounds bounds = bugArea.bounds;

        Vector3Int minCell = targetTilemap.WorldToCell(bounds.min);
        Vector3Int maxCell = targetTilemap.WorldToCell(bounds.max);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cellPosition = new Vector3Int(x, y, 0);

                if (targetTilemap.HasTile(cellPosition))
                {
                    tiles.Add(cellPosition);
                }
            }
        }

        return tiles;
    }
    public bool isInfected(Vector3Int cell)
    {
        return infectedTiles.Contains(cell);
    }
    public void ClearInfection(Vector3Int cell)
    {
        if (!infectedTiles.Contains(cell))
        {
            return; //아무것도 없음 
        }

        targetTilemap.SetColor(cell, originalColors[cell]);
       
        infectedTiles.Remove(cell);
        //destroy 할 필요 없을듯 
    }
}