/*
*
*	Adventure Creator
*	by Chris Burton, 2013-2020
*	
*	"ActionTemplate.cs"
* 
*	This is a blank action template.
* 
*/

using UnityEngine;
using System.Collections.Generic;
using System.Media;
#if UNITY_EDITOR
using UnityEditor;
using KinematicCharacterController.Examples;
using MxM;
#endif

namespace AC
{

	[System.Serializable]
	public class ActionAdjustMxMMovementSpeed : Action
	{

		// Declare variables here
		public GameObject objectToAffect;
		public float newMaxStableMoveSpeed, newMoveSpeed, newMaxSpeed;

		public ActionAdjustMxMMovementSpeed ()
		{
			this.isDisplayed = true;
			category = ActionCategory.Custom;
			title = "Adjust MxM Movement Speed";
			description = "Changes the MXM movement speed of the character";
		}
		
		
		public override float Run ()
		{
			if (objectToAffect && objectToAffect.GetComponent<Char>())
			{
				objectToAffect.GetComponent<ExampleCharacterController>().MaxStableMoveSpeed = newMaxStableMoveSpeed;
				objectToAffect.GetComponent<KinematicControllerWrapper>().MoveSpeed = newMoveSpeed;
				objectToAffect.GetComponentInChildren<MyTrajectoryGenerator>().MaxSpeed = newMaxSpeed;
				
			}
			return 0f;
		}


		public override void Skip ()
		{
			/*
			* This function is called when the Action is skipped, as a
			* result of the player invoking the "EndCutscene" input.
			* 
			* It should perform the instructions of the Action instantly -
			* regardless of whether or not the Action itself has been run
			* normally yet.  If this method is left blank, then skipping
			* the Action will have no effect.  If this method is removed,
			* or if the Run() method call is left below, then skipping the
			* Action will cause it to run itself as normal.
			*/

			Run ();
		}


#if UNITY_EDITOR

		public override void ShowGUI ()
		{
			// Action-specific Inspector GUI code here
			objectToAffect = (GameObject)EditorGUILayout.ObjectField("GameObject to affect:", objectToAffect, typeof(GameObject), true);
			newMaxStableMoveSpeed = EditorGUILayout.FloatField("New Max Stable Move Speed:", newMaxStableMoveSpeed);
			newMoveSpeed = EditorGUILayout.FloatField("New Move Speed:", newMoveSpeed);
			newMaxSpeed = EditorGUILayout.FloatField("New Max Speed:", newMaxSpeed);

			AfterRunningOption ();
		}
		

		public override string SetLabel ()
		{
			// (Optional) Return a string used to describe the specific action's job.

			if (objectToAffect)
			{
				return (" (" + objectToAffect.name + " - " + newMaxSpeed.ToString() + " - " + newMaxStableMoveSpeed.ToString() + " - " + newMoveSpeed.ToString() + ")");
			}
			return "";
		}

#endif

	}

}