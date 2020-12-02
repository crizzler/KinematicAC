// ================================================================================================
// File: MxMTrajectoryGenerator.cs
// 
// Authors:  Kenneth Claassen
// Date:     07-07-2019: Created this file.
// 
//     Contains a part of the 'MxM' namespace for 'Unity Engine'. 
// ================================================================================================
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using AC;

namespace MxM
{
    //===========================================================================================
    /**
    *  @brief The class / Unity component (Monobehaviour) is used to generate a trajectory in 
    *  3D space for an MxMAnimator (Motion Matching animation system). 
    *  
    *  The component has two roles. Firstly to continuously record the past trajectory positions. 
    *  This is done via the inherited MxMtrajectoryGeneratorBase class which handles past trajectory
    *  and extracting of trajectory data. 
    *  
    *  The second role of this component is to predict a future trajectory based on user input. The
    *  future trajectory is also transformed by the camera rotation so the trajectory input is always
    *  relative to the view of the player. Additionally it allows for an InputProfile to shape the
    *  trajectory so that it is Digital in nature (rather than Analog). This is important to match
    *  the viable trajectory speeds to that of viable movement speeds in the animation.
    *  
    *  This class interfaces with the MxManimator through an implementation of IMxMTrajectory. 
    *  This is mostly implemented in MxMtrajectoryGeneratorBase. Every time The MxMAnimator wants 
    *  to perform a search for best fit animation, it requests a trajectory from this component. 
    *  
    *  Note that while the Trajectory Generator may generate a trajectory of X number granular 
    *  samples, the MxMAnimator will extract and interpolate only the samples it needs based on the
    *  trajectory configuration setup by the user.
    *         
    *********************************************************************************************/
    [RequireComponent(typeof(MxMAnimator))]
    public class MyTrajectoryGenerator : MxMTrajectoryGeneratorBase
    {
        public Char player;

        [Header("Motion Settings")]
        [SerializeField] private float m_maxSpeed = 4f; //The maximum speed of the trajectory (can be modified at runtime)
        [SerializeField] private float m_posBias = 15f; //The positional responsivity of the trajectory (can be modified at runtime)
        [SerializeField] private float m_dirBias = 10f; //The rotational responsivity of the trajectory (can be modified at runtime)
        [SerializeField] private ETrajectoryMoveMode m_trajectoryMode = ETrajectoryMoveMode.Normal; //If the trajectory should behave like strafing or not.

        [SerializeField] private float m_stoppingDist = 1f;
        [Header("Input")]
        [SerializeField] public bool IsPointAndClick = false; //Whether to use custom input or not (InputVector must be set every frame if false)
        [SerializeField] private bool m_resetDirectionOnOnInput = true;

        [Header("Other")]
        [SerializeField] private Transform m_camTransform = null; //A reference to the camera transform
        [SerializeField] private MxMInputProfile m_mxmInputProfile = null; //A reference to the Input profile asset used to shape the trajectory

        private bool m_hasInputThisFrame; //A bool to cache whether there has been movement input in the current frame.
        private NativeArray<float3> m_newTrajPositions; //A native array buffer for storing the new trajectory points calculated for the current frame.

        private float m_posBiasMultiplier = 1f; //A multiplier to the positional responsivity of the trajectory generator
        private float m_dirBiasMultiplier = 1f; //A multiplier to the rotational responsivity of the trajectory generator.
        private float m_lastDesiredOrientation = 0f;

        private const string k_horizontalAxis = "Horizontal";   //Constant string for getting the horizontal axis without allocating garbage memory at runtime
        private const string k_verticalAxis = "Vertical";       //Constant string for getting the vertical axis without allocating garbage memory at runtime


        public Vector3 StrafeDirection { get; set; }

        public bool Strafing
        {
            get { return m_trajectoryMode == ETrajectoryMoveMode.Strafe ? true : false; }
            set
            {
                if (value)
                    m_trajectoryMode = ETrajectoryMoveMode.Strafe;
                else
                    m_trajectoryMode = ETrajectoryMoveMode.Normal;
            }
        }

        public bool Climbing
        {
            get { return m_trajectoryMode == ETrajectoryMoveMode.Climb ? true : false; }
            set
            {
                if (value)
                    m_trajectoryMode = ETrajectoryMoveMode.Climb;
                else
                    m_trajectoryMode = ETrajectoryMoveMode.Normal;
            }
        }

        public ETrajectoryMoveMode TrajectoryMode { get { return m_trajectoryMode; } set { m_trajectoryMode = value; } }
        public Vector3 InputVector { get; set; } //The raw input vector
        public Vector2 InputVector2D { get { return new Vector2(InputVector.x, InputVector.z); } set { InputVector = new Vector3(value.x, 0f, value.y); } }
        public Vector3 LinearInputVector { get; private set; } //The transformed input vector relative to camera
        public float MaxSpeed { get { return m_maxSpeed; } set { m_maxSpeed = value; } } //The maximum speed of the trajectory generator
        public float PositionBias { get { return m_posBias; } set { m_posBias = value; } } //The positional responsiveness of the trajectory generator
        public float DirectionBias { get { return m_dirBias; } set { m_dirBias = value; } } //The rotational responsiveness of the trajectory generator
        public MxMInputProfile InputProfile { get { return m_mxmInputProfile; } set { m_mxmInputProfile = value; } } //The input profile used to shape the trajectory generator 
        public Transform RelativeCameraTransform { get { return m_camTransform; } set { m_camTransform = value; } } //The camera transform used to make input relative to the camera.
        public float DesiredOrientation { get { return m_lastDesiredOrientation; } }

        protected override void Setup(float[] a_predictionTimes) { }

        //===========================================================================================
        /**
        *  @brief Monobehaviour Start function called once before the gameobject is updated.
        *  
        *  Ensures that the trajectory generator has a handle to the camera so that it can make
        *  inputs relative
        *         
        *********************************************************************************************/
        protected virtual void Start()
        {
            StrafeDirection = Vector3.forward;
            m_lastDesiredOrientation = transform.rotation.eulerAngles.y;
        }

        //===========================================================================================
        /**
        *  @brief Monobehaviour FixedUpdate which updates / records the past trajectory every physics
        *  update if the Animator component is set to AnimatePhysics update mode.
        *         
        *********************************************************************************************/
        public void FixedUpdate()
        {
            if (p_animator.updateMode == AnimatorUpdateMode.AnimatePhysics)
            {
                UpdatePastTrajectory();
            }
        }

        //===========================================================================================
        /**
        *  @brief Monobehaviour Update function which is called every frame that the object is active. 
        *  
        *  This updates / records the past trajectory, provided that the Animator component isn't 
        *  running in 'Animate Physics'
        *         
        *********************************************************************************************/
        public void Update()
        {
            if (p_animator.updateMode != AnimatorUpdateMode.AnimatePhysics)
            {
                UpdatePastTrajectory();
            }
        }

        //===========================================================================================
        /**
        *  @brief This is the core function of a motion matching controller. It is responsible for 
        *  updating the trajectory prediction for movement. This movement model can differ between 
        *  controllers but this controller takes a deceivingly simplistic approach. 
        *  
        *  The predicted trajectory output from this function is in evenly spaced points and it is not
        *  necessarily the trajectory passed to the MxMAnimator. Rather, the high resolution trajectory
        *  calculated here will be sampled using the ExtractMotion function before passing data to the
        *  MxMAnimator.
        *         
        *********************************************************************************************/
        protected override void UpdatePrediction()
        {
            //Desired linear velocity is calculated based on user input
            Vector3 desiredLinearVelocity = CalculateDesiredLinearVelocity();

            //Calculate the desired linear displacement over a single iteration
            Vector3 desiredLinearDisplacement = desiredLinearVelocity / p_sampleRate;

            float desiredOrientation = 0f;
            if (m_trajectoryMode != ETrajectoryMoveMode.Normal)
            {
                if (m_camTransform == null)
                {
                    desiredOrientation = Vector3.SignedAngle(Vector3.forward, StrafeDirection, Vector3.up);
                }
                else
                {
                    //If we are strafing we want to make the desired orienation relative to the camera
                    Vector3 camForward = Vector3.ProjectOnPlane(m_camTransform.forward, Vector3.up);
                    desiredOrientation = Vector3.SignedAngle(Vector3.forward, camForward, Vector3.up);
                }
            }
            else if (desiredLinearDisplacement.sqrMagnitude > 0.0001f)
            {
                desiredOrientation = Mathf.Atan2(desiredLinearDisplacement.x,
                    desiredLinearDisplacement.z) * Mathf.Rad2Deg;
            }
            else
            {
                if (m_resetDirectionOnOnInput)
                {
                    desiredOrientation = transform.rotation.eulerAngles.y;
                }
                else
                {
                    desiredOrientation = m_lastDesiredOrientation;
                }
            }

            m_lastDesiredOrientation = desiredOrientation;

            var trajectoryGenerateJob = new TrajectoryGeneratorJob()
            {
                TrajectoryPositions = p_trajPositions,
                TrajectoryRotations = p_trajFacingAngles,
                NewTrajectoryPositions = m_newTrajPositions,
                DesiredLinearDisplacement = desiredLinearDisplacement,
                DesiredOrientation = desiredOrientation,
                MoveRate = m_posBias * m_posBiasMultiplier * Time.deltaTime,
                TurnRate = m_dirBias * m_dirBiasMultiplier * Time.deltaTime
            };

            p_trajectoryGenerateJobHandle = trajectoryGenerateJob.Schedule();
        }

        //===========================================================================================
        /**
        *  @brief This function calculates the desired linear velocity based on input. This is the
        *  input multiplied by the maximum speed and modified by an input profile.
        *  
        *  In order to ensure that the generator never produces a trajectory that there is no animation
        *  for, it needs to be extended or shortened based on an input profile. The input profile 
        *  basically remaps ranges of input magnitude to a viable input magnitude. The input vector
        *  is then modified by this value as well as the responsiveness depending on the input profile.
        *  
        *  @return Vector3 - the desired linear velocity of the character.
        *         
        *********************************************************************************************/
        private Vector3 CalculateDesiredLinearVelocity()
        {

#if ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
            //Get the movement input
            if (m_trajectoryMode == ETrajectoryMoveMode.Climb)
            {

                if (!m_customInput)
                    InputVector = new Vector3(Input.GetAxis(k_horizontalAxis), Input.GetAxis(k_verticalAxis), 0f);
                else
                    InputVector = new Vector3(InputVector.x, InputVector.z, InputVector.y);

                InputVector = new Vector3(InputVector.x, InputVector.z, InputVector.y);

            }
            else
            {
                if (!m_customInput)
                    InputVector = new Vector3(Input.GetAxis(k_horizontalAxis), 0f, Input.GetAxis(k_verticalAxis));
            }
#elif ENABLE_INPUT_SYSTEM
            if (m_trajectoryMode == ETrajectoryMoveMode.Climb)
            {
                InputVector = new Vector3(InputVector.x, InputVector.z, InputVector.y);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER || !UNITY_2019_1_OR_NEWER
            if (!IsPointAndClick)
            {
                InputVector = new Vector3(Input.GetAxis(k_horizontalAxis), 0f, Input.GetAxis(k_verticalAxis));
            }
#endif
            else
            {
                float destSqr = (player.GetTargetPosition() - transform.position).sqrMagnitude;

                if (destSqr < m_stoppingDist)
                {
                    InputVector = Vector3.zero;
                    m_hasInputThisFrame = false;
                    return Vector3.zero;
                }
                InputVector = (player.GetTargetPosition() - transform.position);
            }


            //Normalize the input so it is no greater than magnitude of 1
            if (InputVector.sqrMagnitude > 1f)
            {
                InputVector = InputVector.normalized;
            }

            //If we are using an input profile then run it and get our input scale and bias multipliers
            if (m_mxmInputProfile != null)
            {
                var inputData = m_mxmInputProfile.GetInputScale(InputVector);
                InputVector *= inputData.scale;
                p_mxmAnimator.LongErrorWarpScale = 1f / inputData.scale;

                m_posBiasMultiplier = inputData.posBias;
                m_dirBiasMultiplier = inputData.dirBias;
            }
            else
            {
                m_posBiasMultiplier = 1f;
                m_dirBiasMultiplier = 1f;
            }

            if (InputVector.sqrMagnitude > 0.001f)
            {
                m_hasInputThisFrame = true;

                if (m_camTransform == null)
                {
                    return InputVector * m_maxSpeed;
                }
                else
                {
                    //Project the camera forward vector onto a ground plane
                    Vector3 forward = Vector3.ProjectOnPlane(m_camTransform.forward, Vector3.up);

                    //Rotate our input vector relative to the camera
                    LinearInputVector = Quaternion.FromToRotation(Vector3.forward, forward) * InputVector;

                    //Return our desired velocity by multiplying our input by our max speed
                    return LinearInputVector * m_maxSpeed;
                }
            }
            else
            {
                m_hasInputThisFrame = false;
            }

            //No input so just return zero
            return Vector3.zero;
        }

        //===========================================================================================
        /**
        *  @brief This function calculates the relative input vector transfromed both by the camera
        *  and then the character (in that order). This allows movement (or trajectory) input to be 
        *  dependent on camera angle. Since the trajectory is going to be transformed into the character
        *  space by the MxMAnimator, it needs to be inversely transformed by the character transform as well
        *  
        *  @return Vector3 - the relative input vector that will be used to generate a trajectory.
        *         
        *********************************************************************************************/
        public Vector3 GetRelativeInputVector()
        {
            if (m_camTransform == null)
            {
                return InputVector;
            }
            else
            {
                Vector3 forward = Vector3.ProjectOnPlane(m_camTransform.forward, Vector3.up);
                Vector3 linearInput = Quaternion.FromToRotation(Vector3.forward, forward) * InputVector;

                return transform.InverseTransformVector(linearInput);
            }
        }

        //===========================================================================================
        /**
        *  @brief Allocates and initializes any native arrays required for the trajectory generator
        *  to function.
        *         
        *********************************************************************************************/
        protected override void InitializeNativeData()
        {
            base.InitializeNativeData();

            m_newTrajPositions = new NativeArray<float3>(p_trajectoryIterations,
                Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        //===========================================================================================
        /**
        *  @brief Disposes any native data that has been created for jobs to avoid memory leaks.
        *         
        *********************************************************************************************/
        protected override void DisposeNativeData()
        {
            base.DisposeNativeData();

            if (m_newTrajPositions.IsCreated)
                m_newTrajPositions.Dispose();
        }

        //===========================================================================================
        /**
        *  @brief Checks if the trajectory generator has movement input or not. The movement input is
        *  usually cached because it is processed during the update and may need to be fetched at a 
        *  later point.
        *  
        *  @return bool - true if there is movement input, false if there is not.
        *         
        *********************************************************************************************/
        public override bool HasMovementInput()
        {
            return m_hasInputThisFrame;
        }

        //===========================================================================================
        /**
        *  @brief This function resets the 'Motion' on the trajectory generator. Essentially it sets
        *  all past and future predicted points to zero.  It is also an implementation of IMxMTrajectory
        *  
        *  This function is normally called automatically from the MxMAnimator after an event dependent
        *  on the method used 'PostEventTrajectoryHandling' (See MxMAnimator.cs). However, it can also
        *  be used to stomp all trajectory for whatever reason (e.g. when teleporting the character, 
        *  it may be useful to stomp trajectory so they don't do a running stop after teleportation)
        *         
        *********************************************************************************************/
        public override void ResetMotion(float a_rotation = 0f)
        {
            base.ResetMotion(a_rotation);

            m_hasInputThisFrame = false;
        }

        //===========================================================================================
        /**
        *  @brief Allows developers to manually set the input vector on the Trajectory Generator for
        *  custom input.
        *         
        *********************************************************************************************/
        public void SetInput(Vector2 a_input)
        {
            InputVector = new Vector3(a_input.x, 0f, a_input.y);
        }


    }//End of class: MxMTrajectoryGenerator
}//End of namespace: MxM