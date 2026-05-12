using UnityEngine;
using Photon.Pun;
using IEnumerator = System.Collections.IEnumerator;

public enum ItemType { Rum, Octopus }

public abstract class ItemTroll : TrollBase, IDraggable, IPunObservable
{
    private const float HitDestroyDelaySeconds = 1f;

    protected Rigidbody rb;
    protected bool _isGrabbed;
    protected bool _isThrown;

    [Header("Item Settings")]
    [SerializeField] protected float _throwForce = 100f;
    [SerializeField] protected ItemType _itemType;
    [SerializeField] protected AudioClip _hitSound;

    protected Vector3 _grabbedScale = Vector3.one;

    private int _throwerActorNumber = -1;
    private int _originalLayer = -1;
    private bool _hasProcessedHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void ApplyEffect() { }

    public override void EndTroll()
    {
        if (photonView.IsMine)
        {
            StartCoroutine(DelayedNetworkDestroy(3f));
        }
    }

    private IEnumerator DelayedNetworkDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (gameObject == null)
        {
            yield break;
        }

        if (photonView.IsMine || PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    public void SetGrabbedState(bool isGrabbed)
    {
        SetGrabbedState(isGrabbed, GetDefaultThrowDirection(), transform.position, GetLocalActorNumber());
    }

    public void SetGrabbedState(bool isGrabbed, Vector3 releaseDirection)
    {
        SetGrabbedState(isGrabbed, releaseDirection, transform.position, GetLocalActorNumber());
    }

    public void SetGrabbedState(bool isGrabbed, Vector3 releaseDirection, int actorNumber)
    {
        SetGrabbedState(isGrabbed, releaseDirection, transform.position, actorNumber);
    }

    public void SetGrabbedState(bool isGrabbed, Vector3 releaseDirection, Vector3 itemPosition, int actorNumber)
    {
        if (isGrabbed)
        {
            OnDragStart(actorNumber);
        }
        else
        {
            OnDragEnd(releaseDirection, itemPosition, actorNumber);
        }
    }

    public virtual void OnDragStart()
    {
        OnDragStart(GetLocalActorNumber());
    }

    protected virtual void OnDragStart(int actorNumber)
    {
        if (TrollEvents.IsGameplayEventBlocked) return;
        if (_isGrabbed) return;

        _isGrabbed = true;
        _isThrown = false;
        _hasProcessedHit = false;
        _throwerActorNumber = actorNumber;
        _originalLayer = gameObject.layer;

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        transform.localScale = _grabbedScale;
        gameObject.layer = LayerMask.NameToLayer("Ignore");
        transform.position += Vector3.up * 1f;
    }

    public void OnDragEnd()
    {
        OnDragEnd(GetDefaultThrowDirection(), transform.position, GetLocalActorNumber());
    }

    private void OnDragEnd(Vector3 releaseDirection, Vector3 releasePosition, int actorNumber)
    {
        if (TrollEvents.IsGameplayEventBlocked)
        {
            _isGrabbed = false;
            RestoreOriginalLayer();
            return;
        }

        if (!_isGrabbed) return;

        _isGrabbed = false;
        _throwerActorNumber = actorNumber;
        RestoreOriginalLayer();
        transform.position = releasePosition;

        if (rb != null)
        {
            rb.position = releasePosition;
            rb.isKinematic = false;
        }

        if (releaseDirection.sqrMagnitude <= 0.0001f)
        {
            releaseDirection = GetDefaultThrowDirection();
        }

        Throw(releaseDirection.normalized);
    }

    private void Throw(Vector3 direction)
    {
        _isThrown = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
            rb.AddForce(direction * _throwForce, ForceMode.Impulse);
        }

        EndTroll();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TrollEvents.IsGameplayEventBlocked) return;
        if (!CanProcessHitOnThisClient()) return;
        if (_hasProcessedHit || _isGrabbed || !_isThrown) return;
        if (!other.CompareTag("Player")) return;

        PhotonView targetPV = other.GetComponentInParent<PhotonView>();
        if (targetPV == null) return;

        int targetActorNumber = targetPV.OwnerActorNr;
        if (targetActorNumber == _throwerActorNumber) return;

        _hasProcessedHit = true;
        Debug.Log($"[Item Hit] {_itemType} hit actor {targetActorNumber}.");

        TrollEvents.TriggerItemCollected(gameObject.tag, gameObject);
        targetPV.RPC("RPC_ApplyItemEffect", targetPV.Owner, (int)_itemType);

        PlayHitItemSound();
        photonView.RPC(
            nameof(RPC_HitItemSound),
            RpcTarget.Others,
            _throwerActorNumber,
            targetActorNumber);
        photonView.RPC(nameof(RPC_MarkHitProcessed), RpcTarget.Others);

        RequestNetworkDestroy(HitDestroyDelaySeconds);
    }

    private bool CanProcessHitOnThisClient()
    {
        int localActorNumber = GetLocalActorNumber();
        if (_throwerActorNumber > 0)
        {
            return localActorNumber == _throwerActorNumber;
        }

        return photonView.IsMine;
    }

    [PunRPC]
    public void RPC_HitItemSound(int throwerActorNumber, int targetActorNumber)
    {
        if (TrollEvents.IsGameplayEventBlocked) return;

        int localActorNumber = GetLocalActorNumber();
        if (localActorNumber == throwerActorNumber || localActorNumber == targetActorNumber)
        {
            PlayHitItemSound();
        }
    }

    [PunRPC]
    public void RPC_MarkHitProcessed()
    {
        _hasProcessedHit = true;
    }

    [PunRPC]
    public void RPC_DestroyOnMaster(float delay)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        StartCoroutine(DelayedNetworkDestroy(delay));
    }

    private void PlayHitItemSound()
    {
        if (_hitSound == null) return;

        Debug.Log($"[Item Hit] Playing {_itemType} hit sound.");
        SoundEvents.Play3DSFX?.Invoke(_hitSound, transform.position, 0.45f);
    }

    private void RequestNetworkDestroy(float delay)
    {
        if (photonView.IsMine || PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(DelayedNetworkDestroy(delay));
            return;
        }

        photonView.RPC(nameof(RPC_DestroyOnMaster), RpcTarget.MasterClient, delay);
    }

    private Vector3 GetDefaultThrowDirection()
    {
        return Camera.main != null ? Camera.main.transform.forward : transform.forward;
    }

    private int GetLocalActorNumber()
    {
        return PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
    }

    private void RestoreOriginalLayer()
    {
        if (_originalLayer < 0) return;

        gameObject.layer = _originalLayer;
        _originalLayer = -1;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_isGrabbed);
            stream.SendNext(_isThrown);
            stream.SendNext(_throwerActorNumber);
        }
        else
        {
            _isGrabbed = (bool)stream.ReceiveNext();
            _isThrown = (bool)stream.ReceiveNext();
            _throwerActorNumber = (int)stream.ReceiveNext();
        }
    }
}
