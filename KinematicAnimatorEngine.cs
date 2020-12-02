using AC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinematicAnimatorEngine : AnimEngine
{
    KinematicControllerWrapper wrapper;


    public override void Declare(Char _character)
    {
        base.Declare(_character); 
        wrapper = character.gameObject.GetComponent<KinematicControllerWrapper>();
    }

    public override void PlayIdle()
    {
    }

    public override void ActionAnimAssignValues(ActionAnim action, List<ActionParameter> parameters)
    {
        base.ActionAnimAssignValues(action, parameters);
        Debug.Log("bitconeneeeeeeect");
    }

    public override float ActionAnimRun(ActionAnim action)
    {
        Debug.Log("heyhey " + action);
        return base.ActionAnimRun(action);
    }
}
