using KinematicCharacterController;
using MxMGameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using KinematicCharacterController.Examples;
using MxM;
using AC;

public class KinematicControllerWrapper : GenericControllerWrapper
{
    bool _hubridOn = false;
    public MxMAnimator MxMAnimator;
    public Animator Animator;
    #region ACVariables
    public AC.Char characterAC;

    public ControlState GetControlState()
    {
        if (!KickStarter.stateHandler.IsInGameplay() || !characterAC.IsPlayer || characterAC.IsMovingAlongPath() || KickStarter.settingsManager.movementMethod == MovementMethod.PointAndClick
)
        {
            if (characterAC.charState == CharState.Move)
            {
                return ControlState.UnityPathfinding;
            }
            else
            {
                return ControlState.ACTurning;
            }
        }
        return ControlState.UnderDirectControl;
    }

    public enum ControlState { Null, UnderDirectControl, UnityPathfinding, ACTurning };

    private ControlState controlState = ControlState.Null;
    #endregion

    #region MovementVariables
    public MyTrajectoryGenerator mxMTrajectoryGenerator;
    [SerializeField]
    private bool m_applyGravityWhenGrounded = false;

    private KinematicCharacterMotor _kinematicCharacterMotor;

    [SerializeField] private ExampleCharacterController _characterController;
    [SerializeField] private CustomInput _customInput;
	public float MoveSpeed;
    #endregion

    #region GenericWrapperOverrides

    private bool m_enableCollision = true;

    public override bool IsGrounded { get { return _kinematicCharacterMotor.GroundingStatus.IsStableOnGround; } }
    public override bool ApplyGravityWhenGrounded { get { return m_applyGravityWhenGrounded; } }
    public override Vector3 Velocity { get { return _kinematicCharacterMotor.Velocity; } }
    public override float MaxStepHeight
    {
        get { return _kinematicCharacterMotor.MaxStepHeight; }
        set {  }
    }
    public override float Height
    {
        get { return _kinematicCharacterMotor.Capsule.height; }
        set { _kinematicCharacterMotor.Capsule.height = value; }
    }

    public override Vector3 Center
    {
        get { return _kinematicCharacterMotor.CharacterTransformToCapsuleCenter; }
        set {}
    }
    public override float Radius
    {
        get { return _kinematicCharacterMotor.Capsule.radius; }
        set { _kinematicCharacterMotor.Capsule.radius = value; }
    }

    public override void Initialize()
    {
        //m_characterController.enableOverlapRecovery = true;
    }

    public override Vector3 GetCachedMoveDelta() { return Vector3.zero; }
    public override Quaternion GetCachedRotDelta() { return Quaternion.identity; }

    //Gets or sets whether collision is enabled (GenericControllerWrapper override)
    public override bool CollisionEnabled
    {
        get { return m_enableCollision; }
        set
        {
            if (m_enableCollision != value)
            {
                if (m_enableCollision)
                    _kinematicCharacterMotor.enabled = false;
                else
                    _kinematicCharacterMotor.enabled = true;
            }

            m_enableCollision = value;
        }
    }

    #endregion
    //============================================================================================
    /**
    *  @brief Sets up the wrapper, getting a reference to an attached character controller
    *         
    *********************************************************************************************/
    private void Awake()
    {
        _kinematicCharacterMotor = GetComponent<KinematicCharacterMotor>();

        Assert.IsNotNull(_kinematicCharacterMotor, "Error (UnityControllerWrapper): Could not find" +
            "CharacterController component");

        //m_characterController.enableOverlapRecovery = true;
    }

    //============================================================================================
    /**
    *  @brief Moves the controller by an absolute Vector3 tranlation. Delta time must be applied
    *  before passing a_move to this function.
    *  
    *  @param [Vector3] a_move - the movement vector
    *         
    *********************************************************************************************/

    public void Animate(string animation = "")
    {
        _hubridOn = true;
        if (animation == "")
        {
            MxMAnimator.BlendInController(.5f);
            return;
        }
        Animator.SetTrigger(animation);
    }

    public override void Move(Vector3 a_move)
    {
        if (_hubridOn && ( mxMTrajectoryGenerator.InputVector != Vector3.zero || Input.GetKeyDown(KeyCode.Space)))
        {
            _hubridOn = false;
            MxMAnimator.BlendOutController(.5f);
        }

        UpdateControlState();
        if (m_enableCollision)
        {
            UpdateMovement(a_move);
        }
    }
    private void UpdateMovement(Vector3 a_move)
    {
        switch (controlState)

        {

            case ControlState.UnderDirectControl:

                UpdateDirectInput(a_move);

                break;



            case ControlState.UnityPathfinding:

                UpdatePathfindInput(a_move);

                break;



            case ControlState.ACTurning:

                UpdateJustTurningInput();

                break;

        }
    }

    public void UpdateJustTurningInput()
    {
        // Apply inputs to character
        //_characterController.SetInputs(ref characterInputs);
        // Stop regular input, halt movement, and use GetFrameRotation to manually enforce the per-frame rotation
        AICharacterInputs inputs = new AICharacterInputs();

        _characterController.SetInputs(ref inputs);
        _characterController.Motor.BaseVelocity = Vector3.zero;
	    // _characterController.Motor.SetRotation(characterAC.GetFrameRotation()); // edit

    }

    private void UpdatePathfindInput(Vector3 a_move)
    {
        // Read the AC.Char script's GetTargetPosition and GetTargetRotation methods to dictate how the Controller / Motor should be affected
        /*

        
        AICharacterInputs inputs = new AICharacterInputs
        {
            MoveVector = targetDirection
        };
        _characterController.SetInputs(ref inputs);
        _characterController.Motor.RotateCharacter(characterAC.GetTargetRotation());*/
        /*Vector3 targetPosition = characterAC.GetTargetPosition();
        Vector3 targetDirection = (targetPosition - _characterController.Motor.TransientPosition).normalized;*/


        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs
        {
            // Build the CharacterInputs struct
            MoveAxisForward = a_move.z * MoveSpeed,
            MoveAxisRight = a_move.x * MoveSpeed,
            CameraRotation = Quaternion.identity,
        };
        // Apply inputs to character
        _characterController.SetInputs(ref characterInputs);

    }

    private void UpdateDirectInput(Vector3 a_move)
    {
        /*if (a_move.x == 0 && a_move.z == 0 && !Input.GetKeyDown(KeyCode.Space))
        {
            AICharacterInputs inputs = new AICharacterInputs();

            _characterController.SetInputs(ref inputs);
            _characterController.Motor.BaseVelocity = Vector3.zero;
            return;
        }*/

        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs
        {
            // Build the CharacterInputs struct
            MoveAxisForward = a_move.z * MoveSpeed,
            MoveAxisRight = a_move.x * MoveSpeed,
            CameraRotation = Quaternion.identity,
            JumpDown = Input.GetKeyDown(KeyCode.Space)
        };
        // Apply inputs to character
        _characterController.SetInputs(ref characterInputs);
    }

    //change State based on AC
    private void UpdateControlState()
    {
        // Check if we want to determine the character's position through AC, or just through direct input
        if (!KickStarter.stateHandler.IsInGameplay() || !characterAC.IsPlayer || characterAC.IsMovingAlongPath() || KickStarter.settingsManager.movementMethod == MovementMethod.PointAndClick)

        {
            // Check if we want to make the character pathfind, or do nothing while AC turns them

            if (characterAC.charState == CharState.Move)

            {
                mxMTrajectoryGenerator.IsPointAndClick = true;
                if (_customInput!= null)
                {
                    _customInput.IsPointAndClick = true;
                }
                controlState = ControlState.UnityPathfinding;
            }

            else

            {
                if (_customInput != null)
                {
                    _customInput.IsPointAndClick = true;
                }

                mxMTrajectoryGenerator.IsPointAndClick = true;
                controlState = ControlState.ACTurning;

            }

        }
        else
        {
            mxMTrajectoryGenerator.IsPointAndClick = false;

            if (_customInput != null)
            {
                _customInput.IsPointAndClick = false;
            }

            controlState = ControlState.UnderDirectControl;

        }

    }
    private void OnTeleport()
    {
        // This function is called by the Char script whenever the character has been teleported.
        _characterController.Motor.BaseVelocity = Vector3.zero;

        _characterController.Motor.SetPosition(characterAC.GetTargetPosition());

        _characterController.Motor.SetRotation(characterAC.GetTargetRotation());



        if (_characterController.OrientationMethod == OrientationMethod.TowardsCamera)

        {

            ACDebug.LogWarning("Setting rotation to " + characterAC.GetTargetRotation() + ", but may not stick since the Orienation Method is Towards Camera.", gameObject);

        }

    }
    //============================================================================================
    /**
    *  @brief Moves the controller by an absolute Vector3 tranlation and sets the new rotation
    *  of the controller. Delta time must be applied before passing a_move to this function.
    *  
    *  @param [Vector3] a_move - the movement vector
    *  @param [Quaternion] a_newRotation - the new rotation for the character
    *         
    *********************************************************************************************/
    public override void MoveAndRotate(Vector3 a_move, Quaternion a_rotDelta)
    {
        //_kinematicCharacterMotor.transform.rotation *= a_rotDelta;

        /*if (m_enableCollision)
        {
            _kinematicCharacterMotor.MoveCharacter(a_move);
        }
        else
        {
            _kinematicCharacterMotor.transform.Translate(a_move, Space.World);
        }*/
    }

    //============================================================================================
    /**
    *  @brief Sets the rotation of the character contorller with interpolation if supported
    *  
    *  @param [Quaternion] a_newRotation - the new rotation for the character
    *         
    *********************************************************************************************/
    public override void Rotate(Quaternion a_rotDelta)
    {
        //_kinematicCharacterMotor.transform.rotation *= a_rotDelta;
    }

    //============================================================================================
    /**
    *  @brief Monobehaviour OnEnable function which simply passes the enabling status onto the
    *  controller compopnnet
    *         
    *********************************************************************************************/
    private void OnEnable()
    {
        _kinematicCharacterMotor.enabled = true;
    }

    //============================================================================================
    /**
    *  @brief Monobehaviour OnDisable function which simply passes the disabling status onto the
    *  controller compopnnet
    *         
    *********************************************************************************************/
    private void OnDisable()
    {
        _kinematicCharacterMotor.enabled = true;
    }

    //============================================================================================
    /**
    *  @brief Sets the position of the character controller (teleport)
    *  
    *  @param [Vector3] a_position - the new position
    *         
    *********************************************************************************************/
    public override void SetPosition(Vector3 a_position)
    {
        //transform.position = a_position;
    }

    //============================================================================================
    /**
    *  @brief Sets the rotation of the character controller (teleport)
    *         
    *  @param [Quaternion] a_rotation - the new rotation        
    *         
    *********************************************************************************************/
    public override void SetRotation(Quaternion a_rotation)
    {
        //transform.rotation = a_rotation;
    }

    //============================================================================================
    /**
    *  @brief Sets the position and rotation of the character controller (teleport)
    *  
    *  @param [Vector3] a_position - the new position
    *  @param [Quaternion] a_rotation - the new rotation    
    *         
    *********************************************************************************************/
    public override void SetPositionAndRotation(Vector3 a_position, Quaternion a_rotation)
    {
        //transform.SetPositionAndRotation(a_position, a_rotation);
    }
}
