using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum Tile { Room, Wall, Spawn, Warp };

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

    [Header("TileMaps")]
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap roadTilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase roadTile;
    [SerializeField] private TileBase warpTile;
    [SerializeField] private TileBase spawnTile;

    private struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }

    private Tile[,] map;

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

    /// <summary>
    /// Map ����
    /// </summary>
    public void GenerateMap()
    {
        map = new Tile[width, height];
        MapRandomFill();

        for (int i = 0; i < smoothNum; i++)
            SmoothMap();
        
        ProcessMap();
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                OnDrawTile(x, y); //Ÿ�� ����
            }
        }
        
        RandomSpawnAndWarpPoint();
    }

    private bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    #region MapGenerator

    /// <summary>
    /// ���� ������ ���� �� Ȥ�� �� �������� �����ϰ� ä��
    /// </summary>
    private void MapRandomFill()
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
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? Tile.Wall : Tile.Room; //������ ���� �� Ȥ�� �� ���� ����
            }
        }
    }

    /// <summary>
    /// Spawn Point�� Warp Point�� ������ ��ġ�� ����
    /// </summary>
    private void RandomSpawnAndWarpPoint()
    {
        List<Coord> roomTileList = new List<Coord>();
        bool[,] rooms = new bool[width, height];

        Coord firstTile = new Coord(0, 0);
        bool findFirstTile = false;

        // �� ó������ Ž���� Tile ����
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (map[i, j] == Tile.Room)
                {
                    rooms[i, j] = true;
                    findFirstTile = true;
                    firstTile.tileX = i;
                    firstTile.tileY = j;
                    break;
                }
            }

            if (findFirstTile)
                break;
        }

        // BFS
        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(firstTile);
        
        // Room Tile�� ������ Tile�� ����Ʈ�� ������
        while (queue.Count > 0) 
        {
            Coord tile = queue.Dequeue();
            roomTileList.Add(tile);

            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    // �����¿� ĭ�� Ȯ���Ͽ� rooms�� false�̰� tileType�� Room�̸� rooms�� true�� �ٲٰ� ť�� �ش� ��ǥ�� ����
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if (!rooms[x, y] && map[x, y] == Tile.Room)
                        {
                            rooms[x, y] = true;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        int randomSpawnIndex = (int)UnityEngine.Random.Range(0, roomTileList.Count - 1);

        int xSpawnRandom = roomTileList[randomSpawnIndex].tileX;
        int ySpawnRandom = roomTileList[randomSpawnIndex].tileY;

        map[xSpawnRandom, ySpawnRandom] = Tile.Spawn;
    
        Vector3Int spawnPos = new Vector3Int((int)(-width * 0.5f) + xSpawnRandom, (int)(-height * 0.5f) + ySpawnRandom, 0);
        roadTilemap.SetTile(spawnPos, spawnTile);

        List<Coord> warpTileList = new List<Coord>();
        int radius = (int)((width >= height) ? width * 0.55f : height * 0.55f);

        // Spawn Tile�� �������� Radius�� ���ų� �ָ��ִ� Tile���� ����Ʈ�� ����
        for (int i = 0; i < roomTileList.Count; i++)
        {        
            int x = roomTileList[i].tileX;
            int y = roomTileList[i].tileY;

            float distance = Mathf.Pow(xSpawnRandom - x, 2) + Mathf.Pow(ySpawnRandom - y, 2);

            if (distance >= Mathf.Pow(radius, 2))
            {
               warpTileList.Add(new Coord(x, y));
            }
        }

        int randomWarpIndex = (int)UnityEngine.Random.Range(0, warpTileList.Count - 1);

        int xWarpRandom = warpTileList[randomWarpIndex].tileX;
        int yWarpRandom = warpTileList[randomWarpIndex].tileY;

        map[xWarpRandom, yWarpRandom] = Tile.Warp;

        Vector3Int warpPos = new Vector3Int((int)(-width * 0.5f) + xWarpRandom, (int)(-height * 0.5f) + yWarpRandom, 0);
        roadTilemap.SetTile(warpPos, warpTile);
    }

    /// <summary>
    /// �������� ä���� wall�� room�� �̾���� �ڿ������� �������·� �ٲ�
    /// </summary>
    private void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);
                if (neighbourWallTiles > 4)
                    map[x, y] = Tile.Wall; //�ֺ� ĭ �� ���� 4ĭ�� �ʰ��� ��� ���� Ÿ���� Wall�� �ٲ�
                else if (neighbourWallTiles < 4) 
                    map[x, y] = Tile.Room; //�ֺ� ĭ �� ���� 4ĭ �̸��� ��� ���� Ÿ���� Room���� �ٲ�
            }
        }
    }

    /// <summary>
    /// ���õ� Ÿ�� ������ 8ĭ�� Ȯ���ϰ� Wall�� �ش��ϴ� ĭ�� ��ĭ���� ���
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>Wall Tile's Count</returns>
    private int GetSurroundingWallCount(int x, int y)
    {
        int wallCount = 0;
        for (int neighbourX = x - 1; neighbourX <= x + 1; neighbourX++)
        { 
            for (int neighbourY = y - 1; neighbourY <= y + 1; neighbourY++)
            {
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    if (neighbourX != x || neighbourY != y)
                    {
                        if (map[neighbourX, neighbourY] == Tile.Wall || map[neighbourX, neighbourY] == Tile.Room)
                            wallCount += (int)map[neighbourX, neighbourY];
                    }
                }
                else 
                    wallCount++; //�ֺ� Ÿ���� �� ������ ��� ��� wallCount ����
            }
        }
        return wallCount;
    }

    /// <summary>
    /// TileMap�� Tile�� ��ġ
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    private void OnDrawTile(int x, int y)
    {
        Vector3Int pos = new Vector3Int(-width / 2 + x, -height / 2 + y, 0);
        if (map[x, y] == Tile.Wall)
            wallTilemap.SetTile(pos, wallTile);
        else
            roadTilemap.SetTile(pos, roadTile);
    }
    #endregion

    #region Detecting & Correcting Map

    /// <summary>
    /// Wall�� Room�� �ش��ϴ� Regions�� �˻��Ͽ� ���� �ּҰ��� �ѱ� ���ϴ� Region�� ����
    /// </summary>
    private void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(Tile.Wall); // Wall�� �ش��ϴ� Regions�� Ȯ��
        int wallTresholdSize = 50;

        foreach (List<Coord> wallRegion in wallRegions)
        {
            if (wallRegion.Count < wallTresholdSize) // ���� wallRegion�� �ּҰ��� �ѱ��� ���ϸ� Room Tile�� ��ü
            {
                foreach (Coord tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = Tile.Room;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(Tile.Room); // Room�� �ش��ϴ� Regions�� Ȯ��
        int roomTresholdSize = 50;
        List<Room> survivingRooms = new List<Room>(); // Wall Tile�� �ٲ��� ���� Room���� ������ ����Ʈ

        foreach (List<Coord> roomRegion in roomRegions)
        {
            if (roomRegion.Count < roomTresholdSize) // ���� roomRegion�� �ּҰ��� �ѱ��� ���ϸ� Wall Tile�� ��ü
            {
                foreach (Coord tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = Tile.Wall;
                }
            }
            else // ���� �ּҰ��� �Ѱ�ٸ� survivingRooms ����Ʈ�� �ش� Room�� ����
            {
                survivingRooms.Add(new Room(roomRegion, map));
            }
        }

        // survivingRooms�� �ִ� ù��° ���� MainRoom���� �Ͽ� ���� ���� �̾���
        survivingRooms.Sort(); // ���� ū ���� Main Room
        survivingRooms[0].isMainRoom = true;
        survivingRooms[0].isAccessibleFromMainRoom = true;
        ConnectClosesRooms(survivingRooms);
    }

    /// <summary>
    /// Map���� Region���� ã�Ƴ��� ��ȯ
    /// </summary>
    /// <param name="tileType"></param>
    /// <returns>Double List Coordinate of Regions</returns>
    private List<List<Coord>> GetRegions(Tile tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>(); // ã�Ƴ� Region���� ������ ������ ����Ʈ ����
        bool[,] mapFlags = new bool[width, height]; // Region�� ������ �������迭 mapFlags����, Region�� �ش��ϸ� true, �ƴϸ� false

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!mapFlags[x, y] && map[x, y] == tileType) // ���� ���� ��ǥ�� mapFlags�� false�̰�, tileType�� �Ű������� ���� ��� 
                {
                    // tileType�� �ش��ϴ� Tile���� �޾ƿͼ� regions�� ������ �� mapFlags �迭���� newRegion�� �ش��ϴ� ��ǥ���� true�� ����
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = true;
                    }
                }
            }
        }

        return regions;
    }

    /// <summary>
    /// startX, startY���� �����Ͽ� tileType�� �ش��ϴ� Tile�� Ȯ���Ͽ� Region������ ������ Ÿ���� ��ȯ
    /// </summary>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <returns>List of RegionTiles</returns>
    private List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>(); // Region���� ������ �� �ִ� Tile���� ������ ����Ʈ ����
        bool[,] mapFlags = new bool[width, height];
        Tile tileType = map[startX, startY];

        // BFS
        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = true;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    // startX, startY���� �����¿� ĭ�� Ȯ���Ͽ� mapFlags�� false�̰� tileType�� ������ mapFlags�� true�� �ٲٰ� ť�� �ش� ��ǥ�� ����
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if (!mapFlags[x, y] && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = true;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }
    #endregion

    #region RoomConnection

    /// <summary>
    /// ��� �� ���̸� �ձ� ���� Room�� ���� �Լ��� ������ Room Class, Class�� ������ ������ Sort����� ���� IComparable �������̽��� �����ϱ� ����
    /// </summary>
    private class Room : IComparable<Room>
    {
        public List<Coord> tiles; // room�� �ش��ϴ� tile���� ������ ����Ʈ
        public List<Coord> edgeTiles; // room�� �����ڸ��� �ش��ϴ� tile���� ������ ����Ʈ
        public List<Room> connectedRooms; // ���� room���� ����Ǿ��ִ� room���� ������ ����Ʈ
        public int roomSize;
        public bool isAccessibleFromMainRoom; // MainRoom���� ���� �̾��� �� �ִ����� ������ bool����
        public bool isMainRoom;

        public Room() { }

        public Room(List<Coord> roomTiles, Tile[,] map) // �� ������
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();

            edgeTiles = new List<Coord>();
            foreach(Coord tile in tiles)
            {
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                {
                    for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                    {
                        // tile�� �����¿�Ÿ���� Wall Tile�̶�� �����ڸ��� �Ǵ��ϰ� edgeTiles�� ����
                        if (x == tile.tileX || y == tile.tileY)
                        {
                            if (map[x, y] == Tile.Wall)
                                edgeTiles.Add(tile);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Room�� Room�� �̾��ִ� �Լ�
        /// </summary>
        /// <param name="roomA"></param>
        /// <param name="roomB"></param>
        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if (roomA.isAccessibleFromMainRoom)
                roomB.SetAccessibleFromMainRoom();
            else if (roomB.isAccessibleFromMainRoom)
                roomA.SetAccessibleFromMainRoom();

            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        /// <summary>
        /// MainRoom���� Accessible���� ���� Room���� Accessiable�ϵ��� �ٲ�
        /// </summary>
        public void SetAccessibleFromMainRoom()
        {
            if (!isAccessibleFromMainRoom)
            {
                isAccessibleFromMainRoom = true;
                foreach (Room connectedRoom in connectedRooms)
                {
                    connectedRoom.SetAccessibleFromMainRoom();
                }
            }
        }

        /// <summary>
        /// �� Room�� roomSize�� ���Ͽ� ��ȯ
        /// </summary>
        /// <param name="otherRoom"></param>
        /// <returns>RoomSize of Compare to otherRoom</returns>
        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }

        /// <summary>
        /// ���� �뿡�� otherRoom�� �̾��� �ִ����� �Ǵ�
        /// </summary>
        /// <param name="otherRoom"></param>
        /// <returns>Is connectedRoom contain otherRoom</returns>
        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }
    }

    /// <summary>
    /// �����ִ�(���� ������) ����� ����
    /// </summary>
    /// <param name="allRooms"></param>
    /// <param name="forceAccessibilityFromMainRoom"> �ش� Room�� Main Room�� ������ ������ �� �ְ� �Ұ����� </param>
    private void ConnectClosesRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false)
    {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if (forceAccessibilityFromMainRoom)
        {
            foreach (Room room in allRooms)
            {
                if (room.isAccessibleFromMainRoom)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }
        }
        else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int bestDistance = 0; // ���� ª�� �Ÿ�
        Coord bestTileA = new Coord(); // ���� ������ ���� ����� ��ǥ
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room(); // ���� ������ ���� ����� Room
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false; // ������ �������� �Ǵ��ϴ� bool ����

        foreach (Room roomA in roomListA)
        {
            if (!forceAccessibilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0) // ���� roomA�� ����� ���� �ִٸ� continue
                    continue;
            }

            foreach (Room roomB in roomListB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB)) // roomA�� roomB�̰ų� roomB�� ����Ǿ� �ִٸ� continue
                    continue;

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        // roomA�� roomB�� edgeTile�� ��ǥ Ȯ��
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];

                        // ����� �Ÿ� ��� (��Ÿ���)
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            // ���� ���� ������� �Ÿ��� bestDistance���� �۰ų� possibleConnectionFound�� false���
                            // ������� �Ÿ��� bestDistance�� �����ϰ� ���� ����� Tile�� Room�� ����
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }

            // ���� possibleConnectionFound�� true�̰� forceAccessibilityFromMainRoom�� false��� bestRoomA�� bestRoomB���̿� �� ����
            if (possibleConnectionFound && !forceAccessibilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

        // ���� possibleConnectionFound�� true�̰� forceAccessibilityFromMainRoom�� true���
        // bestRoomA�� bestRoomB���̿� ���� �����ϰ� ������ �ٸ� ���� Ž��
        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosesRooms(allRooms, true);
        }

        // forceAccessibilityFromMainRoom�� false��� ������ �ٸ� ���� Ž��
        if (!forceAccessibilityFromMainRoom)
        {
            ConnectClosesRooms(allRooms, true);
        }
    }

    /// <summary>
    /// room A�� B ������ ���� ����
    /// </summary>
    /// <param name="roomA"></param>
    /// <param name="roomB"></param>
    /// <param name="tileA"></param>
    /// <param name="tileB"></param>
    private void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);

        List<Coord> line = GetLine(tileA, tileB);
        foreach (Coord c in line)
        {
            DrawCircle(c, 2);
        }
    }

    private void DrawCircle(Coord c, int r)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= r * r)
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;

                    if (IsInMapRange(drawX, drawY))
                    {
                        map[drawX, drawY] = Tile.Room;
                    }
                }
            }
        }
    }

    /// <summary>
    /// ���� ��ǥ���� ����ǥ���� ���� ���� �����ϴ� Tile�� ��ȯ
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <returns>List of Tile that make up a Line </returns>
    private List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx); // ����� ��� 1, �����ϰ�� -1
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
                y += step;
            else
                x += step;

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                    x += gradientStep;
                else
                    y += gradientStep;

                gradientAccumulation -= longest;
            }
        }
        return line;
    }

    private Vector3 CoordToWorldPoint(Coord tile)
    {
        return new Vector3(-width / 2 + 0.5f + tile.tileX, -height / 2 + 0.5f + tile.tileY);
    }
    #endregion
}
