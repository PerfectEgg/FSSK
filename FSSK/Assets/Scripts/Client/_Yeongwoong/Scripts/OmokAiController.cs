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
    [SerializeField] private OmokAiType selectedAiType = OmokAiType.Easy;
    [SerializeField] private OmokStoneColor playerStoneColor = OmokStoneColor.Black;
    [SerializeField, Min(0f)] private float aiTurnDelay = 0.35f;

    private Coroutine pendingAiTurn;
    private IOmokAi selectedAi;

    private void Reset()
    {
        ResolveReferences();
        selectedAi = CreateAi(selectedAiType);
    }

    private void Awake()
    {
        ResolveReferences();
        selectedAi = CreateAi(selectedAiType);
    }

    private void OnEnable()
    {
        if (matchManager != null)
        {
            matchManager.TurnChanged += HandleTurnChanged;
            matchManager.MatchEnded += HandleMatchEnded;
        }

        SyncState();
    }

    private void OnDisable()
    {
        if (matchManager != null)
        {
            matchManager.TurnChanged -= HandleTurnChanged;
            matchManager.MatchEnded -= HandleMatchEnded;
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
            playerStoneColor = OmokStoneColor.Black;
        }

        selectedAi = CreateAi(selectedAiType);
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
        stoneDropper.SetManualPlacementState(placementState.AllowBlack, placementState.AllowWhite);
    }

    private void TryQueueAiTurn()
    {
        CancelPendingAiTurn();

        if (!CanAiMove())
        {
            return;
        }

        pendingAiTurn = StartCoroutine(RunAiTurn());
    }

    private IEnumerator RunAiTurn()
    {
        if (aiTurnDelay > 0f)
        {
            yield return new WaitForSeconds(aiTurnDelay);
        }

        pendingAiTurn = null;

        if (!CanAiMove() || selectedAi == null)
        {
            yield break;
        }

        OmokStoneColor[,] snapshot = matchManager.GetBoardSnapshot();
        if (!selectedAi.TryChooseMove(snapshot, GetAiStoneColor(), out Vector2Int move))
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
               selectedAi != null &&
               matchManager != null &&
               stoneDropper != null &&
               matchManager.CanTakeTurn(GetAiStoneColor());
    }

    private void CancelPendingAiTurn()
    {
        if (pendingAiTurn == null)
        {
            return;
        }

        StopCoroutine(pendingAiTurn);
        pendingAiTurn = null;
    }

    private OmokStoneColor GetAiStoneColor()
    {
        return playerStoneColor == OmokStoneColor.Black ? OmokStoneColor.White : OmokStoneColor.Black;
    }

    private static IOmokAi CreateAi(OmokAiType aiType)
    {
        return aiType switch
        {
            OmokAiType.Easy => new OmokEasyAi(),
            _ => new OmokEasyAi()
        };
    }
}
