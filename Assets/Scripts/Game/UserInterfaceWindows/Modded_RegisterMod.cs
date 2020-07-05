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
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Serialization;

public class Modded_RegisterMod : MonoBehaviour
{
    static Mod mod;
    public static bool UseSaveWindow { get; set; }

    public void Awake()
    {
        mod.IsReady = true;
    }

    public void Start()
    {
        UIWindowFactory.RegisterCustomUIWindow(UIWindowType.PauseOptions, typeof(Modded_DaggerfallPauseOptionsWindow));
        UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Rest, typeof(Modded_DaggerfallRestWindow));
        Debug.Log("**************Registered Windows");
    }
    
    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        //ModSettings settings = mod.GetSettings();
        
        //UseSaveWindow = settings.GetValue<bool>("GeneralSettings", "UseSaveWindow");

        var go = new GameObject(mod.Title);
        go.AddComponent<Modded_RegisterMod>();
        SaveLoadManager.Instance.RegisterPreventSaveCondition(() => !Modded_DaggerfallRestWindow.SuccessfulRest );
    }
}
