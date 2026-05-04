using UnityEditor;

[CustomEditor(typeof(OmokTurnSystem))]
public class OmokTurnSystemEditor : Editor
{
    private SerializedProperty _gameMode;
    private SerializedProperty _useAi;
    private SerializedProperty _selectedAiType;
    private SerializedProperty _localPlayerColor;
    private SerializedProperty _allowManualInput;
    private SerializedProperty _restrictManualDragToCurrentTurn;
    private SerializedProperty _aiTurnDelay;
    private SerializedProperty _useTurnTimer;
    private SerializedProperty _turnDurationSeconds;
    private SerializedProperty _minimumTurnDurationSeconds;
    private SerializedProperty _timeoutAction;
    private SerializedProperty _goldSeat;
    private SerializedProperty _silverSeat;
    private SerializedProperty _openingTurn;
    private SerializedProperty _allowOverline;
    private SerializedProperty _blockedAttemptConsumesTurn;
    private SerializedProperty _allowBlockerVerticalWin;
    private SerializedProperty _blockerVerticalWinLength;
    private SerializedProperty _authorityMode;
    private SerializedProperty _processPlacementRequestsLocally;
    private SerializedProperty _applyStoneResultsLocally;
    private SerializedProperty _currentTurn;
    private SerializedProperty _currentTurnActor;
    private SerializedProperty _isMatchEnded;
    private SerializedProperty _winner;
    private SerializedProperty _nextRandomRemovalColor;
    private SerializedProperty _timerTurn;
    private SerializedProperty _turnElapsedSeconds;
    private SerializedProperty _turnRemainingSeconds;
    private SerializedProperty _turnRemainingWholeSeconds;
    private SerializedProperty _turnTimerProgress01;
    private SerializedProperty _turnTimerExpired;
    private SerializedProperty _boardSize;
    private SerializedProperty _totalPlacedStones;
    private SerializedProperty _emptyCells;
    private SerializedProperty _goldDisplayColor;
    private SerializedProperty _silverDisplayColor;

    private bool _showAdvanced;
    private bool _showDebug;

    private void OnEnable()
    {
        _gameMode = serializedObject.FindProperty("gameMode");
        _useAi = serializedObject.FindProperty("useAi");
        _selectedAiType = serializedObject.FindProperty("selectedAiType");
        _localPlayerColor = serializedObject.FindProperty("localPlayerColor");
        _allowManualInput = serializedObject.FindProperty("allowManualInput");
        _restrictManualDragToCurrentTurn = serializedObject.FindProperty("restrictManualDragToCurrentTurn");
        _aiTurnDelay = serializedObject.FindProperty("aiTurnDelay");
        _useTurnTimer = serializedObject.FindProperty("useTurnTimer");
        _turnDurationSeconds = serializedObject.FindProperty("turnDurationSeconds");
        _minimumTurnDurationSeconds = serializedObject.FindProperty("minimumTurnDurationSeconds");
        _timeoutAction = serializedObject.FindProperty("timeoutAction");
        _goldSeat = serializedObject.FindProperty("goldSeat");
        _silverSeat = serializedObject.FindProperty("silverSeat");
        _openingTurn = serializedObject.FindProperty("openingTurn");
        _allowOverline = serializedObject.FindProperty("allowOverline");
        _blockedAttemptConsumesTurn = serializedObject.FindProperty("blockedAttemptConsumesTurn");
        _allowBlockerVerticalWin = serializedObject.FindProperty("allowBlockerVerticalWin");
        _blockerVerticalWinLength = serializedObject.FindProperty("blockerVerticalWinLength");
        _authorityMode = serializedObject.FindProperty("authorityMode");
        _processPlacementRequestsLocally = serializedObject.FindProperty("processPlacementRequestsLocally");
        _applyStoneResultsLocally = serializedObject.FindProperty("applyStoneResultsLocally");
        _currentTurn = serializedObject.FindProperty("currentTurn");
        _currentTurnActor = serializedObject.FindProperty("currentTurnActor");
        _isMatchEnded = serializedObject.FindProperty("isMatchEnded");
        _winner = serializedObject.FindProperty("winner");
        _nextRandomRemovalColor = serializedObject.FindProperty("nextRandomRemovalColor");
        _timerTurn = serializedObject.FindProperty("timerTurn");
        _turnElapsedSeconds = serializedObject.FindProperty("turnElapsedSeconds");
        _turnRemainingSeconds = serializedObject.FindProperty("turnRemainingSeconds");
        _turnRemainingWholeSeconds = serializedObject.FindProperty("turnRemainingWholeSeconds");
        _turnTimerProgress01 = serializedObject.FindProperty("turnTimerProgress01");
        _turnTimerExpired = serializedObject.FindProperty("turnTimerExpired");
        _boardSize = serializedObject.FindProperty("boardSize");
        _totalPlacedStones = serializedObject.FindProperty("totalPlacedStones");
        _emptyCells = serializedObject.FindProperty("emptyCells");
        _goldDisplayColor = serializedObject.FindProperty("goldDisplayColor");
        _silverDisplayColor = serializedObject.FindProperty("silverDisplayColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Main", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_gameMode);
        EditorGUILayout.PropertyField(_localPlayerColor);
        if (IsSinglePlayerAiMode())
        {
            EditorGUILayout.PropertyField(_selectedAiType);
            EditorGUILayout.PropertyField(_aiTurnDelay);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Turn Timer", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_useTurnTimer);
        using (new EditorGUI.DisabledScope(!_useTurnTimer.boolValue))
        {
            EditorGUILayout.PropertyField(_turnDurationSeconds);
            EditorGUILayout.PropertyField(_timeoutAction);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Display Colors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_goldDisplayColor);
        EditorGUILayout.PropertyField(_silverDisplayColor);

        EditorGUILayout.Space(8f);
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
        if (_showAdvanced)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_openingTurn);
            EditorGUILayout.PropertyField(_allowOverline);
            EditorGUILayout.PropertyField(_blockedAttemptConsumesTurn);
            EditorGUILayout.PropertyField(_allowBlockerVerticalWin);
            EditorGUILayout.PropertyField(_blockerVerticalWinLength);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Timer Limits", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minimumTurnDurationSeconds);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Mode Derived Settings", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_useAi);
                EditorGUILayout.PropertyField(_allowManualInput);
                EditorGUILayout.PropertyField(_restrictManualDragToCurrentTurn);
                EditorGUILayout.PropertyField(_authorityMode);
            }
            EditorGUI.indentLevel--;
        }

        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            foreach (UnityEngine.Object targetObject in targets)
            {
                OmokTurnSystem turnSystem = (OmokTurnSystem)targetObject;
                turnSystem.ApplyGameModePreset();
                turnSystem.ApplySetupToRuntime();
                turnSystem.RefreshInspectorState();
                EditorUtility.SetDirty(turnSystem);
            }

            serializedObject.Update();
        }

        EditorGUILayout.Space(10f);
        _showDebug = EditorGUILayout.Foldout(_showDebug, "Debug", true);
        if (_showDebug)
        {
            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Resolved Seat Map", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_goldSeat, true);
                EditorGUILayout.PropertyField(_silverSeat, true);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Runtime Snapshot", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_currentTurn);
                EditorGUILayout.PropertyField(_currentTurnActor);
                EditorGUILayout.PropertyField(_isMatchEnded);
                EditorGUILayout.PropertyField(_winner);
                EditorGUILayout.PropertyField(_nextRandomRemovalColor);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Timer Snapshot", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_timerTurn);
                EditorGUILayout.PropertyField(_turnElapsedSeconds);
                EditorGUILayout.PropertyField(_turnRemainingSeconds);
                EditorGUILayout.PropertyField(_turnRemainingWholeSeconds);
                EditorGUILayout.PropertyField(_turnTimerProgress01);
                EditorGUILayout.PropertyField(_turnTimerExpired);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Authority Snapshot", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_processPlacementRequestsLocally);
                EditorGUILayout.PropertyField(_applyStoneResultsLocally);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Board Snapshot", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_boardSize);
                EditorGUILayout.PropertyField(_totalPlacedStones);
                EditorGUILayout.PropertyField(_emptyCells);
            }

            EditorGUILayout.Space(8f);
            if (UnityEngine.GUILayout.Button("Refresh Debug Snapshot"))
            {
                foreach (UnityEngine.Object targetObject in targets)
                {
                    OmokTurnSystem turnSystem = (OmokTurnSystem)targetObject;
                    turnSystem.RefreshInspectorState();
                    EditorUtility.SetDirty(turnSystem);
                }
            }

            using (new EditorGUI.DisabledScope(!UnityEngine.Application.isPlaying))
            {
                if (UnityEngine.GUILayout.Button("Reset Current Turn Timer"))
                {
                    foreach (UnityEngine.Object targetObject in targets)
                    {
                        OmokTurnSystem turnSystem = (OmokTurnSystem)targetObject;
                        turnSystem.ResetTurnTimer();
                        EditorUtility.SetDirty(turnSystem);
                    }
                }
            }
            EditorGUI.indentLevel--;
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return UnityEngine.Application.isPlaying;
    }

    private bool IsSinglePlayerAiMode()
    {
        return (OmokTurnGameMode)_gameMode.enumValueIndex == OmokTurnGameMode.SingleLocalVsAi;
    }
}
