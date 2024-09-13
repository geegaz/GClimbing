
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Paddings;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GClimbingSystem : UdonSharpBehaviour
{  
    [SerializeField] private Transform HandTransform;
    [SerializeField] private LayerMask climableMask;
    [SerializeField] private Material climbingHighlightMaterial;

    [Header("Walljump")]
    [Space]
    [SerializeField] private bool walljumpEnabled = true;
    [SerializeField] private float walljumpSpeed = 5f;

    [Header("Climbing")]
    [Space]
    [SerializeField] private bool velocityBufferEnabled = true;

    [Header("VR Settings")]
    [SerializeField] private bool useGrabButton = true;
    [SerializeField] private float handRadius = 0.1f;
    [SerializeField] private float maxFlingSpeed = 6f;
    [SerializeField] private float flingSpeedMultiplier = 1.2f;

    [Header("Desktop Settings")]
    [SerializeField] private float headReach = 2f;
    [SerializeField] private float headDistance = 0.5f;
    [SerializeField] private float headMoveSpeed = 5f;

    [Header("Events")]
    [SerializeField] private bool _sendEventsToClimbedObjects = true;
    [SerializeField] private UdonBehaviour[] _eventTargets;
    [SerializeField] private string _grabbedEvent = "ClimbingGrabbed";
    [SerializeField] private string _droppedEvent = "ClimbingDropped";

    // Desktop-specific
    private Vector3 _lastHeadDir;
    private float _lastHeadDistance;

    // Climbing & velocity
    private Vector3 _lastClimbedVelocity;
    private Vector3 _lastTransformPosition;
    private Vector3 _lastTransformVelocity;
    private Transform _lastClimbedTransform;

    private Collider[] grabSurfaces = new Collider[1];
    private Vector3[] _velocityBuffer = new Vector3[5];

    [NonSerialized] public bool climbing = false;
    [NonSerialized] public HandType climbingHand;
    
    // Cache local player and VR status
    [NonSerialized] public VRCPlayerApi localPlayer;
    [NonSerialized] public bool inVR;

    private bool _holdingMouseLeft;

    private float _leftHandHighlight = 0f;
    private float _rightHandHighlight = 0f;
    

    #region Events

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (localPlayer != null) inVR = localPlayer.IsUserInVR();
        if (HandTransform) HandTransform.localScale = Vector3.one * handRadius;
    }

    private void Update() 
    {
        if (climbingHighlightMaterial) {
            UpdateMaterial();
        }
    }

    public override void PostLateUpdate()
    {
        if (climbing) {
            UpdateGrab(climbingHand);
        }
    }

    #endregion

    #region Inputs

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (value && walljumpEnabled && climbing) {
            // Let go with jump force
            Drop(Vector3.up * walljumpSpeed);
        }
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            if (useGrabButton) return; // Skip execution
            ProcessInput(value, args.handType);
        }
        else {
            // The Use input is always Left Click on Desktop
            ProcessInput(value, HandType.LEFT);
        }
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            if (!useGrabButton) return; // Skip execution
            ProcessInput(value, args.handType);
        }
    }

    public override void InputDrop(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            Debug.LogError("Climbing system detected InputDrop event in VR - this is not handled");
            return; // Not handled - skip execution
        }
        else {
            // The Drop input is always Right Click on PC
            ProcessInput(value, HandType.RIGHT);
        }
    }


    private void ProcessInput(bool value, HandType hand) 
    {
        if (inVR) {
            if (value && !IsClimbingWith(hand))
                if (TestGrabVR(hand))
                    Grab(hand);
            if (!value && IsClimbingWith(hand))
                Drop();
        }
        else {
            // Desktop
            if (hand == HandType.LEFT) {
                if (value && TestGrabDesktop()) {
                    _holdingMouseLeft = true; // Don't change the head distance
                    Grab(HandType.RIGHT); // Use the right hand by default
                }
                else {
                    _holdingMouseLeft = false;
                    if (climbing && localPlayer.IsPlayerGrounded())
                        Drop(); // Drop if we let go while on ground
                }
            }
            else {
                if (!value && climbing && !HasPickup(VRC_Pickup.PickupHand.Right))
                    Drop(); // Drop on right click if the player doesn't have a pickup
            }
        }
    }

    #endregion

    #region Climbing Actions

    private void UpdateMaterial() 
    {
        if (inVR) {
            // Update from tracked hand position
            GetHandPos(HandType.RIGHT, out Vector3 rightHandPos);
            GetHandPos(HandType.LEFT, out Vector3 leftHandPos);

            _rightHandHighlight = Mathf.Lerp(_rightHandHighlight, climbing && climbingHand == HandType.RIGHT ? 0f : 0.5f, 0.2f);
            _leftHandHighlight = Mathf.Lerp(_leftHandHighlight, climbing && climbingHand == HandType.LEFT ? 0f : 0.5f, 0.2f);

            climbingHighlightMaterial.SetFloat("_RightHandDist", climbing && climbingHand == HandType.RIGHT ? 0f : handRadius);
            climbingHighlightMaterial.SetFloat("_LeftHandDist", climbing && climbingHand == HandType.LEFT ? 0f : handRadius);
            climbingHighlightMaterial.SetVector("_RightHandPosition", new Vector4(rightHandPos.x, rightHandPos.y, rightHandPos.z, _rightHandHighlight));
            climbingHighlightMaterial.SetVector("_LeftHandPosition", new Vector4(leftHandPos.x, leftHandPos.y, leftHandPos.z, _leftHandHighlight));
        }
        else {
            // Update from raycast
            GetHeadPos(out Vector3 headPos, out Vector3 headDir);

            Vector3 targetPos;
            if (Physics.Raycast(headPos, headDir, out RaycastHit hit, headReach, climableMask, QueryTriggerInteraction.Collide)) {
                targetPos = hit.point;
                climbingHighlightMaterial.SetFloat("_RightHandDist", climbing && _holdingMouseLeft ? 0f : handRadius);
            }
                
            else {
                targetPos = headPos + headDir * headReach;
                climbingHighlightMaterial.SetFloat("_RightHandDist", 0f);
            }
            _rightHandHighlight = Mathf.Lerp(_rightHandHighlight, climbing && _holdingMouseLeft ? 0f : 0.5f, 0.2f);
            climbingHighlightMaterial.SetVector("_RightHandPosition", new Vector4(targetPos.x, targetPos.y, targetPos.z, _rightHandHighlight));
        }
    }

    private void UpdateGrab(HandType hand) 
    {
        Vector3 climbingPos;
        if (inVR) {
            GetHandPos(hand, out climbingPos);
        }
        else {
            GetHeadPos(out Vector3 headPos, out Vector3 headDir);
            
            if (_holdingMouseLeft) // Update head offset based on mouse dir while mouse is pressed
                _lastHeadDir = headDir;
            if (_lastHeadDistance > headDistance)
                _lastHeadDistance = Mathf.MoveTowards(_lastHeadDistance, headDistance, headMoveSpeed * Time.deltaTime);
            climbingPos = (headPos + _lastHeadDir * _lastHeadDistance);
        }

        // Calculate climbing velocity (total velocity to reach the grabbing point)
        Vector3 climbingOffset = HandTransform.position - climbingPos;
        _lastClimbedVelocity = climbingOffset * (1.0f / Time.deltaTime);

        // Calculate object velocity (velocity of the object we're grabbing on)
        Vector3 transformOffset = HandTransform.position - _lastTransformPosition;
        _lastTransformVelocity = transformOffset * (1.0f / Time.deltaTime);
        _lastTransformPosition = HandTransform.position;

        // Store velocity in buffer for smoothing on drop
        if (velocityBufferEnabled)
            _velocityBuffer[Time.frameCount % _velocityBuffer.Length] = _lastClimbedVelocity;

        // Apply velocity
        localPlayer.SetVelocity(_lastClimbedVelocity);
    }

    public void Grab(HandType hand) 
    {
        // Reset last velocity
        _lastClimbedVelocity = Vector3.zero;
        _lastTransformVelocity = Vector3.zero;
        _lastTransformPosition = HandTransform.position;
        //localPlayer.SetGravityStrength(0f);

        // Send events
        if (climbing) SendDroppedEvent(_lastClimbedTransform.gameObject); // previous climbed object
        SendGrabbedEvent(HandTransform.parent.gameObject); // current climbed object

        climbingHand = hand;
        climbing = true;
    }

    public void Drop() 
    {
        Drop(Vector3.zero);
    }

    public void Drop(Vector3 boost) 
    {
        if (velocityBufferEnabled) {
            // Make an average of the previous velocities
            // to fake the conservation of force when letting go
            var vel = Vector3.zero;
            for (int i = 0; i < _velocityBuffer.Length; i++)
            {
                vel += _velocityBuffer[i];
            }
            _lastClimbedVelocity = vel / _velocityBuffer.Length;
        }
        
        _lastClimbedVelocity = _lastTransformVelocity + 
            Vector3.ClampMagnitude(_lastClimbedVelocity - _lastTransformVelocity, maxFlingSpeed) * flingSpeedMultiplier;
        localPlayer.SetVelocity(_lastClimbedVelocity + boost);
        //localPlayer.SetGravityStrength(1f);

        // Send events
        SendDroppedEvent(HandTransform.parent.gameObject);

        climbing = false;
    }

    public void DropGrabbed(Transform tf) {
        if (IsGrabbing(tf)) Drop();
    }

    public void ForceGrab(Transform tf, HandType hand, Vector3 offset) {
        HandTransform.position = tf.position + offset;
        HandTransform.parent = tf;

        Grab(hand);
    }

    private bool TestGrabVR(HandType hand) 
    {
        GetHandPos(hand, out Vector3 handPos);

        if(Physics.OverlapSphereNonAlloc(handPos, handRadius, grabSurfaces, climableMask, QueryTriggerInteraction.Collide) >= 1) {
            // Store previous transform to send let go events
            _lastClimbedTransform = HandTransform.parent;
            // Reparent hand transform to new parent
            HandTransform.position = handPos; // Don't move hand in VR
            HandTransform.parent = grabSurfaces[0].transform;
            return true;
        }
        return false;
    }

    private bool TestGrabDesktop() 
    {
        GetHeadPos(out Vector3 headPos, out Vector3 headDir);

        if (Physics.Raycast(headPos, headDir, out RaycastHit hit, headReach, climableMask, QueryTriggerInteraction.Collide)) {
            // Store previous transform to send let go events
            _lastClimbedTransform = HandTransform.parent;
            // Reparent hand transform to new parent
            HandTransform.position = hit.point;
            HandTransform.parent = hit.transform;
            // Store head distance to move smoothly there
            _lastHeadDistance = Vector3.Distance(headPos, hit.point);
            return true;
        }
        return false;
    }

    #endregion

    #region Climbing Utilities

    public bool IsClimbingWith(HandType hand) {
        return climbing && climbingHand == hand;
    }

    public bool IsGrabbing(Transform tf) {
        if (climbing) {
            return HandTransform.parent == tf;
        }
        return false;
    }

    public bool HasPickup(VRC_Pickup.PickupHand hand) {
        return localPlayer.GetPickupInHand(hand) != null;
    }

    private void GetHandPos(HandType hand, out Vector3 hand_pos) {
        VRCPlayerApi.TrackingData handTrackingData = hand == HandType.LEFT ? localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand) : localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        hand_pos = handTrackingData.position;
    }

    private void GetHeadPos(out Vector3 head_pos, out Vector3 head_dir) {
         VRCPlayerApi.TrackingData headTrackingData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
         head_pos = headTrackingData.position;
         head_dir = headTrackingData.rotation * Vector3.forward;
    }

    private void SendGrabbedEvent(GameObject climbed_object) {
        if (_sendEventsToClimbedObjects) {
            UdonBehaviour behavior = (UdonBehaviour)climbed_object.GetComponent(typeof(UdonBehaviour));
            if (behavior) behavior.SendCustomEvent(_grabbedEvent);
        }
        foreach (UdonBehaviour target in _eventTargets)
        {
            target.SendCustomEvent(_grabbedEvent);
        }

    }

    private void SendDroppedEvent(GameObject climbed_object) {
        if (_sendEventsToClimbedObjects) {
            UdonBehaviour behavior = (UdonBehaviour)climbed_object.GetComponent(typeof(UdonBehaviour));
            if (behavior) behavior.SendCustomEvent(_droppedEvent);
        }
        foreach (UdonBehaviour target in _eventTargets)
        {
            target.SendCustomEvent(_droppedEvent);
        }
    }

    #endregion

}

