using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum Tile { Road, Wall, Spawn, Warp };

public class MapGenerator : MonoBehaviour
{
    private static MapGenerator instance;

    [Header("Map Data")]
    [SerializeField] private int width;
    [SerializeField] private int height;

    [SerializeField] private double seed;
    [SerializeField] private bool useRandomSeed;

    [Header("Generating Value")]
    [Range(0, 100)]
    [SerializeField] private int randomFillPercent;
    [SerializeField] private int smoothNum;

    private Tile[,] map;

    [Header("TileMaps")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap roadTilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase roadTile;
    [SerializeField] private TileBase warpTile;
    [SerializeField] private TileBase spawnTile;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(this);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Start()
    {
        GenerateMap();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            wallTilemap.ClearAllTiles();
            roadTilemap.ClearAllTiles();
            GenerateMap();
        }
    }

    #region MapGenerator
    public void GenerateMap()
    {
        map = new Tile[width, height];
        MapRandomFill();

        for (int i = 0; i < smoothNum; i++)
            SmoothMap();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                OnDrawTile(x, y); //Ÿ�� ����
            }
        }

        RandomSpawnAndWarpPoint();
    }

    private void MapRandomFill() //���� ������ ���� �� Ȥ�� �� �������� �����ϰ� ä��� �޼ҵ�
    {
        if (useRandomSeed)
            seed = Time.time; //�õ�

        System.Random pseudoRandom = new System.Random(seed.GetHashCode()); //�õ�� ���� �ǻ� ���� ����

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1) 
                    map[x, y] = Tile.Wall;
                else 
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? Tile.Wall : Tile.Road; //������ ���� �� Ȥ�� �� ���� ����
            }
        }
    }

    private void RandomSpawnAndWarpPoint()
    {
        bool findWarp = false;
        int xRandom = 0;
        int yRandom = 0;

        while (map[xRandom, yRandom] != Tile.Road)
        {
            xRandom = (int)Random.Range(width * 0.1f, width - (width * 0.1f));
            yRandom = (int)Random.Range(height * 0.1f, height - (height * 0.1f));
        }

        map[xRandom, yRandom] = Tile.Spawn;

        Vector3Int spawnPos = new Vector3Int(-width / 2 + xRandom, -height / 2 + yRandom, 0);
        roadTilemap.SetTile(spawnPos, spawnTile);

        for (int x = width / 10; x < width - (width / 10); x++)
        {
            for ( int y = height / 10; y < height - (height / 10); y++)
            {
                float distance = Mathf.Pow(xRandom - x, 2) + Mathf.Pow(yRandom - y, 2);

                if (map[x, y] == Tile.Road && distance == Mathf.Pow((width >= height) ? width * 0.55f : height * 0.55f, 2))
                {
                    findWarp = true;
                    map[x, y] = Tile.Warp;

                    Vector3Int warpPos = new Vector3Int(-width / 2 + x, -height / 2 + y, 0);
                    roadTilemap.SetTile(warpPos, warpTile);

                    break;
                }
            }

            if (findWarp)
                break;
        }
    }

    private void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);
                if (neighbourWallTiles > 4)
                    map[x, y] = Tile.Wall; //�ֺ� ĭ �� ���� 4ĭ�� �ʰ��� ��� ���� Ÿ���� ������ �ٲ�
                else if (neighbourWallTiles < 4) 
                    map[x, y] = Tile.Road; //�ֺ� ĭ �� ���� 4ĭ �̸��� ��� ���� Ÿ���� �� �������� �ٲ�
            }
        }
    }

    private int GetSurroundingWallCount(int x, int y)
    {
        int wallCount = 0;
        for (int neighbourX = x - 1; neighbourX <= x + 1; neighbourX++)
        { //���� ��ǥ�� �������� �ֺ� 8ĭ �˻�
            for (int neighbourY = y - 1; neighbourY <= y + 1; neighbourY++)
            {
                if (neighbourX >= 0 && neighbourX < width && neighbourY >= 0 && neighbourY < height)
                {
                    if (neighbourX != x || neighbourY != y)
                    {
                        if (map[neighbourX, neighbourY] == Tile.Wall || map[neighbourX, neighbourY] == Tile.Road)
                            wallCount += (int)map[neighbourX, neighbourY];
                    }
                }
                else 
                    wallCount++; //�ֺ� Ÿ���� �� ������ ��� ��� wallCount ����
            }
        }
        return wallCount;
    }

    private void OnDrawTile(int x, int y)
    {
        Vector3Int pos = new Vector3Int(-width / 2 + x, -height / 2 + y, 0);
        if (map[x, y] == Tile.Wall)
            wallTilemap.SetTile(pos, wallTile);
        else
            roadTilemap.SetTile(pos, roadTile);
    }
    #endregion
}
