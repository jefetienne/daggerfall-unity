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
using DaggerfallWorkshop.Utility.AssetInjection;

namespace Modded_Tooltips_Interaction
{
    public class Modded_HUDTooltipWindow : BaseScreenComponent
    {

        #region Fields

        #region Raycasting
        //Use the farthest distance
        const float rayDistance = PlayerActivate.StaticNPCActivationDistance;

        GameObject mainCamera;
        int playerLayerMask = 0;
        Transform prevHit;
        string prevText;
        GameObject goDoor;
        BoxCollider goDoorCollider;
        StaticDoor prevDoor;
        string prevDoorText;
        float prevDistance;

        #endregion

        PlayerEnterExit playerEnterExit;
        PlayerGPS playerGPS;
        PlayerActivate playerActivate;

        #region Stolen methods/variables/properties

        byte[] openHours;
        byte[] closeHours;
        MethodInfo buildingIsUnlockedMethodInfo;
        MethodInfo getBuildingLockValueMethodInfo;

        Panel nativePanel;

        #endregion

        #region Tooltip

        const int defaultMarginSize = 2;

        DaggerfallFont font;
        Vector2 mouseOffset = new Vector2(0, 4);
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
            // Tooltip
            font = DaggerfallUI.DefaultFont;
            BackgroundColor = DaggerfallUI.DaggerfallUnityDefaultToolTipBackgroundColor;
            SetMargins(Margins.All, defaultMarginSize);

            // Raycasting
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            playerLayerMask = ~(1 << LayerMask.NameToLayer("Player"));

            // Reading
            playerEnterExit = GameManager.Instance.PlayerEnterExit;
            playerGPS = GameManager.Instance.PlayerGPS;
            playerActivate = GameManager.Instance.PlayerActivate;

            // Stealing hidden variables/methods

            Type type = DaggerfallUI.Instance.DaggerfallHUD.GetType();
            var prop = type.BaseType.GetProperty(
                "NativePanel",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            nativePanel = (Panel)prop.GetValue(DaggerfallUI.Instance.DaggerfallHUD);

            type = GameManager.Instance.PlayerActivate.GetType();

            buildingIsUnlockedMethodInfo = type.GetMethod(
                "BuildingIsUnlocked",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            getBuildingLockValueMethodInfo = type.GetMethod(
                "GetBuildingLockValue",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                null,
                new Type[] { typeof(BuildingSummary) },
                null);
            
            openHours = (byte[])type.GetField(
                "openHours",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .GetValue(playerActivate);

            closeHours = (byte[])type.GetField(
                "closeHours",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .GetValue(playerActivate);
        }

        #endregion

        #region Public Methods

        public override void Update()
        {
            base.Update();

            // Weird bug occurs when the player is clicking on a static door from a distance because the activation creates another "goDoor"
            // which overlaps and prevents the player from going in. So we must delete the tooltip's goDoor beforehand if the player is activating
            if (InputManager.Instance.ActionComplete(InputManager.Actions.ActivateCenterObject))
            {
                GameObject.Destroy(goDoor);
                goDoor = null;
                goDoorCollider = null;
            }

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

                if (isSame && hit.distance <= prevDistance)
                {
                    return prevText;
                }
                else
                {
                    object comp;
                    string ret = null;

                    if (hit.distance <= PlayerActivate.DefaultActivationDistance)
                    {
                        if (CheckComponent<DaggerfallAction>(hit, out comp))
                        {
                            if (((DaggerfallAction)comp).TriggerFlag == DFBlock.RdbTriggerFlags.Direct)
                            {
                                var name = hit.transform.GetComponent<MeshFilter>().mesh.name.Split(' ')[0];
                                var record = Convert.ToInt32(name);
                                switch (record)
                                {
                                    case 61027:
                                    case 61028:
                                        ret = "Lever";
                                        break;
                                    default:
                                        ret = "<Interact>";
                                        break;
                                }

                                prevDistance = PlayerActivate.DefaultActivationDistance;
                            }
                        }
                        else if (CheckComponent<DaggerfallLadder>(hit, out comp))
                        {
                            ret = "Ladder";
                            prevDistance = PlayerActivate.DefaultActivationDistance;
                        }
                        else if (CheckComponent<DaggerfallBookshelf>(hit, out comp))
                        {
                            ret = "Bookshelf";
                            prevDistance = PlayerActivate.DefaultActivationDistance;
                        }
                    }

                    if (string.IsNullOrEmpty(ret) && hit.distance <= PlayerActivate.MobileNPCActivationDistance)
                    {
                        if (CheckComponent<MobilePersonNPC>(hit, out comp))
                        {
                            ret = ((MobilePersonNPC)comp).NameNPC;
                            prevDistance = PlayerActivate.MobileNPCActivationDistance;
                        }
                        else if (CheckComponent<DaggerfallEntityBehaviour>(hit, out comp))
                        {
                            EnemyMotor enemyMotor = ((DaggerfallEntityBehaviour)comp).transform.GetComponent<EnemyMotor>();

                            if (!enemyMotor || !enemyMotor.IsHostile)
                            {
                                ret = ((DaggerfallEntityBehaviour)comp).Entity.Name;
                                prevDistance = PlayerActivate.MobileNPCActivationDistance;
                            }

                        }
                        else if (CheckComponent<DaggerfallBulletinBoard>(hit, out comp))
                        {
                            ret = "Bulletin Board";
                            prevDistance = PlayerActivate.MobileNPCActivationDistance;
                        }
                    }

                    if (string.IsNullOrEmpty(ret) && hit.distance <= PlayerActivate.StaticNPCActivationDistance)
                    {
                        if (CheckComponent<StaticNPC>(hit, out comp))
                        {
                            ret = ((StaticNPC)comp).DisplayName;
                            prevDistance = PlayerActivate.StaticNPCActivationDistance;
                        }
                    }

                    if (string.IsNullOrEmpty(ret) && CheckComponent<DaggerfallLoot>(hit, out comp))
                    {
                        var loot = (DaggerfallLoot)comp;

                        if (loot.ContainerType == LootContainerTypes.CorpseMarker && hit.distance <= PlayerActivate.CorpseActivationDistance)
                        {
                            ret = loot.entityName + " (dead)";
                            prevDistance = PlayerActivate.CorpseActivationDistance;
                        }
                        else if (hit.distance <= PlayerActivate.TreasureActivationDistance)
                        {
                            prevDistance = PlayerActivate.TreasureActivationDistance;
                            switch (loot.ContainerType)
                            {
                                case LootContainerTypes.DroppedLoot:
                                case LootContainerTypes.RandomTreasure:
                                    if (loot.Items.Count == 1)
                                    {
                                        var item = loot.Items.GetItem(0);
                                        ret = item.LongName;

                                        if (item.stackCount > 1)
                                            ret += " (" + item.stackCount + ")";
                                    }
                                    else
                                    {
                                        ret = "Loot Pile";
                                    }
                                    break;
                                case LootContainerTypes.ShopShelves:
                                    ret = "Shop Shelf";
                                    break;
                                case LootContainerTypes.HouseContainers:
                                    var name = hit.transform.GetComponent<MeshFilter>().mesh.name.Split(' ')[0];
                                    var record = Convert.ToInt32(name);
                                    switch (record)
                                    {
                                        case 41003:
                                        case 41800:
                                        case 41803:
                                            ret = "Wardrobe";
                                            break;
                                        case 41004:
                                        case 41007:
                                        case 41008:
                                        case 41033:
                                        case 41038:
                                        case 41801:
                                        case 41805:
                                        case 41810:
                                        case 41802:
                                            ret = "Cabinets";
                                            break;
                                        case 41027:
                                            ret = "Shelf";
                                            break;
                                        case 41032:
                                        case 41034:
                                        case 41050:
                                        case 41806:
                                            ret = "Dresser";
                                            break;
                                        case 41035:
                                        case 41037:
                                        case 41051:
                                        case 41807:
                                        case 41804:
                                        case 41808:
                                        case 41809:
                                        case 41814:
                                            ret = "Cupboard";
                                            break;
                                        case 41815:
                                        case 41816:
                                        case 41817:
                                        case 41818:
                                        case 41819:
                                        case 41820:
                                        case 41821:
                                        case 41822:
                                        case 41823:
                                        case 41824:
                                        case 41825:
                                        case 41826:
                                        case 41827:
                                        case 41828:
                                        case 41829:
                                        case 41830:
                                        case 41831:
                                        case 41832:
                                        case 41833:
                                        case 41834:
                                            ret = "Crate";
                                            break;
                                        case 41811:
                                        case 41812:
                                        case 41813:
                                            ret = "Chest";
                                            break;
                                    }
                                    break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(ret) && hit.distance <= PlayerActivate.DoorActivationDistance)
                    {
                        if (CheckComponent<DaggerfallActionDoor>(hit, out comp))
                        {
                            var door = (DaggerfallActionDoor)comp;
                            if (!door.IsLocked)
                                ret = "Door";
                            else
                                ret = "Door\rLock Level: "+door.CurrentLockValue;

                            prevDistance = PlayerActivate.DoorActivationDistance;
                        }
                    }

                    if (string.IsNullOrEmpty(ret))
                    {
                        Transform doorOwner;
                        DaggerfallStaticDoors doors = GetDoors(hit.transform, out doorOwner);
                        if (doors)
                        {
                            ret = GetStaticDoorText(doors, hit, doorOwner);
                            prevDistance = PlayerActivate.DoorActivationDistance;
                        }
                    }

                    prevText = ret;

                    return ret;
                }
            }

            return null;
        }

        string GetStaticDoorText(DaggerfallStaticDoors doors, RaycastHit hit, Transform doorOwner)
        {
            StaticDoor door;

            //Debug.Log("GETSTATICDOORTEXT"+hashit);

            if (hit.distance <= PlayerActivate.DoorActivationDistance
                && (HasHit(doors, hit.point, out door) || CustomDoor.HasHit(hit, out door)))
            {
                if (door.doorType == DoorTypes.Building && !playerEnterExit.IsPlayerInside)
                {
                    // Check for a static building hit
                    StaticBuilding building;
                    DFLocation.BuildingTypes buildingType;
                    bool buildingUnlocked;
                    int buildingLockValue;

                    Transform buildingOwner;
                    DaggerfallStaticBuildings buildings = GetBuildings(hit.transform, out buildingOwner);
                    if (buildings && buildings.HasHit(hit.point, out building))
                    {
                        // Get building directory for location
                        BuildingDirectory buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                        if (!buildingDirectory)
                            return "<ERR: 010>";

                        // Get detailed building data from directory
                        BuildingSummary buildingSummary;
                        if (!buildingDirectory.GetBuildingSummary(building.buildingKey, out buildingSummary))
                            return "<ERR: 011>";

                        buildingUnlocked = BuildingIsUnlocked(buildingSummary);
                        buildingLockValue = GetBuildingLockValue(buildingSummary);
                        buildingType = buildingSummary.BuildingType;

                        // Discover building
                        playerGPS.DiscoverBuilding(building.buildingKey);

                        // Get discovered building
                        PlayerGPS.DiscoveredBuilding db;
                        if (playerGPS.GetDiscoveredBuilding(building.buildingKey, out db))
                        {
                            // Check against quest system for an overriding quest-assigned display name for this building
                            string tooltip = "To\r" + db.displayName;

                            if (!buildingUnlocked && buildingLockValue > 0)
                                tooltip += "\rLock Level: " + buildingLockValue;

                            if (!buildingUnlocked && buildingType < DFLocation.BuildingTypes.Temple
                                && buildingType != DFLocation.BuildingTypes.HouseForSale)
                            {
                                string buildingClosedMessage = (buildingType == DFLocation.BuildingTypes.GuildHall) 
                                                                ? TextManager.Instance.GetLocalizedText("guildClosed")
                                                                : TextManager.Instance.GetLocalizedText("storeClosed");

                                buildingClosedMessage = buildingClosedMessage.Replace("%d1", openHours[(int)buildingType].ToString());
                                buildingClosedMessage = buildingClosedMessage.Replace("%d2", closeHours[(int)buildingType].ToString());
                                tooltip += "\r" + buildingClosedMessage;
                            }

                            prevDoorText = tooltip;

                            return tooltip;
                        }
                    }

                    //If we caught ourselves hitting the same door again directly without touching the building, just return the previous text which should be the door's
                    return prevDoorText;
                }
                else if (door.doorType == DoorTypes.Building && playerEnterExit.IsPlayerInside)
                {
                    // Hit door while inside, transition outside
                    return "To\r" + playerGPS.CurrentLocation.Name;
                }
                else if (door.doorType == DoorTypes.DungeonEntrance && !playerEnterExit.IsPlayerInside)
                {
                    // Hit dungeon door while outside, transition inside
                    return "To\r" + playerGPS.CurrentLocation.Name;
                }
                else if (door.doorType == DoorTypes.DungeonExit && playerEnterExit.IsPlayerInside)
                {
                    // Hit dungeon exit while inside, ask if access wagon or transition outside
                    if (playerGPS.CurrentLocationType == DFRegion.LocationTypes.TownCity
                        || playerGPS.CurrentLocationType == DFRegion.LocationTypes.TownHamlet
                        || playerGPS.CurrentLocationType == DFRegion.LocationTypes.TownVillage)
                        return "To\r" + playerGPS.CurrentLocation.Name;
                    else
                        return "To\r" + playerGPS.CurrentRegion.Name + " Region";
                }
            }

            prevHit = null;

            //Debug.Log(5);
            return null;
        }

        /// <summary>
        /// Check for a door hit in world space.
        /// </summary>
        /// <param name="point">Hit point from ray test in world space.</param>
        /// <param name="doorOut">StaticDoor out if hit found.</param>
        /// <returns>True if point hits a static door.</returns>
        public bool HasHit(DaggerfallStaticDoors dfuStaticDoors, Vector3 point, out StaticDoor doorOut)
        {
            //Debug.Log("HasHit started");
            doorOut = new StaticDoor();

            if (dfuStaticDoors.Doors == null)
                return false;

            var Doors = dfuStaticDoors.Doors;

            // Using a single hidden trigger created when testing door positions
            // This avoids problems with AABBs as trigger rotates nicely with model transform
            // A trigger is also more useful for debugging as its drawn by editor
            if (goDoor == null)
            {
                goDoor = new GameObject();
                goDoor.hideFlags = HideFlags.HideAndDontSave;
                goDoor.transform.parent = dfuStaticDoors.transform;
                goDoorCollider = goDoor.AddComponent<BoxCollider>();
                goDoorCollider.isTrigger = true;
            }

            BoxCollider c = goDoorCollider;
            bool found = false;

            if (goDoor && prevHit == goDoor.transform && c.bounds.Contains(point))
            {
                //Debug.Log("EARLY FOUND");
                found = true;
                doorOut = prevDoor;
            }

            // Test each door in array

            for (int i = 0; !found && i < Doors.Length; i++)
            {
                //Debug.Log("DOORS ITERATE"+i);
                Quaternion buildingRotation = GameObjectHelper.QuaternionFromMatrix(Doors[i].buildingMatrix);
                Vector3 doorNormal = buildingRotation * Doors[i].normal;
                Quaternion facingRotation = Quaternion.LookRotation(doorNormal, Vector3.up);

                // Setup single trigger position and size over each door in turn
                // This method plays nice with transforms
                c.size = Doors[i].size;
                goDoor.transform.parent = dfuStaticDoors.transform;
                goDoor.transform.position = dfuStaticDoors.transform.rotation * Doors[i].buildingMatrix.MultiplyPoint3x4(Doors[i].centre);
                goDoor.transform.position += dfuStaticDoors.transform.position;
                goDoor.transform.rotation = facingRotation;

                // Check if hit was inside trigger
                if (c.bounds.Contains(point))
                {
                    //Debug.Log("HasHit FOUND");
                    found = true;
                    doorOut = Doors[i];
                    if (doorOut.doorType == DoorTypes.DungeonExit)
                        break;
                }
            }

            // Remove temp trigger
            if (!found && goDoor)
            {
                //Debug.Log("DESTROY");
                GameObject.Destroy(goDoor);
                goDoor = null;
                goDoorCollider = null;
            }
            else if (found)
            {
                prevHit = goDoor.transform;
                prevDoor = doorOut;
            }

            return found;
        }

        private bool CheckComponent<T>(RaycastHit hit, out object obj)
        {
            obj = hit.transform.GetComponent<T>();
            return obj != null;
        }

        // Look for doors on object, then on direct parent
        private DaggerfallStaticDoors GetDoors(Transform doorsTransform, out Transform owner)
        {
            owner = null;
            DaggerfallStaticDoors doors = doorsTransform.GetComponent<DaggerfallStaticDoors>();
            if (!doors)
            {
                doors = doorsTransform.GetComponentInParent<DaggerfallStaticDoors>();
                if (doors)
                    owner = doors.transform;
            }
            else
            {
                owner = doors.transform;
            }

            return doors;
        }

        // Look for building array on object, then on direct parent
        private DaggerfallStaticBuildings GetBuildings(Transform buildingsTransform, out Transform owner)
        {
            owner = null;
            DaggerfallStaticBuildings buildings = buildingsTransform.GetComponent<DaggerfallStaticBuildings>();
            if (!buildings)
            {
                buildings = buildingsTransform.GetComponentInParent<DaggerfallStaticBuildings>();
                if (buildings)
                    owner = buildings.transform;
            }
            else
            {
                owner = buildings.transform;
            }

            return buildings;
        }
        private bool BuildingIsUnlocked(BuildingSummary buildingSummary)
        {
            return (bool)buildingIsUnlockedMethodInfo.Invoke(playerActivate, new object[] { buildingSummary });
        }

        private int GetBuildingLockValue(BuildingSummary buildingSummary)
        {
            return (int)getBuildingLockValueMethodInfo.Invoke(playerActivate, new object[] { buildingSummary });
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
                    float temp = textPos.x;
                    var calc = font.CalculateTextWidth(textRows[i], Scale);
                    textPos.x = (rect.x) + ((rect.width - calc) / 2 * Scale.x) - (rect.width * Scale.x / 3f) - (LeftMargin / 2 * Scale.x);
                    //Debug.Log(rect.x+","+rect.width+","+calc+","+textPos.x);
                    font.DrawText(textRows[i], textPos, Scale, textColor);
                    textPos.y += font.GlyphHeight * Scale.y;
                    textPos.x = temp;
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