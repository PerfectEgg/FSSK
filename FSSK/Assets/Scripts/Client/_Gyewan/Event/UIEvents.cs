using System;
using UnityEngine;

// UI 관련 이벤트
public static class UIEvents
{
    public static Action<float, bool> OnDebuffUIUpdate; 
    public static Action OnDebuffEnded;
}