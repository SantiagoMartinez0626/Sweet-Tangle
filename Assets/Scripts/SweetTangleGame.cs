using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SweetTangleGame : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 8;
    [SerializeField] private float cellSize = 1.1f;
    [SerializeField] private float pieceScale = 0.9f;
    [SerializeField] private float spawnYOffset = 3.5f;

    [Header("Gameplay")]
    [SerializeField] private int pieceTypeCount = 5;
    [SerializeField] private int maxScore = 300;
    [SerializeField] private int pointsPerMove = 3;

    [Header("Animation")]
    [SerializeField] private float swapDuration = 0.14f;
    [SerializeField] private float dropDurationPerCell = 0.028f;
    [SerializeField] private float minDropDuration = 0.02f;
    [SerializeField] private float maxDropDuration = 0.09f;
    [SerializeField] private float clearDuration = 0.06f;
    [SerializeField] private float previewOffsetFactor = 0.28f;

    [Header("Background")]
    [Tooltip("Rectángulo del grid en Background.png (origen arriba-izquierda, en píxeles).")]
    [SerializeField] private int gridPixelX = 377;
    [SerializeField] private int gridPixelYTop = 178;
    [SerializeField] private int gridPixelWidth = 682;
    [SerializeField] private int gridPixelHeight = 679;
    [SerializeField] private float backgroundDepth = 5f;

    private const string PieceSpritesFolder = "Assets/Art/Sprites";
    private const string BackgroundPath = "Assets/Art/Backgrounds/Background.png";

    private readonly Color[] fallbackColors =
    {
        new Color(0.95f, 0.35f, 0.35f),
        new Color(0.33f, 0.65f, 0.95f),
        new Color(0.95f, 0.87f, 0.35f),
        new Color(0.45f, 0.9f, 0.45f),
        new Color(0.8f, 0.45f, 0.95f),
        new Color(1f, 0.6f, 0.2f),
    };

    private TilePiece[,] board;
    private TilePiece selectedPiece;
    private TilePiece dragStartPiece;
    private TilePiece previewTargetPiece;
    private Vector2 dragStartMousePos;
    private bool dragConsumed;
    private bool isBusy;
    private Transform boardRoot;
    private Vector2 boardOrigin;
    private SweetTangleHud hud;
    private int movePiecesCleared;
    private int moveMaxMatchLength;
    private const float DragThresholdPixels = 10f;
    private Vector2Int currentPreviewDirection = Vector2Int.zero;
    private Sprite[] pieceSprites;
    private Sprite backgroundSprite;

    private void Start()
    {
        LoadPieceSprites();
        LoadBackgroundSprite();
        SetupWorld();
        SetupHud();
        BuildBoard();
        StartCoroutine(ClearMatchesUntilStable());
    }

    private void Update()
    {
        HandleClickSwapInput();
        HandleDragInput();
    }

    private void SetupHud()
    {
        hud = GetComponent<SweetTangleHud>();
        if (hud == null)
        {
            hud = gameObject.AddComponent<SweetTangleHud>();
        }

        float boardTopY = boardOrigin.y + (height - 1) * cellSize;
        float boardWidth = width * cellSize;
        hud.Initialize(maxScore, pointsPerMove, boardTopY, boardWidth);
    }

    private void LoadPieceSprites()
    {
        int spriteCount = Mathf.Min(pieceTypeCount, 5);
        pieceSprites = new Sprite[spriteCount];

        for (int i = 0; i < spriteCount; i++)
        {
            string sheetName = $"Sheet {i + 1}";
#if UNITY_EDITOR
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath($"{PieceSpritesFolder}/{sheetName}.png");
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    pieceSprites[i] = sprite;
                    break;
                }
            }
#else
            pieceSprites[i] = Resources.Load<Sprite>($"Art/Sprites/{sheetName}");
#endif
        }
    }

    private void LoadBackgroundSprite()
    {
#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(BackgroundPath);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                backgroundSprite = sprite;
                return;
            }
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (texture != null)
        {
            backgroundSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
#else
        backgroundSprite = Resources.Load<Sprite>("Art/Backgrounds/Background");
#endif
    }

    private void SetupWorld()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraGO = new GameObject("Main Camera");
            mainCamera = cameraGO.AddComponent<Camera>();
            cameraGO.tag = "MainCamera";
        }

        mainCamera.orthographic = true;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.62f, 0.84f, 0.98f);
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        mainCamera.transform.rotation = Quaternion.identity;

        if (mainCamera.GetComponent<SweetTangleCameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<SweetTangleCameraController>();
        }

        boardRoot = new GameObject("BoardRoot").transform;
        boardRoot.SetParent(transform);
        boardRoot.localPosition = Vector3.zero;

        boardOrigin = new Vector2(
            -((width - 1) * cellSize) * 0.5f,
            -((height - 1) * cellSize) * 0.5f
        );

        SpriteRenderer backgroundRenderer = BuildBackground();
        FitCameraToBackground(mainCamera, backgroundRenderer);
    }

    private SpriteRenderer BuildBackground()
    {
        if (backgroundSprite == null)
        {
            return null;
        }

        float ppu = backgroundSprite.pixelsPerUnit;
        float cellWidthPx = gridPixelWidth / (float)width;
        float cellHeightPx = gridPixelHeight / (float)height;
        float scaleX = cellSize / (cellWidthPx / ppu);
        float scaleY = cellSize / (cellHeightPx / ppu);

        Vector2 spriteLocalCell00 = GetGridCellCenterInSpriteLocal(0, 0);
        Vector3 worldCell00 = new Vector3(boardOrigin.x, boardOrigin.y, 0f);

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(transform);
        backgroundObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        backgroundObject.transform.localPosition = new Vector3(
            worldCell00.x - spriteLocalCell00.x * scaleX,
            worldCell00.y - spriteLocalCell00.y * scaleY,
            backgroundDepth
        );

        SpriteRenderer backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = backgroundSprite;
        backgroundRenderer.sortingOrder = -20;
        return backgroundRenderer;
    }

    private Vector2 GetGridCellCenterInSpriteLocal(int gridX, int gridY)
    {
        float cellWidthPx = gridPixelWidth / (float)width;
        float cellHeightPx = gridPixelHeight / (float)height;
        float pixelX = gridPixelX + (gridX + 0.5f) * cellWidthPx;
        float pixelYTop = gridPixelYTop + gridPixelHeight - (gridY + 0.5f) * cellHeightPx;
        return PixelTopLeftToSpriteLocal(pixelX, pixelYTop);
    }

    private Vector2 PixelTopLeftToSpriteLocal(float pixelX, float pixelYTop)
    {
        float textureWidth = backgroundSprite.texture.width;
        float textureHeight = backgroundSprite.texture.height;
        float u = pixelX / textureWidth;
        float v = (textureHeight - pixelYTop) / textureHeight;
        Vector2 spriteSize = backgroundSprite.bounds.size;
        return new Vector2((u - 0.5f) * spriteSize.x, (v - 0.5f) * spriteSize.y);
    }

    private void FitCameraToBackground(Camera camera, SpriteRenderer backgroundRenderer)
    {
        float aspect = camera.aspect > 0f ? camera.aspect : 16f / 9f;

        if (backgroundRenderer != null)
        {
            Bounds bounds = backgroundRenderer.bounds;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.02f;
        }
        else
        {
            float halfBoardHeight = height * cellSize * 0.5f;
            float halfBoardWidth = width * cellSize * 0.5f;
            camera.orthographicSize = Mathf.Max(halfBoardHeight, halfBoardWidth / aspect) * 1.08f;
        }

        SweetTangleCameraController cameraController = camera.GetComponent<SweetTangleCameraController>();
        if (cameraController != null)
        {
            cameraController.ConfigureZoomLimits(camera.orthographicSize * 0.85f, camera.orthographicSize * 1.35f);
        }
    }

    private void BuildBoard()
    {
        board = new TilePiece[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpawnPieceAt(x, y, false);
            }
        }
    }

    private void HandleDragInput()
    {
        if (isBusy)
        {
            return;
        }

        if (GetPointerDown())
        {
            ClearDragPreview();
            dragStartPiece = GetPieceUnderMouse();
            dragStartMousePos = GetPointerPosition();
            dragConsumed = false;
            currentPreviewDirection = Vector2Int.zero;
            SetSelected(dragStartPiece);
        }

        if (!GetPointerHeld() || dragConsumed || dragStartPiece == null)
        {
            return;
        }

        Vector2 drag = GetPointerPosition() - dragStartMousePos;
        UpdateDragPreview(drag);
        if (drag.magnitude < DragThresholdPixels)
        {
            return;
        }

        dragConsumed = true;
        Vector2Int direction = GetDirectionFromDrag(drag);
        ClearDragPreview();

        int targetX = dragStartPiece.GridX + direction.x;
        int targetY = dragStartPiece.GridY + direction.y;
        if (!IsInsideBoard(targetX, targetY))
        {
            SetSelected(null);
            return;
        }

        TilePiece targetPiece = board[targetX, targetY];
        TilePiece first = dragStartPiece;
        SetSelected(null);
        StartCoroutine(SwapThenResolve(first, targetPiece));
    }

    private void HandleClickSwapInput()
    {
        if (isBusy || !GetPointerDown())
        {
            return;
        }

        TilePiece clicked = GetPieceUnderMouse();
        if (clicked == null)
        {
            return;
        }

        if (selectedPiece == null)
        {
            SetSelected(clicked);
            return;
        }

        if (clicked == selectedPiece)
        {
            SetSelected(null);
            return;
        }

        if (AreAdjacent(selectedPiece, clicked))
        {
            TilePiece first = selectedPiece;
            SetSelected(null);
            StartCoroutine(SwapThenResolve(first, clicked));
            return;
        }

        SetSelected(clicked);
    }

    private void SetSelected(TilePiece piece)
    {
        if (selectedPiece != null)
        {
            selectedPiece.SetSelected(false);
        }

        selectedPiece = piece;
        if (selectedPiece != null)
        {
            selectedPiece.SetSelected(true);
        }
    }

    private void LateUpdate()
    {
        if (GetPointerUp())
        {
            ClearDragPreview();
            dragStartPiece = null;
            dragConsumed = false;
            SetSelected(null);
        }
    }

    private IEnumerator SwapThenResolve(TilePiece a, TilePiece b)
    {
        isBusy = true;
        ResetMoveStats();

        Vector3 aPos = a.transform.position;
        Vector3 bPos = b.transform.position;
        yield return AnimateMove(a, bPos, swapDuration);
        yield return AnimateMove(b, aPos, swapDuration);
        SwapData(a, b);

        HashSet<TilePiece> matches = FindMatches();
        if (matches.Count == 0)
        {
            hud?.OnInvalidMove();
            yield return AnimateMove(a, aPos, swapDuration);
            yield return AnimateMove(b, bPos, swapDuration);
            SwapData(a, b);
            isBusy = false;
            yield break;
        }

        while (matches.Count > 0)
        {
            yield return ClearMatches(matches);
            yield return CollapseColumns();
            matches = FindMatches();
        }

        EnsureBoardFilled();
        hud?.OnValidMove(movePiecesCleared, moveMaxMatchLength);
        isBusy = false;
    }

    private void ResetMoveStats()
    {
        movePiecesCleared = 0;
        moveMaxMatchLength = 0;
    }

    private IEnumerator ClearMatchesUntilStable()
    {
        isBusy = true;
        HashSet<TilePiece> matches = FindMatches();
        while (matches.Count > 0)
        {
            yield return ClearMatches(matches);
            yield return CollapseColumns();
            matches = FindMatches();
        }

        EnsureBoardFilled();
        isBusy = false;
    }

    private IEnumerator ClearMatches(HashSet<TilePiece> matches)
    {
        movePiecesCleared += matches.Count;

        List<IEnumerator> animations = new List<IEnumerator>();
        foreach (TilePiece piece in matches)
        {
            if (piece == null)
            {
                continue;
            }

            int gridX = piece.GridX;
            int gridY = piece.GridY;
            if (IsInsideBoard(gridX, gridY) && board[gridX, gridY] == piece)
            {
                board[gridX, gridY] = null;
            }

            animations.Add(AnimateClear(piece));
        }

        yield return RunAnimationsParallel(animations);
    }

    private IEnumerator CollapseColumns()
    {
        List<IEnumerator> dropAnimations = new List<IEnumerator>();

        for (int x = 0; x < width; x++)
        {
            List<TilePiece> survivingPieces = new List<TilePiece>();
            for (int y = 0; y < height; y++)
            {
                TilePiece piece = board[x, y];
                board[x, y] = null;
                if (piece != null)
                {
                    survivingPieces.Add(piece);
                }
            }

            int spawnCount = height - survivingPieces.Count;

            for (int i = 0; i < survivingPieces.Count; i++)
            {
                int newY = i;
                TilePiece piece = survivingPieces[i];
                board[x, newY] = piece;
                piece.SetGridPosition(x, newY);
                QueueDropAnimation(dropAnimations, piece, GridToWorld(x, newY));
            }

            for (int i = 0; i < spawnCount; i++)
            {
                int spawnY = survivingPieces.Count + i;
                SpawnPieceAt(x, spawnY, true);
                TilePiece spawned = board[x, spawnY];
                if (spawned != null)
                {
                    QueueDropAnimation(dropAnimations, spawned, GridToWorld(x, spawnY));
                }
            }
        }

        yield return RunAnimationsParallel(dropAnimations);
    }

    private void QueueDropAnimation(List<IEnumerator> animations, TilePiece piece, Vector3 targetPosition)
    {
        if (piece == null)
        {
            return;
        }

        Vector3 startPosition = piece.transform.position;
        if ((targetPosition - startPosition).sqrMagnitude < 0.0001f)
        {
            piece.SnapToCell(targetPosition, GetPieceTargetScale(piece));
            return;
        }

        float duration = GetDropDuration(startPosition, targetPosition);
        animations.Add(AnimateMove(piece, targetPosition, duration));
    }

    private float GetDropDuration(Vector3 from, Vector3 to)
    {
        float cellDistance = Mathf.Abs(to.y - from.y) / Mathf.Max(cellSize, 0.001f);
        return Mathf.Clamp(cellDistance * dropDurationPerCell, minDropDuration, maxDropDuration);
    }

    private IEnumerator RunAnimationsParallel(List<IEnumerator> animations)
    {
        if (animations == null || animations.Count == 0)
        {
            yield break;
        }

        int remaining = animations.Count;
        foreach (IEnumerator animation in animations)
        {
            StartCoroutine(RunAnimationAndSignal(animation, () => remaining--));
        }

        while (remaining > 0)
        {
            yield return null;
        }
    }

    private IEnumerator RunAnimationAndSignal(IEnumerator animation, System.Action onComplete)
    {
        yield return StartCoroutine(animation);
        onComplete?.Invoke();
    }

    private void EnsureBoardFilled()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == null)
                {
                    SpawnPieceAt(x, y, false);
                    continue;
                }

                TilePiece piece = board[x, y];
                piece.SetGridPosition(x, y);
                piece.SnapToCell(GridToWorld(x, y), GetPieceTargetScale(piece));
            }
        }
    }

    private Vector3 GetPieceTargetScale(TilePiece piece)
    {
        float targetSize = cellSize * pieceScale;
        SpriteRenderer spriteRenderer = piece.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float spriteWidth = spriteRenderer.sprite.bounds.size.x;
            float uniformScale = spriteWidth > 0f ? targetSize / spriteWidth : targetSize;
            return Vector3.one * uniformScale;
        }

        return Vector3.one * targetSize;
    }

    private TilePiece SpawnPieceAt(int x, int y, bool fromAbove)
    {
        int typeCount = Mathf.Min(pieceTypeCount, fallbackColors.Length);
        int type = Random.Range(0, typeCount);
        Vector3 targetPos = GridToWorld(x, y);
        Vector3 startPos = fromAbove ? targetPos + Vector3.up * spawnYOffset : targetPos;
        float targetSize = cellSize * pieceScale;

        GameObject go;
        TilePiece piece;

        if (pieceSprites != null && type < pieceSprites.Length && pieceSprites[type] != null)
        {
            Sprite sprite = pieceSprites[type];
            go = new GameObject($"Piece_{x}_{y}");
            go.transform.SetParent(boardRoot);
            go.transform.position = startPos;

            SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 10;

            float spriteWidth = sprite.bounds.size.x;
            float uniformScale = spriteWidth > 0f ? targetSize / spriteWidth : targetSize;
            go.transform.localScale = Vector3.one * uniformScale;

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(sprite.bounds.size.x, sprite.bounds.size.y, 0.1f);

            piece = go.AddComponent<TilePiece>();
            piece.Initialize(x, y, type, sprite, spriteRenderer);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Piece_{x}_{y}";
            go.transform.SetParent(boardRoot);
            go.transform.position = startPos;
            go.transform.localScale = Vector3.one * targetSize;

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            piece = go.AddComponent<TilePiece>();
            piece.InitializeFallback(x, y, type, fallbackColors[type], renderer);
        }

        board[x, y] = piece;
        return piece;
    }

    private HashSet<TilePiece> FindMatches()
    {
        HashSet<TilePiece> matches = new HashSet<TilePiece>();

        for (int y = 0; y < height; y++)
        {
            int runLength = 1;
            for (int x = 1; x < width; x++)
            {
                TilePiece prev = board[x - 1, y];
                TilePiece curr = board[x, y];
                bool same = prev != null && curr != null && prev.TypeId == curr.TypeId;

                if (same)
                {
                    runLength++;
                }
                else
                {
                    AddRunToMatches(matches, x - 1, y, runLength, horizontal: true);
                    runLength = 1;
                }
            }

            AddRunToMatches(matches, width - 1, y, runLength, horizontal: true);
        }

        for (int x = 0; x < width; x++)
        {
            int runLength = 1;
            for (int y = 1; y < height; y++)
            {
                TilePiece prev = board[x, y - 1];
                TilePiece curr = board[x, y];
                bool same = prev != null && curr != null && prev.TypeId == curr.TypeId;

                if (same)
                {
                    runLength++;
                }
                else
                {
                    AddRunToMatches(matches, x, y - 1, runLength, horizontal: false);
                    runLength = 1;
                }
            }

            AddRunToMatches(matches, x, height - 1, runLength, horizontal: false);
        }

        return matches;
    }

    private void AddRunToMatches(HashSet<TilePiece> matches, int endX, int endY, int runLength, bool horizontal)
    {
        if (runLength < 3)
        {
            return;
        }

        moveMaxMatchLength = Mathf.Max(moveMaxMatchLength, runLength);

        for (int i = 0; i < runLength; i++)
        {
            int x = horizontal ? endX - i : endX;
            int y = horizontal ? endY : endY - i;
            matches.Add(board[x, y]);
        }
    }

    private IEnumerator AnimateMove(TilePiece piece, Vector3 destination, float duration)
    {
        if (piece == null)
        {
            yield break;
        }

        Vector3 start = piece.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            piece.transform.position = Vector3.Lerp(start, destination, t);
            yield return null;
        }

        piece.transform.position = destination;
    }

    private IEnumerator AnimateClear(TilePiece piece)
    {
        if (piece == null)
        {
            yield break;
        }

        Vector3 startScale = piece.transform.localScale;
        float elapsed = 0f;
        while (elapsed < clearDuration)
        {
            if (piece == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / clearDuration);
            piece.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (piece != null)
        {
            Destroy(piece.gameObject);
        }
    }

    private void SwapData(TilePiece a, TilePiece b)
    {
        int ax = a.GridX;
        int ay = a.GridY;
        int bx = b.GridX;
        int by = b.GridY;

        board[ax, ay] = b;
        board[bx, by] = a;

        a.SetGridPosition(bx, by);
        b.SetGridPosition(ax, ay);
    }

    private bool AreAdjacent(TilePiece a, TilePiece b)
    {
        int dx = Mathf.Abs(a.GridX - b.GridX);
        int dy = Mathf.Abs(a.GridY - b.GridY);
        return dx + dy == 1;
    }

    private bool IsInsideBoard(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    private TilePiece GetPieceUnderMouse()
    {
        if (Camera.main == null)
        {
            return null;
        }

        Ray ray = Camera.main.ScreenPointToRay(GetPointerPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return null;
        }

        return hit.collider.GetComponent<TilePiece>();
    }

    private bool GetPointerDown()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool GetPointerHeld()
    {
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private bool GetPointerUp()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    private Vector2 GetPointerPosition()
    {
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }

    private void UpdateDragPreview(Vector2 drag)
    {
        if (dragStartPiece == null || dragConsumed)
        {
            ClearDragPreview();
            return;
        }

        if (drag.magnitude < 1f)
        {
            ClearDragPreview();
            return;
        }

        Vector2Int direction = GetDirectionFromDrag(drag);
        if (direction == Vector2Int.zero)
        {
            ClearDragPreview();
            return;
        }

        if (direction != currentPreviewDirection)
        {
            if (previewTargetPiece != null)
            {
                previewTargetPiece.SetHint(false);
            }
            currentPreviewDirection = direction;
            int targetX = dragStartPiece.GridX + direction.x;
            int targetY = dragStartPiece.GridY + direction.y;
            previewTargetPiece = IsInsideBoard(targetX, targetY) ? board[targetX, targetY] : null;
            if (previewTargetPiece != null)
            {
                previewTargetPiece.SetHint(true);
            }
        }

        float normalizedStrength = Mathf.Clamp01(drag.magnitude / DragThresholdPixels);
        float worldOffset = cellSize * previewOffsetFactor * normalizedStrength;
        Vector3 offset = new Vector3(direction.x, direction.y, 0f) * worldOffset;
        dragStartPiece.transform.position = GridToWorld(dragStartPiece.GridX, dragStartPiece.GridY) + offset;

        if (previewTargetPiece != null)
        {
            previewTargetPiece.transform.position =
                GridToWorld(previewTargetPiece.GridX, previewTargetPiece.GridY) - offset * 0.35f;
        }
    }

    private void ClearDragPreview()
    {
        currentPreviewDirection = Vector2Int.zero;
        if (dragStartPiece != null)
        {
            dragStartPiece.transform.position = GridToWorld(dragStartPiece.GridX, dragStartPiece.GridY);
        }

        if (previewTargetPiece != null)
        {
            previewTargetPiece.SetHint(false);
            previewTargetPiece.transform.position = GridToWorld(previewTargetPiece.GridX, previewTargetPiece.GridY);
            previewTargetPiece = null;
        }
    }

    private Vector2Int GetDirectionFromDrag(Vector2 drag)
    {
        if (Mathf.Abs(drag.x) >= Mathf.Abs(drag.y))
        {
            return drag.x >= 0f ? Vector2Int.right : Vector2Int.left;
        }

        return drag.y >= 0f ? Vector2Int.up : Vector2Int.down;
    }

    private Vector3 GridToWorld(int x, int y, float z = 0f)
    {
        return new Vector3(
            boardOrigin.x + x * cellSize,
            boardOrigin.y + y * cellSize,
            z
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<SweetTangleGame>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("SweetTangleGame");
        bootstrap.AddComponent<SweetTangleGame>();
    }
}
