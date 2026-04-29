using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class OmokAiController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OmokMatchManager matchManager;
    [SerializeField] private OmokStoneDropper stoneDropper;

    [Header("Single Player")]
    [FormerlySerializedAs("useEasyAi")]
    [SerializeField] private bool useAi;
    [SerializeField] private OmokAiType selectedAiType = OmokAiType.Normal;
    [SerializeField] private OmokStoneColor playerStoneColor = OmokStoneColor.Gold;
    [SerializeField, Min(0f)] private float aiTurnDelay = 0.35f;

    private Coroutine _pendingAiTurn;
    private IOmokAi _selectedAi;

    public bool UseAi => useAi;
    public OmokAiType SelectedAiType => selectedAiType;
    public OmokStoneColor PlayerStoneColor => playerStoneColor;

    private void Reset()
    {
        ResolveReferences();
        _selectedAi = CreateAi(selectedAiType);
    }

    private void Awake()
    {
        ResolveReferences();
        _selectedAi = CreateAi(selectedAiType);
    }

    private void OnEnable()
    {
        if (matchManager != null)
        {
            matchManager.OnTurnChanged += HandleTurnChanged;
            matchManager.OnMatchEnded += HandleMatchEnded;
        }

        SyncState();
    }

    private void OnDisable()
    {
        if (matchManager != null)
        {
            matchManager.OnTurnChanged -= HandleTurnChanged;
            matchManager.OnMatchEnded -= HandleMatchEnded;
        }

        CancelPendingAiTurn();

        if (stoneDropper != null)
        {
            stoneDropper.SetManualPlacementState(true, true);
        }
    }

    private void OnValidate()
    {
        if (playerStoneColor == OmokStoneColor.None)
        {
            playerStoneColor = OmokStoneColor.Gold;
        }

        _selectedAi = CreateAi(selectedAiType);
    }

    private void ResolveReferences()
    {
        if (matchManager == null)
        {
            matchManager = GetComponent<OmokMatchManager>();
        }

        if (matchManager == null)
        {
            matchManager = FindFirstObjectByType<OmokMatchManager>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponent<OmokStoneDropper>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = FindFirstObjectByType<OmokStoneDropper>();
        }
    }

    public void SetAiEnabled(bool isEnabled)
    {
        useAi = isEnabled;

        if (!isActiveAndEnabled)
        {
            return;
        }

        SyncState();
    }

    public void SetSelectedAiType(OmokAiType aiType)
    {
        selectedAiType = aiType;
        _selectedAi = CreateAi(selectedAiType);

        if (!isActiveAndEnabled)
        {
            return;
        }

        SyncState();
    }

    public void SetPlayerStoneColor(OmokStoneColor stoneColor)
    {
        if (stoneColor == OmokStoneColor.None)
        {
            return;
        }

        playerStoneColor = stoneColor;

        if (!isActiveAndEnabled)
        {
            return;
        }

        SyncState();
    }

    private void SyncState()
    {
        RefreshManualPlacementState();
        TryQueueAiTurn();
    }

    private void HandleTurnChanged(OmokStoneColor nextTurn)
    {
        SyncState();
    }

    private void HandleMatchEnded(OmokStoneColor resultWinner)
    {
        CancelPendingAiTurn();

        if (stoneDropper != null)
        {
            stoneDropper.SetManualPlacementState(false, false);
        }
    }

    private void RefreshManualPlacementState()
    {
        if (stoneDropper == null)
        {
            return;
        }

        if (!useAi || matchManager == null)
        {
            stoneDropper.SetManualPlacementState(true, true);
            return;
        }

        OmokManualPlacementState placementState = matchManager.GetManualPlacementState(playerStoneColor);
        stoneDropper.SetManualPlacementState(placementState.AllowGold, placementState.AllowSilver);
    }

    private void TryQueueAiTurn()
    {
        CancelPendingAiTurn();

        if (!CanAiMove())
        {
            return;
        }

        _pendingAiTurn = StartCoroutine(RunAiTurn());
    }

    private IEnumerator RunAiTurn()
    {
        yield return null;

        if (aiTurnDelay > 0f)
        {
            yield return new WaitForSeconds(aiTurnDelay);
        }

        _pendingAiTurn = null;

        if (!CanAiMove() || _selectedAi == null)
        {
            yield break;
        }

        OmokStoneColor[,] snapshot = matchManager.GetBoardSnapshot();
        if (!_selectedAi.TryChooseMove(snapshot, GetAiStoneColor(), out Vector2Int move))
        {
            yield break;
        }

        if (!stoneDropper.TryRequestPlacement(GetAiStoneColor(), move))
        {
            TryQueueAiTurn();
        }
    }

    private bool CanAiMove()
    {
        return useAi &&
               _selectedAi != null &&
               matchManager != null &&
               stoneDropper != null &&
               matchManager.CanTakeTurn(GetAiStoneColor());
    }

    private void CancelPendingAiTurn()
    {
        if (_pendingAiTurn == null)
        {
            return;
        }

        StopCoroutine(_pendingAiTurn);
        _pendingAiTurn = null;
    }

    private OmokStoneColor GetAiStoneColor()
    {
        return playerStoneColor == OmokStoneColor.Gold ? OmokStoneColor.Silver : OmokStoneColor.Gold;
    }

    private static IOmokAi CreateAi(OmokAiType aiType)
    {
        return aiType switch
        {
            OmokAiType.Easy => new OmokEasyAi(),
            OmokAiType.Normal => new OmokNormalAi(),
            _ => new OmokNormalAi()
        };
    }
}
