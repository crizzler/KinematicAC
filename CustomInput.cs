using MxM;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomInput : MonoBehaviour
{
    public bool IsPointAndClick = true;
    private MxMAnimator m_mxmAnimator;

    private LocomotionSpeedRamp m_locomotionSpeedRamp;

    [SerializeField]
    private MxMEventDefinition m_jumpDefinition = null;


    private EState m_curState = EState.General;

    private Vector3 m_lastPosition;
    private Vector3 m_curVelocity;


    private enum EState
    {
        General,
        Sliding,
        Jumping
    }

    // Start is called before the first frame update
    void Start()
    {
        m_mxmAnimator = GetComponentInChildren<MxMAnimator>();
        m_locomotionSpeedRamp = GetComponentInChildren<LocomotionSpeedRamp>();
    }

    // Update is called once per frame
    void Update()
    {
        if (m_locomotionSpeedRamp != null)
            m_locomotionSpeedRamp.UpdateSpeedRamp();

        Vector3 position = transform.position;
        m_curVelocity = (position - m_lastPosition) / Time.deltaTime;
        m_curVelocity.y = 0f;

        switch (m_curState)
        {
            case EState.General:
                {
                    UpdateGeneral();
                }
                break;
            case EState.Jumping:
                {
                    UpdateJump();
                }
                break;
        }

        m_lastPosition = position;
    }

    private void UpdateGeneral()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !IsPointAndClick)
        {
            if (m_jumpDefinition != null)
            {
                Jump();
            }
        }
    }

    public void Jump()
    {
        m_jumpDefinition.ClearContacts();
        m_jumpDefinition.AddDummyContacts(1);
        m_mxmAnimator.BeginEvent(m_jumpDefinition);

        ref readonly EventContact eventContact = ref m_mxmAnimator.NextEventContact_Actual_World;

        Ray ray = new Ray(eventContact.Position + (Vector3.up * 3.5f), Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit rayHit, 10f))
        {
            m_mxmAnimator.ModifyDesiredEventContactPosition(rayHit.point);
        }

        m_curState = EState.Jumping;
    }

    private void UpdateJump()
    {
        if (m_mxmAnimator.IsEventComplete)
        {
            m_curState = EState.General;
            m_lastPosition = transform.position;
            m_curVelocity = Vector3.zero;
        }
    }
}

