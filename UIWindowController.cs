using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UIWindowController : MonoBehaviour
{
    public enum AnimationType
    {
        None,
        Fade,
        Scale,
        FadeAndScale
    }

    public event Action<bool> OnWindowStateChange;

    // ── Top level controls ────────────────────────────────────────────────
    [HorizontalGroup("TopBar")]
    [Button("Open", ButtonSizes.Medium), GUIColor(0.4f, 1f, 0.4f)]
    [PropertyOrder(-10)]
    void OpenWindow() => Open(true);

    [HorizontalGroup("TopBar")]
    [Button("Close", ButtonSizes.Medium), GUIColor(1f, 0.4f, 0.4f)]
    [PropertyOrder(-10)]
    void CloseWindow() => Open(false);

    [HorizontalGroup("TopBar")]
    [Button("Toggle", ButtonSizes.Medium), GUIColor(1f, 0.8f, 0.4f)]
    [PropertyOrder(-10)]
    void ToggleWindow() => Toggle();
    [PropertyOrder(-9)]
    [SerializeField] string friendlyTitle = "Untitled Window";
    [PropertyOrder(-9)]
    [SerializeField] Sprite windowIcon = null;
    [PropertyOrder(-9)]
    [ToggleLeft]
    [SerializeField] public bool debugMessages;
    [PropertyOrder(-9)]
    [ToggleLeft]
    [SerializeField] public bool debugLateUpdateMessages;
    // ── Tabs ──────────────────────────────────────────────────────────────
    public string FriendlyTitle => friendlyTitle;
    public Sprite WindowIcon => windowIcon;
    [TabGroup("Tabs", "References")]
    [LabelText("Canvas Group")]
    public CanvasGroup myCanvas;

    [TabGroup("Tabs", "Behaviour")]
    [ReadOnly]
    public bool IsOpen;

    [TabGroup("Tabs", "Behaviour")]
    public bool pauseOnOpen;

    [TabGroup("Tabs", "Behaviour")]
    [Title("Initialisation")]
    [SerializeField] public bool closeOnAwake = false;

    [TabGroup("Tabs", "Behaviour")]
    [SerializeField] public bool openOnAwake = false;

    [TabGroup("Tabs", "Behaviour")]
    [SerializeField] public bool controlGameObject = false;

    [TabGroup("Tabs", "Behaviour")]
    [Title("Input Locking")]
    [SerializeField] public bool disableCameraControlOnOpen = false;

    [TabGroup("Tabs", "Behaviour")]
    [SerializeField] public bool disableBlockMovementOnOpen = false;

    [TabGroup("Tabs", "Behaviour")]
    [SerializeField] public bool deselectBlocksOnOpen = false;

    [TabGroup("Tabs", "Animation")]
    [SerializeField] private AnimationType animationType = AnimationType.None;

    [TabGroup("Tabs", "Animation")]
    [SerializeField, ShowIf("@animationType != AnimationType.None")]
    private float animationDuration = 0.2f;

    [TabGroup("Tabs", "Animation")]
    [SerializeField, ShowIf("@animationType == AnimationType.Scale || animationType == AnimationType.FadeAndScale")]
    private Vector3 closedScale = new Vector3(0.8f, 0.8f, 1f);

    [TabGroup("Tabs", "Animation")]
    [SerializeField, ShowIf("@animationType != AnimationType.None")]
    private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ── Private state ─────────────────────────────────────────────────────
    private bool initialized = false;
    private Coroutine currentAnimation;
    private Vector3 originalScale = Vector3.one;
    protected float showAlpha = 1;
    // ── Lifecycle ─────────────────────────────────────────────────────────

    public virtual void Awake()
    {
        GetReferences();
        Subscribe(true);

        if (closeOnAwake) Open(false);
        if (openOnAwake) Open(true);
    }

    public virtual void Subscribe(bool subscribe) { }

    public virtual void GetReferences()
    {
        if (myCanvas == null)
            myCanvas = GetComponent<CanvasGroup>();

        initialized = true;
    }

    public virtual void Toggle() => Open(!IsOpen);

    public virtual void Open(bool open)
    {
        if (!initialized) GetReferences();

        if (myCanvas == null)
        {
            Debug.LogError($"[{gameObject.name}] No CanvasGroup attached to UIWindowController!");
            return;
        }

        if (debugMessages) Debug.Log($"[{gameObject.name}] Open({open})");

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }

        if (animationType != AnimationType.None && Application.isPlaying)
        {
            if (controlGameObject && open)
                gameObject.SetActive(true);

            if (!open && !gameObject.activeSelf)
                SetWindowState(false, true);
            else
                currentAnimation = StartCoroutine(AnimateWindow(open));
        }
        else
        {
            SetWindowState(open, true);
        }

        //if (disableCameraControlOnOpen)
        //    CameraBridge.SetInputEnabled(!open);

        //if (deselectBlocksOnOpen && open)
        //    SelectionController.RequestDeselect();

        //if (disableBlockMovementOnOpen)
        //    SelectionController.SetMovementEnabled(!open);
    }

    // ── Animation ─────────────────────────────────────────────────────────

    private IEnumerator AnimateWindow(bool open)
    {
        float elapsed = 0f;
        float startAlpha = myCanvas.alpha;
        Vector3 startScale = transform.localScale;
        float targetAlpha = open ? showAlpha : 0f;
        Vector3 targetScale = open ? originalScale : closedScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float curveT = animationCurve.Evaluate(t);

            if (animationType == AnimationType.Fade || animationType == AnimationType.FadeAndScale)
                myCanvas.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveT);

            if (animationType == AnimationType.Scale || animationType == AnimationType.FadeAndScale)
                transform.localScale = Vector3.Lerp(startScale, targetScale, curveT);

            yield return null;
        }

        SetWindowState(open, false);
        currentAnimation = null;
    }

    // ── State ─────────────────────────────────────────────────────────────
    public virtual bool CanInteract => IsOpen;
    private void SetWindowState(bool open, bool instant)
    {
        myCanvas.alpha = open ? showAlpha : 0f;
        myCanvas.blocksRaycasts = open;

        if (animationType == AnimationType.Scale || animationType == AnimationType.FadeAndScale)
            transform.localScale = open ? originalScale : closedScale;

        if (open)
            gameObject.SetActive(true);
        else if (controlGameObject)
            gameObject.SetActive(false);

        if (IsOpen != open)
        {
            IsOpen = open;
            OnWindowStateChange?.Invoke(IsOpen);
            if (debugMessages) Debug.Log($"[{gameObject.name}] State changed → IsOpen: {IsOpen}");
        }

        if (pauseOnOpen)
            Time.timeScale = open ? 0f : 1f;
        myCanvas.interactable = CanInteract;
    }

    // ── Disable ───────────────────────────────────────────────────────────

    public virtual void OnDisable()
    {
        Subscribe(false);

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
    }


}