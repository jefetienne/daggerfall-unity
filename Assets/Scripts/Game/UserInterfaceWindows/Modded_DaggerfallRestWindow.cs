// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Hazelnut, Numidium
// 
// Notes:
//

using UnityEngine;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Banking;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class Modded_DaggerfallRestWindow : DaggerfallRestWindow
    {

		private bool rested = false;

        public Modded_DaggerfallRestWindow(IUserInterfaceManager uiManager, bool ignoreAllocatedBed = false)
            : base(uiManager, ignoreAllocatedBed)
        {
        }

		public override void Update()
		{
			base.Update();

			if (!rested && (currentRestMode == RestModes.FullRest || currentRestMode == RestModes.TimedRest)) {
				rested = true;
				Debug.Log("**********Resting!");
			}
		}

        public override void OnPop()
        {
            base.OnPop();

			if (rested) {
				if (!enemyBrokeRest) {
					Debug.Log("************Saving....");
					GameManager.Instance.SaveLoadManager.EnumerateSaves();
					GameManager.Instance.SaveLoadManager.Save(GameManager.Instance.PlayerEntity.Name, "RestSave");
				} else {
					DaggerfallUI.AddHUDText("Enemies nearby, failed to save!");
				}
			}
        }
    }
}
