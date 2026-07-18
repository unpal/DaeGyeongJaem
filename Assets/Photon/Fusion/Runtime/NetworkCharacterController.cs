namespace Fusion {
  using System.Runtime.CompilerServices;
  using System.Runtime.InteropServices;
  using UnityEngine;
    using UnityEngine.UIElements;

  [StructLayout(LayoutKind.Explicit)]
  [NetworkStructWeaved(WORDS + 4)]
  public unsafe struct NetworkCCData : INetworkStruct {
    public const int WORDS = NetworkTRSPData.WORDS + 4;
    public const int SIZE  = WORDS * 4;

    [FieldOffset(0)]
    public NetworkTRSPData TRSPData;

    [FieldOffset((NetworkTRSPData.WORDS + 0) * Allocator.REPLICATE_WORD_SIZE)]
    int _grounded;

    [FieldOffset((NetworkTRSPData.WORDS + 1) * Allocator.REPLICATE_WORD_SIZE)]
    Vector3Compressed _velocityData;

    public bool Grounded {
      get => _grounded == 1;
      set => _grounded = (value ? 1 : 0);
    }

    public Vector3 Velocity {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _velocityData;
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      set => _velocityData = value;
    }
  }

  [DisallowMultipleComponent]
  [RequireComponent(typeof(CharacterController))]
  
  [NetworkBehaviourWeaved(NetworkCCData.WORDS)]
  // ReSharper disable once CheckNamespace
  public sealed unsafe class NetworkCharacterController : NetworkTRSP, INetworkTRSPTeleport, IBeforeAllTicks, IAfterAllTicks, IBeforeCopyPreviousState {
    new ref NetworkCCData Data => ref ReinterpretState<NetworkCCData>();

    [Header("Character Controller Settings")]
    public float gravity = -20.0f;
    public float jumpImpulse   = 8.0f;
    public float acceleration  = 10.0f;
    public float braking       = 10.0f;
    public float maxSpeed      = 2.0f;
    public float rotationSpeed = 15.0f;
    public bool IsClimbing;
    public bool IsDash;
    public float ClimbingSpeed = 1.0f;
    public float edgePushTime = 0.2f;
    public float edgePushTimer;
    public float edgePushForce = 5f;
    Tick _initial;
    CharacterController _controller;
    [Header("Character Force Setting")]
    public Transform FrontGroundLayCasterTrans;
    public Transform RightGroundLayCasterTrans;
    public Transform LeftGroundLayCasterTrans;
    public Transform BackGroundLayCasterTrans;
    public bool isFrontGround;
    public bool isBackGround;
    [SerializeField] private LayerMask WallLayer;


        public Vector3 Velocity {
      get => Data.Velocity;
      set => Data.Velocity = value;
    }

    public bool Grounded {
      get => Data.Grounded;
      set => Data.Grounded = value;
    }
        bool IsFrontGround()
        {
            isFrontGround = Physics.Raycast(
                FrontGroundLayCasterTrans.position,
                Vector3.down,
                0.4f,
                WallLayer);
            return isFrontGround;
        }
        bool IsBackGround()
        {
            isBackGround = Physics.Raycast(
                BackGroundLayCasterTrans.position,
                Vector3.down,
                0.4f,
                WallLayer);
            return isBackGround;
        }
        public void Teleport(Vector3? position = null, Quaternion? rotation = null) {
      _controller.enabled = false;
      NetworkTRSP.Teleport(this, transform, position, rotation);
      _controller.enabled = true;
    }


    public bool Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
      if (Data.Grounded || ignoreGrounded) {
        var newVel = Data.Velocity;
        newVel.y      += overrideImpulse ?? jumpImpulse;
        Data.Velocity =  newVel;
                return true;
      }
            return false;
        }

    public void Move(Vector3 direction) {
      var deltaTime    = Runner.DeltaTime;
      var previousPos  = transform.position;
      var moveVelocity = Data.Velocity;

      direction = direction.normalized;
      if (edgePushTimer < 0)
      {
          edgePushTimer = Runner.DeltaTime;
      }
      if (/*Data.Grounded*/IsFrontGround() && IsBackGround() && moveVelocity.y < 0) {
        moveVelocity.y = 0f;
      }

      //클라이밍이나 마지막 대쉬때는 중력 계산 안되게
     moveVelocity.y += (IsClimbing || IsDash ? 0 : gravity) * Runner.DeltaTime;

      //float verticalVelocity = 0;
      //if (IsFrontGround() && IsBackGround() && verticalVelocity < 0)
      //{
      //    verticalVelocity = 0;
      //}
      //else if (IsFrontGround() && !IsBackGround())
      //{
      //    verticalVelocity += gravity * Runner.DeltaTime;
      //    moveVelocity = transform.forward * verticalVelocity;
      //}
      //else if (!IsFrontGround() && IsBackGround())
      //{
      //    verticalVelocity += gravity * Runner.DeltaTime;
      //    moveVelocity = -transform.forward * verticalVelocity;
      //}
      //else if (!IsFrontGround() && !IsBackGround())
      //{
      //    verticalVelocity = 0;
      //}
      var horizontalVel = default(Vector3);
      horizontalVel.x = moveVelocity.x;
      if (IsClimbing)
      {
           horizontalVel.y = moveVelocity.y;
      }

            horizontalVel.z = moveVelocity.z;
      if (direction == default) {
        horizontalVel = Vector3.Lerp(horizontalVel, default, braking * deltaTime);
      } else {
        horizontalVel      = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, (IsClimbing ? ClimbingSpeed : maxSpeed));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Runner.DeltaTime);
      }

      moveVelocity.x = horizontalVel.x;
      if (IsClimbing)
      {
        moveVelocity.y = horizontalVel.y;
      }

      moveVelocity.z = horizontalVel.z;
      _controller.Move(moveVelocity * deltaTime);

      Data.Velocity = (transform.position - previousPos) * Runner.TickRate;
      if (IsDash)
      {
          Vector3 newvel = Data.Velocity;
          newvel.y += edgePushForce;
          Data.Velocity += newvel;
          IsDash = false;
          edgePushTimer = edgePushTime;
      }
      Data.Grounded = _controller.isGrounded;
    }
    
    public override void Spawned() {
      _initial = default;
      TryGetComponent(out _controller);
      // Without disabling and re-enabling the CharacterController here, the first Move call will reset the position to 0,0,0 instead of
      // keeping the position it was spawned at. Presumably disabling it clears some kind of internally cached "previous position" value
      _controller.enabled = false;
      _controller.enabled = true;
      CopyToBuffer();
    }

    public override void Render() {
      NetworkTRSP.Render(this, transform, false, false, false, ref _initial);
    }

    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount) {
      CopyToEngine();
    }

    void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount) {
      CopyToBuffer();
    }

    void IBeforeCopyPreviousState.BeforeCopyPreviousState() {
      CopyToBuffer();
    }
    
    void Awake() {
      TryGetComponent(out _controller);
    }

    void CopyToBuffer() {
      Data.TRSPData.Position = transform.position;
      Data.TRSPData.Rotation = transform.rotation;
    }

    void CopyToEngine() {
      // CC must be disabled before resetting the transform state
      _controller.enabled = false;

      // set position and rotation
      transform.SetPositionAndRotation(Data.TRSPData.Position, Data.TRSPData.Rotation);

      // Re-enable CC
      _controller.enabled = true;
    }
  }
}