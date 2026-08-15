using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class BugZone : MonoBehaviour
{
    [SerializeField] private int bugCount = 3;

    
    [SerializeField] private float range = 5f;


    [SerializeField] private Tilemap targetTilemap;

    // 감염 표시용 오버레이 (레벨 타일맵보다 위에 그려지는 별도 타일맵) //구본환 8.15
    [SerializeField] private Tilemap infectionTilemap; //구본환 8.15
    // 흰색 스프라이트 타일이어야 틴트 색이 제대로 보임 //구본환 8.15
    [SerializeField] private TileBase infectionTile; //구본환 8.15
    // 감염 표시 색 (알파를 낮춰 반투명 오버레이로 표시) //구본환 8.15
    [SerializeField] private Color infectionColor = new Color(1f, 0f, 1f, 0.35f); //구본환 8.15
    

    private BoxCollider2D bugArea;
    List<Vector3Int> infectedTiles = new List<Vector3Int>();

    //// 오염 전 타일 색 저장 //구본환 8.15 오버레이 방식으로 변경되어 미사용
    //private Dictionary<Vector3Int, Color> originalColors
    //    = new Dictionary<Vector3Int, Color>();

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

            //originalColors[cellPosition] = targetTilemap.GetColor(cellPosition); //구본환 8.15 오버레이 방식으로 변경되어 미사용

            //targetTilemap.SetTileFlags(cellPosition, TileFlags.None); //구본환 8.15 미사용
            //targetTilemap.SetColor(cellPosition, Color.magenta); //구본환 8.15 미사용

            infectionTilemap.SetTile(cellPosition, infectionTile); //구본환 8.15
            infectionTilemap.SetTileFlags(cellPosition, TileFlags.None); //구본환 8.15
            infectionTilemap.SetColor(cellPosition, infectionColor); //구본환 8.15
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

        //targetTilemap.SetColor(cell, originalColors[cell]); //구본환 8.15 오버레이 방식으로 변경되어 미사용

        infectionTilemap.SetTile(cell, null); //구본환 8.15
       
        infectedTiles.Remove(cell);
        //destroy 할 필요 없을듯 
    }
}
