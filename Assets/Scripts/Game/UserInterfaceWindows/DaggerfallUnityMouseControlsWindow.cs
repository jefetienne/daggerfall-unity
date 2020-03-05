// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2020 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors: Justin Steele
//
// Notes:
//

using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    /// <summary>
    /// Implements mouse controls window.
    /// </summary>
    public class DaggerfallUnityMouseControlsWindow : DaggerfallPopupWindow
    {

        #region Fields

        Color keybindButtonBackgroundColor;

        Panel mainPanel;
        TextLabel titleLabel;
        Button escapeKeybindButton = new Button();
        Button consoleKeybindButton = new Button();
        //Button screenshotKeybindButton = new Button();
        Button quickSaveKeybindButton = new Button();
        Button quickLoadKeybindButton = new Button();

        public List<Button> buttonGroup = new List<Button>();

        bool waitingForInput = false;

        #endregion

        #region Constructors

        public DaggerfallUnityMouseControlsWindow(IUserInterfaceManager uiManager, DaggerfallBaseWindow previous = null)
            : base(uiManager, previous)
        {
        }

        #endregion

        #region Unity

        public override void Update()
        {
            base.Update();

            if (!AllowCancel && !waitingForInput && Input.GetKeyDown(KeyCode.Escape))
            {
                DaggerfallControlsWindow.ShowMultipleAssignmentsMessage(uiManager, this);
            }
        }

        #endregion

        #region Setup

        protected override void Setup()
        {
            // Always dim background
            ParentPanel.BackgroundColor = ScreenDimColor;

            keybindButtonBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);

            // Main panel
            Color mainPanelBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            Vector2 mainPanelSize = new Vector2(280, 170);
            mainPanel = new Panel();
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            mainPanel.Size = mainPanelSize;
            mainPanel.Outline.Enabled = true;
            SetBackground(mainPanel, mainPanelBackgroundColor, "mainPanelBackgroundColor");
            NativePanel.Components.Add(mainPanel);

            // Title label
            titleLabel = new TextLabel();
            titleLabel.ShadowPosition = Vector2.zero;
            titleLabel.Position = new Vector2(4, 4);
            titleLabel.Text = "Configure Advanced Controls";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.Components.Add(titleLabel);

            // keybind buttons
            SetupKeybindButton(escapeKeybindButton, InputManager.Actions.Escape, 0, 20);
            SetupKeybindButton(consoleKeybindButton, InputManager.Actions.ToggleConsole, 0, 40);
            //SetupKeybindButton(screenshotKeybindButton, InputManager.Actions.Jump, 90, 20);
            SetupKeybindButton(quickSaveKeybindButton, InputManager.Actions.QuickSave, 90, 40);
            SetupKeybindButton(quickLoadKeybindButton, InputManager.Actions.QuickLoad, 180, 20);

            // Continue
            //Button continueButton = DaggerfallUI.AddButton(new Rect(20, 109, 68, 18), mainPanel);
            //continueButton.OnMouseClick += ContinueButton_OnMouseClick;
        }

        #endregion

        #region Overrides

        public override void OnPush()
        {
            OnReturn();
        }

        public override void OnReturn()
        {
            UpdateKeybindButtons();
            CheckDuplicates();
        }

        #endregion

        #region Private methods

        //for "reset defaults" overload
        //**might delete this, since reset defaults is in the main controls window
        private void SetupKeybindButton(Button button, InputManager.Actions action)
        {
            button.Label.Text = DaggerfallControlsWindow.unsavedKeybindDict[action];//InputManager.Instance.GetBinding(action).ToString();
            button.Label.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
        }

        //for initialization
        private void SetupKeybindButton(Button button, InputManager.Actions action, int x, int y)
        {
            Panel panel = new Panel();
            panel.Position = new Vector2(x, y);
            panel.Size = new Vector2(85, 15);

            Panel labelPanel = new Panel();
            labelPanel.Size = new Vector2(40, 10);
            labelPanel.Position = new Vector2(0, 0);
            labelPanel.HorizontalAlignment = HorizontalAlignment.Left;
            labelPanel.VerticalAlignment = VerticalAlignment.Middle;

            TextLabel label = new TextLabel();
            label.Position = new Vector2(0, 0);
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.VerticalAlignment = VerticalAlignment.Middle;
            label.ShadowPosition = Vector2.zero;
            label.Text = action.ToString();
            label.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;

            button.Name = action.ToString();
            button.Label.ShadowPosition = Vector2.zero;
            button.Size = new Vector2(43, 10);
            button.Position = new Vector2(43, 0);
            button.HorizontalAlignment = HorizontalAlignment.Right;
            button.VerticalAlignment = VerticalAlignment.Middle;

            SetBackground(button, keybindButtonBackgroundColor, "advancedKeybindBackgroundColor");
            button.OnMouseClick += KeybindButton_OnMouseClick;

            buttonGroup.Add(button);

            labelPanel.Components.Add(label);
            panel.Components.Add(labelPanel);
            panel.Components.Add(button);
            mainPanel.Components.Add(panel);

            SetupKeybindButton(button, action);
        }

        private void UpdateKeybindButtons()
        {
            SetupKeybindButton(escapeKeybindButton, InputManager.Actions.Escape);
            SetupKeybindButton(consoleKeybindButton, InputManager.Actions.ToggleConsole);
            //SetupKeybindButton(screenshotKeybindButton, InputManager.Actions.Jump);
            SetupKeybindButton(quickSaveKeybindButton, InputManager.Actions.QuickSave);
            SetupKeybindButton(quickLoadKeybindButton, InputManager.Actions.QuickLoad);
        }

        //from DaggerfallUnitySaveGameWindow
        void SetBackground(BaseScreenComponent panel, Color color, string textureName)
        {
            Texture2D tex;
            if (TextureReplacement.TryImportTexture(textureName, true, out tex))
            {
                panel.BackgroundTexture = tex;
                TextureReplacement.LogLegacyUICustomizationMessage(textureName);
            }
            else
                panel.BackgroundColor = color;
        }

        private void CheckDuplicates()
        {
            IEnumerable<String> keyList = DaggerfallControlsWindow.unsavedKeybindDict.Select(kv => kv.Value); //buttonGroup.Select(b => b.Label.Text).ToList();

            var dupes = DaggerfallControlsWindow.GetDuplicates(keyList);

            foreach (Button keybindButton in buttonGroup)
            {
                if (dupes.Contains(keybindButton.Label.Text))
                    keybindButton.Label.TextColor = new Color(1, 0, 0);
                else
                    keybindButton.Label.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
            }
        }

        //a workaround solution to setting the 'waitingForInput' instance variable in a
        //static IEnumerator method. IEnumerator methods can't accept out/ref parameters
        private void SetWaitingForInput(bool b)
        {
            waitingForInput = b;
            AllowCancel = !b;
        }

        #endregion

        #region Event Handlers

        private void ContinueButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            CancelWindow();
        }

        private void KeybindButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            Button thisKeybindButton = (Button)sender;
            if (!waitingForInput)
                InputManager.Instance.StartCoroutine(WaitForKeyPress(thisKeybindButton));
        }

        IEnumerator WaitForKeyPress(Button button)
        {
            yield return DaggerfallControlsWindow.WaitForKeyPress(button, CheckDuplicates, SetWaitingForInput);
        }

        #endregion

    }
}