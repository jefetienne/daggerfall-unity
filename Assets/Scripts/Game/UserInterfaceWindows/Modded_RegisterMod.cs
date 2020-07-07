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
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

public class Modded_RegisterMod : MonoBehaviour
{
    public const string restSaveName = "RestSave";

    static Mod mod;
    public static bool UseSaveWindow { get; private set; }
    public static int MinRestTime { get; private set; }
    public static bool SuccessfulRest { get; private set; }

    private static Modded_RegisterMod instance;
    public static Modded_RegisterMod Instance { get { return instance; } }

    public void Awake()
    {
        mod.IsReady = true;
        instance = this;
    }

    public void Start()
    {
        UIWindowFactory.RegisterCustomUIWindow(UIWindowType.Rest, typeof(Modded_DaggerfallRestWindow));
        Debug.Log("**************Registered Rest Window");
    }
    
    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        ModSettings settings = mod.GetSettings();
        
        UseSaveWindow = settings.GetValue<bool>("GeneralSettings", "UseSaveWindow");
        
        MinRestTime = settings.GetValue<int>("GeneralSettings", "MinRestTime");
        if (MinRestTime < 1)
            MinRestTime = 1;

        var go = new GameObject(mod.Title);
        go.AddComponent<Modded_RegisterMod>();
        SaveLoadManager.Instance.RegisterPreventSaveCondition(() => !SuccessfulRest );
    }

    public static void AskBeforeSaving() {
        SuccessfulRest = true;
        Instance.StartCoroutine(PushAskBeforeSavingBox());
    }

    public static IEnumerator PushAskBeforeSavingBox() {
        yield return null;
        
        DaggerfallMessageBox modBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, DaggerfallUI.UIManager.TopWindow);

        modBox.EnableVerticalScrolling(80);

        bool saveFound = GameManager.Instance.SaveLoadManager.FindSaveFolderByNames(GameManager.Instance.PlayerEntity.Name, restSaveName) != -1;

        if (!Modded_RegisterMod.UseSaveWindow && saveFound)
            modBox.SetText(new string[] {
                "Would you like to save your game?",
                String.Empty,
                "Note: This will overwrite your last '" + restSaveName + "'." });
        else
            modBox.SetText("Would you like to save your game?");

        modBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
        modBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
        modBox.PauseWhileOpen = true;

        modBox.OnButtonClick += ((s, messageBoxButton) =>
        {
            s.CloseWindow();
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                if (Modded_RegisterMod.UseSaveWindow)
                    DaggerfallUI.UIManager.PushWindow(UIWindowFactory.GetInstanceWithArgs(UIWindowType.UnitySaveGame,
                        new object[] { DaggerfallUI.UIManager, DaggerfallUnitySaveGameWindow.Modes.SaveGame, DaggerfallUI.UIManager.TopWindow, false }));
                else
                {
                    GameManager.Instance.SaveLoadManager.EnumerateSaves();
				    GameManager.Instance.SaveLoadManager.Save(GameManager.Instance.PlayerEntity.Name, restSaveName);
                }
            }

            //is set false even before the user saves in the save window
            //so far 0.10.24 it isn't a problem because the save button does not check for save prevention once the window is open
			SuccessfulRest = false;
        });

        modBox.Show();
    }
}
