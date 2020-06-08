using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using FullSerializer;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Player;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility.AssetInjection;

namespace DaggerfallWorkshop.Game.Serialization
{
	public class ModdedSaveLoadManager : SaveLoadManager {

		private const string restSaveName = "RestSave";
		const string notReadyExceptionText = "SaveLoad not ready.";

        public new void Save(string characterName, string saveName, bool instantReload = false)
        {
			if(saveName != restSaveName)
				DaggerfallUI.MessageBox("Can only save while resting.");
				return;

			base.Save(characterName, saveName, instantReload);
        }

        public new void QuickSave(bool instantReload = false)
        {
            if (!LoadInProgress)
            {
                if (GameManager.Instance.SaveLoadManager.IsSavingPrevented)
                    DaggerfallUI.MessageBox(TextManager.Instance.GetText("DaggerfallUI", "cannotSaveNow"));
                else
                    Save(GameManager.Instance.PlayerEntity.Name, restSaveName, instantReload);
            }
        }

        public new void QuickLoad()
        {
            Load(GameManager.Instance.PlayerEntity.Name, restSaveName);
        }

        /// <summary>
        /// Checks if quick save folder exists.
        /// </summary>
        /// <returns>True if quick save exists.</returns>
        public new bool HasQuickSave(string characterName)
        {
            // Look for existing save with this character and name
            int key = FindSaveFolderByNames(characterName, restSaveName);

            // Get folder
            return key != -1;
        }

	}
}