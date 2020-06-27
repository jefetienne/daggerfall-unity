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

		public static bool SuccessfulRest { get; set; }
		private bool skipSave;

        public Modded_DaggerfallRestWindow(IUserInterfaceManager uiManager, bool ignoreAllocatedBed = false)
            : base(uiManager, ignoreAllocatedBed)
        {
        }

		public override void Update()
		{
			base.Update();

			if (!endedRest && currentRestMode == RestModes.Loiter) {
				skipSave = true;
			}
		}

        public override void OnPop()
        {
            base.OnPop();

			if (endedRest && !skipSave) {
				if (!enemyBrokeRest) {
					Debug.Log("************Saving....");

					SuccessfulRest = true;

					GameManager.Instance.SaveLoadManager.EnumerateSaves();
					GameManager.Instance.SaveLoadManager.Save(GameManager.Instance.PlayerEntity.Name, "RestSave");

					SuccessfulRest = false;
				} else {
					DaggerfallUI.AddHUDText("Enemies nearby, failed to save!");
				}
			}
        }
    }
}
