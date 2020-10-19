using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Save;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;

public class Modded_SneakCrouchFusion : MonoBehaviour {

	void Update ()
    {
        // Force toggle sneak
        if (!GameManager.Instance.SpeedChanger.ToggleSneak)
        {
            GameManager.Instance.SpeedChanger.ToggleSneak = true;
        }

        //if crouch, do sneak. If sneak, do crouch.
        if (InputManager.Instance.ActionStarted(InputManager.Actions.Crouch))
        {
            InputManager.Instance.AddAction(InputManager.Actions.Sneak);
        }
        else if (InputManager.Instance.ActionStarted(InputManager.Actions.Sneak))
        {
            InputManager.Instance.AddAction(InputManager.Actions.Crouch);
        }

        //in the case of running while crouching/sneaking - go back to sneaking after finishing running
        if (GameManager.Instance.PlayerMotor.IsCrouching 
            && !GameManager.Instance.SpeedChanger.isRunning 
            && !GameManager.Instance.SpeedChanger.isSneaking)
        {
            InputManager.Instance.AddAction(InputManager.Actions.Sneak);
        }
	}

    [Invoke(StateManager.StateTypes.Game)]
    public static void InitAtGameState(InitParams initParams)
    {
        Debug.Log("**********Init Modded_SneakCrouchFusion");
        var pl = GameObject.FindGameObjectWithTag("Player");
        pl.AddComponent<Modded_SneakCrouchFusion>();
        Debug.Log("************Added Modded_SneakCrouchFusion component");
    }
}