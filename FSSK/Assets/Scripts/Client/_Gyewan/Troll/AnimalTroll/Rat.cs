using UnityEngine;

// 쥐
public class Rat : AnimalTroll
{

    void Start()
    {
        _waittingTime = 3f;
    }

    protected override void OnStateEnter(AnimalState state)
    {
        if(state == AnimalState.Entering || state == AnimalState.Action || state == AnimalState.Exiting)
            _isInteractable = false;
        else
            _isInteractable = true;
    }

    protected override void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                if (_currentTime >= _enteringTime)
                    ChangeState(AnimalState.Waiting);
                break;
            case AnimalState.Waiting:
                if (_currentTime >= _waittingTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Action:
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }
}