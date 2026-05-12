using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class StunScreenEffectController : MonoBehaviour
{
    [SerializeField] private GameObject stunPanel;
    [SerializeField] private Graphic stunGraphic;
    [SerializeField] private bool useStunDuration = true;
    [SerializeField, Min(0.05f)] private float visibleSeconds = 1f;
    [SerializeField, Range(0f, 1f)] private float visibleOpacity = 1f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.25f;
    [SerializeField] private bool disablePanelWhenHidden = true;

    [Header("Test")]
    [SerializeField] private bool allowKeyboardTesting = true;
    [SerializeField] private KeyCode testKey = KeyCode.K;

    private Coroutine _routine;
    private float _visibleAlpha = 1f;

    private void Awake()
    {
        ResolveReferences();

        if (stunGraphic != null)
        {
            RefreshVisibleAlpha();
            HideImmediate();
        }
    }

    private void OnEnable()
    {
        TrollEvents.OnStunEffect += HandleStunEffect;
        GameEvents.OnGameOverTriggered += HandleGameOver;
    }

    private void OnDisable()
    {
        TrollEvents.OnStunEffect -= HandleStunEffect;
        GameEvents.OnGameOverTriggered -= HandleGameOver;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
            HideImmediate();
        }
    }

    private void Update()
    {
        if (allowKeyboardTesting && Input.GetKeyDown(testKey))
        {
            Play(visibleSeconds);
        }
    }

    private void HandleStunEffect(float stunDuration)
    {
        float duration = useStunDuration ? stunDuration : visibleSeconds;
        Play(duration);
    }

    private void HandleGameOver()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        HideImmediate();
    }

    public void Play(float duration)
    {
        ResolveReferences();

        if (stunPanel == null || stunGraphic == null)
        {
            return;
        }

        RefreshVisibleAlpha();

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(ShowRoutine(Mathf.Max(0.05f, duration)));
    }

    private IEnumerator ShowRoutine(float duration)
    {
        stunPanel.SetActive(true);

        if (fadeInSeconds > 0f)
        {
            yield return Fade(0f, _visibleAlpha, fadeInSeconds);
        }
        else
        {
            SetAlpha(_visibleAlpha);
        }

        float holdSeconds = Mathf.Max(0f, duration - fadeInSeconds - fadeOutSeconds);
        if (holdSeconds > 0f)
        {
            yield return Hold(holdSeconds);
        }

        if (fadeOutSeconds > 0f)
        {
            yield return Fade(_visibleAlpha, 0f, fadeOutSeconds);
        }
        else
        {
            SetAlpha(0f);
        }

        if (disablePanelWhenHidden)
        {
            stunPanel.SetActive(false);
        }

        _routine = null;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlpha(to);
    }

    private IEnumerator Hold(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void HideImmediate()
    {
        SetAlpha(0f);

        if (disablePanelWhenHidden && stunPanel != null)
        {
            stunPanel.SetActive(false);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (stunGraphic == null)
        {
            return;
        }

        Color color = stunGraphic.color;
        color.a = Mathf.Clamp01(alpha);
        stunGraphic.color = color;
    }

    private void RefreshVisibleAlpha()
    {
        _visibleAlpha = Mathf.Clamp01(visibleOpacity);
    }

    private void ResolveReferences()
    {
        if (stunPanel == null && stunGraphic != null)
        {
            stunPanel = stunGraphic.gameObject;
        }

        if (stunGraphic == null && stunPanel != null)
        {
            stunGraphic = stunPanel.GetComponent<Graphic>();
        }
    }
}
