using System;
using System.Linq;
using System.Reflection;
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

namespace Modded_Tooltips_Interaction
{
    public class Modded_HUDTooltipWindow : BaseScreenComponent
    {

        #region Fields
        GameObject mainCamera;
        int playerLayerMask = 0;
        const float rayDistance = 4;
        Transform prevHit;
        string prevText;

        Panel nativePanel;

        const int defaultMarginSize = 2;

        DaggerfallFont font;
        float toolTipDelay = 0;
        Vector2 mouseOffset = new Vector2(0, 4);
        private int currentCursorHeight = -1;
        private int currentSystemHeight;
        private int currentRenderingHeight;
        private bool currentFullScreen;

        bool drawToolTip = false;
        Color textColor = DaggerfallUI.DaggerfallUnityDefaultToolTipTextColor;

        string[] textRows;
        float widestRow = 0;
        string lastText = string.Empty;
        bool previousSDFState;

        #endregion

        [Invoke(StateManager.StateTypes.Game)]
        public static void InitAtGameState(InitParams initParams)
        {
            Debug.Log("****************************tooltips2");
            var tooltip = new Modded_HUDTooltipWindow();

            DaggerfallUI.Instance.DaggerfallHUD.ParentPanel.Components.Add(tooltip);
            
        }

        #region Constructors

        public Modded_HUDTooltipWindow()
        {
            font = DaggerfallUI.DefaultFont;
            BackgroundColor = DaggerfallUI.DaggerfallUnityDefaultToolTipBackgroundColor;
            SetMargins(Margins.All, defaultMarginSize);

            Type type = DaggerfallUI.Instance.DaggerfallHUD.GetType();
            var prop = type.BaseType.GetProperty("NativePanel",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            nativePanel = (Panel)prop.GetValue(DaggerfallUI.Instance.DaggerfallHUD);

            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));
        }

        #endregion

        #region Public Methods

        public override void Update()
        {
            base.Update();
            if (Display.main.systemHeight != currentSystemHeight ||
                Display.main.renderingHeight != currentRenderingHeight || 
                DaggerfallUnity.Settings.Fullscreen != currentFullScreen)
                UpdateMouseOffset();
            
            Scale = nativePanel.LocalScale;
            AutoSize = AutoSizeModes.Scale;

            var text = GetHoverText();
            if (!string.IsNullOrEmpty(text))
            {
                Draw(text);
            }
        }
        
        private string GetHoverText()
        {
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

            RaycastHit hit;
            bool hitSomething = Physics.Raycast(ray, out hit, rayDistance, playerLayerMask);

            bool isSame = hit.transform == prevHit;

            if (hitSomething)
            {
                prevHit = hit.transform;

                if (isSame)
                {
                    return prevText;
                }
                else
                {
                    object comp;
                    string ret = null;

                    if (CheckComponent<StaticNPC>(hit, out comp))
                    {
                        ret = ((StaticNPC)comp).DisplayName;
                    }
                    else if (CheckComponent<MobilePersonNPC>(hit, out comp))
                    {
                        ret = ((MobilePersonNPC)comp).NameNPC;
                    }
                    else if (CheckComponent<DaggerfallEntityBehaviour>(hit, out comp))
                    {
                        ret = ((DaggerfallEntityBehaviour)comp).Entity.Name;
                    }
                    else if (CheckComponent<DaggerfallActionDoor>(hit, out comp))
                    {
                        var door = (DaggerfallActionDoor)comp;
                        if (!door.IsLocked)
                            ret = "Door";
                        else
                            ret = "Door\rLock Level: "+door.CurrentLockValue;
                    }
                    /*else if (CheckComponent<DaggerfallActionDoor>(hit, out comp))
                    {

                    }*/

                    prevText = ret;

                    return ret;
                }
            }

            return null;
        }

        private bool CheckComponent<T>(RaycastHit hit, out object obj)
        {
            obj = hit.transform.GetComponent<T>();
            return obj != null;
        }

        private void UpdateMouseOffset()
        {
            currentSystemHeight = Display.main.systemHeight;
            currentRenderingHeight = Display.main.renderingHeight;
            currentFullScreen = DaggerfallUnity.Settings.Fullscreen;
            mouseOffset = new Vector2(0, 0/*currentCursorHeight * 200f*/ / (currentFullScreen ? currentSystemHeight : currentRenderingHeight));
        }

        /// <summary>
        /// Flags tooltip to be drawn at end of UI update.
        /// </summary>
        /// <param name="text">Text to render inside tooltip.</param>
        public void Draw(string text)
        {
            // Validate
            if (font == null || string.IsNullOrEmpty(text))
            {
                drawToolTip = false;
                return;
            }

            // Update text rows
            UpdateTextRows(text);
            if (textRows == null || textRows.Length == 0)
            {
                drawToolTip = false;
                return;
            }

            // Set tooltip size
            Size = new Vector2(
                (widestRow + LeftMargin + RightMargin),
                (font.GlyphHeight * textRows.Length + TopMargin + BottomMargin - 1));

            // Set tooltip position
            Position = new Vector2(Screen.width / 2, currentFullScreen ? currentSystemHeight / 2 : currentRenderingHeight / 2) + mouseOffset;

            // Ensure tooltip inside screen area
            Rect rect = Rectangle;
            if (rect.xMax > Screen.width)
            {
                float difference = (rect.xMax - Screen.width) * 1f / Scale.x;
                Vector2 newPosition = new Vector2(Position.x - difference, Position.y);
                Position = newPosition;
            }
            if (rect.yMax > Screen.height)
            {
                float difference = (rect.yMax - Screen.height) * 1f / Scale.y;
                Vector2 newPosition = new Vector2(Position.x, Position.y - difference);
                Position = newPosition;
            }

            // Check if mouse position is in parent's rectangle (to prevent tooltips out of panel's rectangle to be displayed)
            if (1 == 1)
            {
                // Raise flag to draw tooltip
                drawToolTip = true;
            }
        }

        public override void Draw()
        {
            if (!Enabled)
                return;

            if (drawToolTip)
            {
                base.Draw();

                // Set render area for tooltip to whole screen (material might have been changed by other component, i.e. _ScissorRect might have been set to a subarea of screen (e.g. by TextLabel class))
                Material material = font.GetMaterial();
                Vector4 scissorRect = new Vector4(0, 1, 0, 1);
                material.SetVector("_ScissorRect", scissorRect);

                // Determine text position
                Rect rect = Rectangle;
                Vector2 textPos = new Vector2(
                    rect.x + LeftMargin * Scale.x,
                    rect.y + TopMargin * Scale.y);

                //if (rect.xMax > Screen.width) textPos.x -= (rect.xMax - Screen.width);

                // Draw tooltip text
                for (int i = 0; i < textRows.Length; i++)
                {
                    font.DrawText(textRows[i], textPos, Scale, textColor);
                    textPos.y += font.GlyphHeight * Scale.y;
                }

                // Lower flag
                drawToolTip = false;
            }
        }

        #endregion

        #region Private Methods

        void UpdateTextRows(string text)
        {
            // Do nothing if text has not changed since last time
            bool sdfState = font.IsSDFCapable;
            if (text == lastText && sdfState == previousSDFState)
                return;

            // Split into rows based on \r escape character
            // Text read from plain-text files will become \\r so need to replace this first
            text = text.Replace("\\r", "\r");
            textRows = text.Split('\r');

            // Set text we just processed
            lastText = text;

            // Find widest row
            widestRow = 0;
            for (int i = 0; i < textRows.Length; i++)
            {
                float width = font.CalculateTextWidth(textRows[i], Scale);
                if (width > widestRow)
                    widestRow = width;
            }
            previousSDFState = sdfState;
        }

        #endregion
    }
}