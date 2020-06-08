// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Utility.AssetInjection;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    /// <summary>
    /// Implements popup window on escape during gameplay.
    /// </summary>
    public class Modded_DaggerfallPauseOptionsWindow : DaggerfallPauseOptionsWindow
    {

        #region Constructors

        public Modded_DaggerfallPauseOptionsWindow(IUserInterfaceManager uiManager, IUserInterfaceWindow previousWindow = null)
            : base(uiManager, previousWindow)
        {
        }

        #endregion

        #region Overrides

        protected override void Setup()
        {
			base.Setup();

            // Save game
            Button saveButton = DaggerfallUI.AddButton(new Rect(4, 4, 45, 16), optionsPanel);
            saveButton.BackgroundColor = new Color(1, 0, 0, 0.5f);
            //saveButton.OnMouseClick += SaveButton_OnMouseClick;
            //saveButton.Hotkey = DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.OptionsSave);
        }

        #endregion

        #region Private Helpers

        private static float GetDetailBarWidth(int value)
        {
            return Mathf.Lerp(0, barMaxLength, value / (float)(QualitySettings.names.Length - 1));
        }

        #endregion
    }
}
