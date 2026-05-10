using UnityEngine;

public class OmokBlockerTarget : MonoBehaviour
{
    [SerializeField] private bool blocksStone = true;
    [SerializeField] private bool keepBlockedStone = true;
    [SerializeField] private OmokBlockerAttachmentMode attachmentMode = OmokBlockerAttachmentMode.SurfaceContact;
    [SerializeField] private bool consumeTurnWhenBlocked = true;
    [SerializeField] private bool countForBlockerStackWin = true;
    [SerializeField] private Transform attachmentTarget;
    [SerializeField] private bool followAnimatedVisualSurface = true;

    public bool BlocksStone => blocksStone;
    public bool KeepBlockedStone => keepBlockedStone;
    public OmokBlockerAttachmentMode AttachmentMode => attachmentMode;
    public bool ConsumeTurnWhenBlocked => consumeTurnWhenBlocked;
    public bool CountForBlockerStackWin => countForBlockerStackWin && keepBlockedStone;
    public Transform AttachmentTarget => attachmentTarget;
    public bool FollowAnimatedVisualSurface => followAnimatedVisualSurface;
}
