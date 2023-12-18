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
    /// Map 생성
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
                OnDrawTile(x, y); //타일 생성
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
    /// 맵을 비율에 따라 벽 혹은 빈 공간으로 랜덤하게 채움
    /// </summary>
    private void MapRandomFill()
    {
        if (useRandomSeed)
            seed = Time.time; //시드

        System.Random pseudoRandom = new System.Random(seed.GetHashCode()); //시드로 부터 의사 난수 생성

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1) 
                    map[x, y] = Tile.Wall;
                else 
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? Tile.Wall : Tile.Room; //비율에 따라 벽 혹은 빈 공간 생성
            }
        }
    }

    /// <summary>
    /// Spawn Point와 Warp Point를 랜덤한 위치에 생성
    /// </summary>
    private void RandomSpawnAndWarpPoint()
    {
        bool findWarp = false;
        int xRandom = 0;
        int yRandom = 0;

        while (map[xRandom, yRandom] != Tile.Room) // Map의 선택된 랜덤타일이 Room 타일일때까지 반복
        {
            xRandom = (int)UnityEngine.Random.Range(width * 0.1f, width - (width * 0.1f));
            yRandom = (int)UnityEngine.Random.Range(height * 0.1f, height - (height * 0.1f));
        }

        map[xRandom, yRandom] = Tile.Spawn;

        Vector3Int spawnPos = new Vector3Int(-width / 2 + xRandom, -height / 2 + yRandom, 0);
        roadTilemap.SetTile(spawnPos, spawnTile);

        for (int x = width / 10; x < width - (width / 10); x++)
        {
            for ( int y = height / 10; y < height - (height / 10); y++)
            {
                float distance = Mathf.Pow(xRandom - x, 2) + Mathf.Pow(yRandom - y, 2);

                if (map[x, y] == Tile.Room && distance == Mathf.Pow((width >= height) ? width * 0.55f : height * 0.55f, 2))
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

    /// <summary>
    /// 무작위로 채워진 wall과 room을 이어나가며 자연스러운 동굴형태로 바꿈
    /// </summary>
    private void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);
                if (neighbourWallTiles > 4)
                    map[x, y] = Tile.Wall; //주변 칸 중 벽이 4칸을 초과할 경우 현재 타일을 벽으로 바꿈
                else if (neighbourWallTiles < 4) 
                    map[x, y] = Tile.Room; //주변 칸 중 벽이 4칸 미만일 경우 현재 타일을 빈 공간으로 바꿈
            }
        }
    }

    /// <summary>
    /// 선택된 타일 주위의 8칸을 확인하고 Wall에 해당하는 칸이 몇칸인지 계산
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
                    wallCount++; //주변 타일이 맵 범위를 벗어날 경우 wallCount 증가
            }
        }
        return wallCount;
    }

    /// <summary>
    /// TileMap에 Tile을 배치
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
    /// Wall과 Room에 해당하는 Regions를 검사하여 일정 최소값을 넘기 못하는 Region을 변경
    /// </summary>
    private void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(Tile.Wall); // Wall에 해당하는 Regions를 확인
        int wallTresholdSize = 50;

        foreach (List<Coord> wallRegion in wallRegions)
        {
            if (wallRegion.Count < wallTresholdSize) // 만약 wallRegion이 최소값을 넘기지 못하면 Room Tile로 교체
            {
                foreach (Coord tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = Tile.Room;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(Tile.Room); // Room에 해당하는 Regions를 확인
        int roomTresholdSize = 50;
        List<Room> survivingRooms = new List<Room>(); // Wall Tile로 바뀌지 않은 Room들을 저장할 리스트

        foreach (List<Coord> roomRegion in roomRegions)
        {
            if (roomRegion.Count < roomTresholdSize) // 만약 roomREgion이 최소값을 넘기지 못하면 Wall Tile로 교체
            {
                foreach (Coord tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = Tile.Wall;
                }
            }
            else // 만약 최소값을 넘겼다면 survivingRooms 리스트에 해당 Room을 저장
            {
                survivingRooms.Add(new Room(roomRegion, map));
            }
        }

        // survivingRooms에 있는 방을 전부 이어줌
        survivingRooms.Sort();
        survivingRooms[0].isMainRoom = true;
        survivingRooms[0].isAccessibleFromMainRoom = true;
        ConnectClosesRooms(survivingRooms);
    }

    /// <summary>
    /// Map에서 Region들을 찾아내어 반환
    /// </summary>
    /// <param name="tileType"></param>
    /// <returns>Double List of Regions</returns>
    private List<List<Coord>> GetRegions(Tile tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>(); // 찾아낸 Region들을 저장할 이차원 리스트 선언
        int[,] mapFlags = new int[width, height]; // Region을 판정할 이차원배열 mapFlags선언, Region에 해당하면 1, 아니면 0

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType) // 만약 현재 좌표의 mapFlags가 0이고, tileType이 매개변수와 같을 경우 
                {
                    // tileType에 해당하는 Tile들을 받아와서 regions에 저장한 후 mapFlags 배열에서 newRegion에 해당하는 좌표들을 1로 변경
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    /// <summary>
    /// startX, startY부터 시작하여 tileType에 해당하는 Tile을 확인하여 Region설정이 가능한 타일을 반환
    /// </summary>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <returns>List of RegionTiles</returns>
    private List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>(); // Region으로 설정할 수 있는 Tile들을 저장할 리스트 선언
        int[,] mapFlags = new int[width, height];
        Tile tileType = map[startX, startY];

        // BFS
        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    // startX, startY부터 상하좌우 칸을 확인하여 mapFlags가 0이고 tileType이 같으면 mapFlags를 1로 바꾸고 큐에 해당 좌표를 삽입
                    if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
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
    /// 방과 방 사이를 잇기 위해 Room에 대한 함수를 저장할 Room Class, Class로 지정한 이유는 Sort사용을 위해 IComparable 인터페이스를 적용하기 위함
    /// </summary>
    private class Room : IComparable<Room>
    {
        public List<Coord> tiles; // room에 해당하는 tile들을 저장할 리스트
        public List<Coord> edgeTiles; // room의 가장자리에 해당하는 tile들을 저장할 리스트
        public List<Room> connectedRooms; // 현재 room에서 연결되어있는 room들을 저장할 리스트
        public int roomSize;
        public bool isAccessibleFromMainRoom; // MainRoom에서 부터 이어질 수 있는지를 판정할 bool변수
        public bool isMainRoom;

        public Room() { }

        public Room(List<Coord> roomTiles, Tile[,] map) // 룸 생성자
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
                        // tile의 상하좌우를 확인하여 만약 해당타일이 Wall Tile이라면 가장자리로 판단하고 edgeTiles에 저장
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
        /// Room과 Room을 이어주는 함수
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
        /// MainRoom에서 Accessible하지 않은 Room들을 Accessiable하도록 바꿈
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

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }

        /// <summary>
        /// 현재 룸에서 otherRoom이 이어져 있는지를 판단
        /// </summary>
        /// <param name="otherRoom"></param>
        /// <returns>Is connectedRoom contain otherRoom</returns>
        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }
    }

    /// <summary>
    /// 닫혀있는 방들을 연결
    /// </summary>
    /// <param name="allRooms"></param>
    /// <param name="forceAccessibilityFromMainRoom"> MainRoom에서 강제로 Accessible하도록 할것인지 </param>
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

        int bestDistance = 0; // 가장 짧은 거리
        Coord bestTileA = new Coord(); // 연결 가능한 가장 가까운 좌표
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room(); // 연결 가능한 가장 가까운 Room
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false; // 연결이 가능한지 판단하는 bool 변수

        foreach (Room roomA in roomListA)
        {
            if (!forceAccessibilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0) // 만약 roomA에 연결된 방이 있다면 continue
                    continue;
            }

            foreach (Room roomB in roomListB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB)) // roomA가 roomB이거나 roomB와 연결되어 있다면 continue
                    continue;

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        // roomA와 roomB의 edgeTile의 좌표 확인
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];

                        // 방들의 거리 계산 (피타고라스)
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            // 만약 서로 방까지의 거리가 bestDistance보다 작거나 possibleConnectionFound가 false라면
                            // 방까지의 거리를 bestDistance로 설정하고 가장 가까운 Tile과 Room을 갱신
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

            // 만약 possibleConnectionFound가 true이고 forceAccessibilityFromMainRoom이 false라면 bestRoomA와 bestRoomB사이에 길 생성
            if (possibleConnectionFound && !forceAccessibilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

        // 만약 possibleConnectionFound가 true이고 forceAccessibilityFromMainRoom이 true라면
        // bestRoomA와 bestRoomB사이에 길을 생성하고 연결할 다른 방을 탐색
        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosesRooms(allRooms, true);
        }

        // forceAccessibilityFromMainRoom이 false라면 연결할 다른 방을 탐색
        if (!forceAccessibilityFromMainRoom)
        {
            ConnectClosesRooms(allRooms, true);
        }
    }

    /// <summary>
    /// room A와 B 사이의 길을 생성
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
    /// 시작 좌표에서 끝좌표까지 이은 선을 구성하는 Tile을 반환
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
        int step = Math.Sign(dx);
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
