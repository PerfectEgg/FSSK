using UnityEngine;

// 쥐
public class Rat : AnimalTroll
{
    private float danceTime = 1.5f;

    void Start()
    {
        waittingTime = 3f;
    }

    protected override void OnStateEnter(AnimalState state)
    {
        if(state == AnimalState.Entering || state == AnimalState.Action || state == AnimalState.Exiting)
            isInteractable = false;
        else
            isInteractable = true;
    }

    protected override void UpdateState()
    {
        switch(currentState)
        {
            case AnimalState.Entering:
                if (currentTime >= enteringTime)
                    ChangeState(AnimalState.Waiting);
                break;
            case AnimalState.Waiting:
                if (currentTime >= waittingTime)
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