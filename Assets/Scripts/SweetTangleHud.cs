using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SweetTangleHud : MonoBehaviour
{
    private const string UiFolder = "Assets/Art/UI";

    [SerializeField] private float messageDuration = 1.35f;
    [SerializeField] private float logoHeightAboveGrid = 1.15f;
    [SerializeField] private float logoMaxWidth = 5.5f;
    [SerializeField] private float messageWorldWidth = 4.2f;
    [SerializeField] private int uiSortingOrder = 30;

    private Sprite logoSprite;
    private Sprite invalidMoveSprite;
    private Sprite genialSprite;
    private Sprite estupendoSprite;
    private Sprite hazGanadoSprite;
    private Sprite hazGanadoAltSprite;

    private Transform logoTransform;
    private SpriteRenderer messageRenderer;
    private Coroutine messageRoutine;
    private int score;
    private int maxScore = 300;
    private int pointsPerMove = 3;
    private GUIStyle scoreStyle;

    public int Score => score;
    public int MaxScore => maxScore;

    public void Initialize(int maxScoreValue, int pointsPerMoveValue, float boardTopY, float boardWidth)
    {
        maxScore = maxScoreValue;
        pointsPerMove = Mathf.Max(1, pointsPerMoveValue);
        score = 0;
        LoadSprites();
        BuildLogo(boardTopY, boardWidth);
        BuildMessageOverlay();
        AdjustCameraForLogo();
    }

    public void OnValidMove(int piecesCleared, int maxMatchLength)
    {
        score = Mathf.Min(score + pointsPerMove, maxScore);

        if (score >= maxScore)
        {
            ShowMessage(GetRandomWinSprite());
            score = 0;
            return;
        }

        bool isNormalMatchOfThree = piecesCleared == 3 && maxMatchLength == 3;
        ShowMessage(isNormalMatchOfThree ? genialSprite : estupendoSprite);
    }

    public void OnInvalidMove()
    {
        score = Mathf.Max(0, score - pointsPerMove);
        ShowMessage(invalidMoveSprite);
    }

    private Sprite GetRandomWinSprite()
    {
        bool useAlt = Random.value >= 0.5f;
        if (useAlt && hazGanadoAltSprite != null)
        {
            return hazGanadoAltSprite;
        }

        if (hazGanadoSprite != null)
        {
            return hazGanadoSprite;
        }

        return hazGanadoAltSprite;
    }

    private void LoadSprites()
    {
        logoSprite = LoadUiSprite("SweetTangle_Logo");
        invalidMoveSprite = LoadUiSprite("MovimientoInvalido");
        genialSprite = LoadUiSprite("Genial");
        estupendoSprite = LoadUiSprite("Estupendo");
        hazGanadoSprite = LoadUiSprite("HazGanado");
        hazGanadoAltSprite = LoadUiSprite("HazGanado_Alt");
    }

    private static Sprite LoadUiSprite(string assetName)
    {
#if UNITY_EDITOR
        string path = $"{UiFolder}/{assetName}.png";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
#else
        return Resources.Load<Sprite>($"Art/UI/{assetName}");
#endif
        return null;
    }

    private void BuildLogo(float boardTopY, float boardWidth)
    {
        if (logoSprite == null)
        {
            return;
        }

        GameObject logoObject = new GameObject("Logo");
        logoObject.transform.SetParent(transform);
        logoTransform = logoObject.transform;

        SpriteRenderer renderer = logoObject.AddComponent<SpriteRenderer>();
        renderer.sprite = logoSprite;
        renderer.sortingOrder = uiSortingOrder;

        float targetWidth = Mathf.Min(logoMaxWidth, boardWidth * 0.92f);
        float scale = logoSprite.bounds.size.x > 0f ? targetWidth / logoSprite.bounds.size.x : 1f;
        logoObject.transform.localScale = Vector3.one * scale;
        logoObject.transform.position = new Vector3(0f, boardTopY + logoHeightAboveGrid, -1f);
    }

    private void AdjustCameraForLogo()
    {
        if (logoTransform == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        SpriteRenderer logoRenderer = logoTransform.GetComponent<SpriteRenderer>();
        if (logoRenderer == null)
        {
            return;
        }

        float logoTop = logoRenderer.bounds.max.y;
        camera.orthographicSize = Mathf.Max(camera.orthographicSize, logoTop + 0.35f);

        SweetTangleCameraController controller = camera.GetComponent<SweetTangleCameraController>();
        if (controller != null)
        {
            controller.ConfigureZoomLimits(camera.orthographicSize * 0.85f, camera.orthographicSize * 1.35f);
        }
    }

    private void BuildMessageOverlay()
    {
        GameObject messageObject = new GameObject("MoveMessage");
        messageObject.transform.SetParent(transform);
        messageObject.transform.position = new Vector3(0f, 0f, -0.5f);
        messageRenderer = messageObject.AddComponent<SpriteRenderer>();
        messageRenderer.sortingOrder = uiSortingOrder + 1;
        messageRenderer.enabled = false;
    }

    private void ShowMessage(Sprite sprite)
    {
        if (sprite == null || messageRenderer == null)
        {
            return;
        }

        if (messageRoutine != null)
        {
            StopCoroutine(messageRoutine);
        }

        messageRoutine = StartCoroutine(ShowMessageRoutine(sprite));
    }

    private IEnumerator ShowMessageRoutine(Sprite sprite)
    {
        messageRenderer.sprite = sprite;
        messageRenderer.enabled = true;
        messageRenderer.color = Color.white;

        float scale = sprite.bounds.size.x > 0f ? messageWorldWidth / sprite.bounds.size.x : 1f;
        messageRenderer.transform.localScale = Vector3.one * scale;

        float elapsed = 0f;
        while (elapsed < messageDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / messageDuration);
            float alpha = t < 0.12f ? t / 0.12f : t > 0.82f ? (1f - t) / 0.18f : 1f;
            Color color = messageRenderer.color;
            color.a = alpha;
            messageRenderer.color = color;
            yield return null;
        }

        messageRenderer.enabled = false;
        messageRoutine = null;
    }

    private void OnGUI()
    {
        EnsureScoreStyle();

        const float panelWidth = 220f;
        const float panelHeight = 44f;
        Rect rect = new Rect(16f, 16f, panelWidth, panelHeight);

        Color previous = GUI.color;
        GUI.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(rect, $"Puntos: {score} / {maxScore}", scoreStyle);
        GUI.color = previous;
    }

    private void EnsureScoreStyle()
    {
        if (scoreStyle != null)
        {
            return;
        }

        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }
}
