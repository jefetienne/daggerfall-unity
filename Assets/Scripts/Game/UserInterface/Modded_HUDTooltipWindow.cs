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
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Utility.AssetInjection;

namespace Modded_Tooltips_Interaction
{
    public class Modded_HUDTooltipWindow : Panel
    {

        #region Fields

        #region Raycasting
        //Use the farthest distance
        const float rayDistance = PlayerActivate.StaticNPCActivationDistance;

        GameObject mainCamera;
        int playerLayerMask = 0;

        //Caching
        Transform prevHit;
        string prevText;

        GameObject goDoor;
        BoxCollider goDoorCollider;
        StaticDoor prevDoor;
        string prevDoorText;
        float prevDistance;

        Transform prevDoorCheckTransform;
        Transform prevDoorOwner;
        DaggerfallStaticDoors prevStaticDoors;

        #endregion

        PlayerEnterExit playerEnterExit;
        PlayerGPS playerGPS;
        PlayerActivate playerActivate;
        HUDTooltip tooltip;
        #region Stolen methods/variables/properties

        byte[] openHours;
        byte[] closeHours;
        MethodInfo buildingIsUnlockedMethodInfo;
        MethodInfo getBuildingLockValueMethodInfo;

        Panel nativePanel;

        #endregion

        #endregion

        [Invoke(StateManager.StateTypes.Game)]
        public static void InitAtGameState(InitParams initParams)
        {
            Debug.Log("****************************tooltips2");
            var ttw = new Modded_HUDTooltipWindow();

            Type type = DaggerfallUI.Instance.DaggerfallHUD.GetType();
            var prop = type.BaseType.GetProperty(
                "NativePanel",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var nativePanel = (Panel)prop.GetValue(DaggerfallUI.Instance.DaggerfallHUD);

            nativePanel.Components.Add(ttw);
        }

        #region Constructors

        public Modded_HUDTooltipWindow()
        {
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

            tooltip = new HUDTooltip();
            //tooltip.Parent = this;
            this.Components.Add(tooltip);
        }

        #endregion

        #region Public Methods

        public override void Draw() {
            base.Draw();
            tooltip.Draw();
        }

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
            
            //Scale = nativePanel.LocalScale;
            Size = nativePanel.Size;
            AutoSize = AutoSizeModes.None;

            var text = GetHoverText();
            if (!string.IsNullOrEmpty(text))
            {
                tooltip.Draw(text);
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
                    if (hit.distance <= prevDistance)
                        return prevText;
                    else
                        return null;
                }
                else
                {
                    object comp;
                    string ret = null;

                    if (hit.transform.name.Length > 16 && hit.transform.name.Substring(0, 16) == "DaggerfallTerrain")
                        return null;

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
                            var npc = ((StaticNPC)comp);
                            if (CheckComponent<DaggerfallBillboard>(hit, out comp))
                            {
                                var bb = ((DaggerfallBillboard)comp);
                                var archive = bb.Summary.Archive;
                                var index = bb.Summary.Record;

                                if (archive == 175)
                                {
                                    switch (index)
                                    {
                                        case 0:
                                            ret = "Azura";
                                            break;
                                        case 1:
                                            ret = "Boethiah";
                                            break;
                                        case 2:
                                            ret = "Clavicus Vile";
                                            break;
                                        case 3:
                                            ret = "Hircine";
                                            break;
                                        case 4:
                                            ret = "Hermaeus Mora";
                                            break;
                                        case 5:
                                            ret = "Malacath";
                                            break;
                                        case 6:
                                            ret = "Mehrunes Dagon";
                                            break;
                                        case 7:
                                            ret = "Mephala";
                                            break;
                                        case 8:
                                            ret = "Meridia";
                                            break;
                                        case 9:
                                            ret = "Molag Bal";
                                            break;
                                        case 10:
                                            ret = "Namira";
                                            break;
                                        case 11:
                                            ret = "Nocturnal";
                                            break;
                                        case 12:
                                            ret = "Peryite";
                                            break;
                                        case 13:
                                            ret = "Sanguine";
                                            break;
                                        case 14:
                                            ret = "Sheogorath";
                                            break;
                                        case 15:
                                            ret = "Vaermina";
                                            break;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(ret))
                                ret = npc.DisplayName;

                            prevDistance = PlayerActivate.StaticNPCActivationDistance;
                        }
                    }

                    if (hit.distance <= PlayerActivate.DefaultActivationDistance)
                    {
                        if (CheckComponent<DaggerfallAction>(hit, out comp))
                        {
                            var da = (DaggerfallAction)comp;
                            if (da.TriggerFlag == DFBlock.RdbTriggerFlags.Direct
                                || da.TriggerFlag == DFBlock.RdbTriggerFlags.Direct6
                                || da.TriggerFlag == DFBlock.RdbTriggerFlags.MultiTrigger)
                            {
                                bool multiTriggerOkay = false;
                                var mesh = hit.transform.GetComponent<MeshFilter>();
                                if (mesh)
                                {
                                    var ind = mesh.name.IndexOf('=');
                                    string str;
                                    // "DaggerfallMesh [ID=XXXXX]"
                                    if (ind >= 0)
                                        str = mesh.name.Substring(ind + 1, mesh.name.Length - 1 - ind - 1);
                                    else
                                        str = mesh.name.Split(' ')[0];

                                    int record;
                                    if (int.TryParse(str, out record))
                                    {
                                        switch (record)
                                        {
                                            case 74037:
                                                ret = "Wheel";
                                                multiTriggerOkay = true;
                                                break;
                                            case 61027:
                                            case 61028:
                                                ret = "Lever";
                                                multiTriggerOkay = true;
                                                break;
                                            case 74143:
                                                ret = "The Mantella";
                                                break;
                                            case 62323:
                                            // Secret teleport
                                            case 72019:
                                            case 74215:
                                            case 74225:
                                                multiTriggerOkay = true;
                                                break;
                                        }
                                    }
                                }
                                /*else if (CheckComponent<DaggerfallBillboard>(hit, out comp))
                                {
                                    var bb = ((DaggerfallBillboard)comp);
                                    var archive = bb.Summary.Archive;
                                    var index = bb.Summary.Record;

                                    if (archive == 211)
                                    {
                                        switch (index)
                                        {
                                            case 4:
                                                ret = "Chain";
                                                break;
                                        }
                                    }
                                }*/

                                if (da.TriggerFlag == DFBlock.RdbTriggerFlags.MultiTrigger && !multiTriggerOkay)
                                {
                                    ret = null;
                                }
                                else
                                {
                                    if (string.IsNullOrEmpty(ret))
                                        ret = "<Interact>";

                                    prevDistance = PlayerActivate.DefaultActivationDistance;
                                }

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
                        else if (CheckComponent<QuestResourceBehaviour>(hit, out comp))
                        {
                            var qrb = (QuestResourceBehaviour)comp;

                            if (qrb.TargetResource != null)
                            {
                                if (qrb.TargetResource is Item)
                                {
                                    if (CheckComponent<DaggerfallBillboard>(hit, out comp))
                                    {
                                        var bb = ((DaggerfallBillboard)comp);
                                        var archive = bb.Summary.Archive;
                                        var index = bb.Summary.Record;

                                        if (archive == 211)
                                        {
                                            switch (index)
                                            {
                                                case 54:
                                                    ret = "The Totem of Tiber Septim";
                                                    break;
                                            }
                                        }
                                    }

                                    if (string.IsNullOrEmpty(ret))
                                        ret = DaggerfallUnity.Instance.ItemHelper.ResolveItemLongName(((Item)qrb.TargetResource).DaggerfallUnityItem, false);

                                    prevDistance = PlayerActivate.DefaultActivationDistance;
                                }
                            }
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
                                        case 41004:
                                        case 41800:
                                        case 41801:
                                            ret = "Wardrobe";
                                            break;
                                        case 41007:
                                        case 41008:
                                        case 41033:
                                        case 41038:
                                        case 41805:
                                        case 41810:
                                        case 41802:
                                            ret = "Cabinets";
                                            break;
                                        case 41027:
                                            ret = "Shelf";
                                            break;
                                        case 41034:
                                        case 41050:
                                        case 41803:
                                        case 41806:
                                            ret = "Dresser";
                                            break;
                                        case 41032:
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
                        else
                        {
                            prevHit = null;
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
                            string tooltip;
                            if (buildingType != DFLocation.BuildingTypes.Town23)
                            {
                                tooltip = "To\r" + db.displayName;
                            }
                            else
                            {
                                tooltip = "To\r" + playerGPS.CurrentLocation.Name + " City Walls";
                            }

                            if (!buildingUnlocked)
                            {
                                tooltip += "\rLock Level: " + buildingLockValue;
                            }

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

            if (doorsTransform == prevDoorCheckTransform)
            {
                owner = prevDoorOwner;
                return prevStaticDoors;
            }

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

            prevDoorCheckTransform = doorsTransform;
            prevStaticDoors = doors;
            prevDoorOwner = owner;

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

        public class HUDTooltip : BaseScreenComponent
        {
            #region Fields

            const int defaultMarginSize = 2;

            DaggerfallFont font;
            private int currentCursorHeight = -1;
            private int currentSystemHeight;
            private int currentRenderingHeight;
            private bool currentFullScreen;

            bool drawToolTip = false;
            string[] textRows;
            float widestRow = 0;
            string lastText = string.Empty;
            bool previousSDFState;

            #endregion

            #region Properties

            /// <summary>
            /// Gets or sets font used inside tooltip.
            /// </summary>
            public DaggerfallFont Font
            {
                get { return font; }
                set { font = value; }
            }

			/// <summary>
			/// Sets delay time in seconds before tooltip is displayed.
			/// </summary>
			public float ToolTipDelay { get; set; } = 0;

            /// <summary>
            /// Gets or sets tooltip draw position relative to mouse.
            /// </summary>
            public Vector2 MouseOffset { get; set; } = new Vector2(0, 4);

			/// <summary>
			/// Gets or sets tooltip text colour.
			/// </summary>
			public Color TextColor { get; set; } = DaggerfallUI.DaggerfallUnityDefaultToolTipTextColor;

            #endregion

            #region Constructors

            public HUDTooltip()
            {
                font = DaggerfallUI.DefaultFont;
                BackgroundColor = DaggerfallUI.DaggerfallUnityDefaultToolTipBackgroundColor;
                SetMargins(Margins.All, defaultMarginSize);
            }

            #endregion

            #region Public Methods

            public override void Update()
            {
                base.Update();
                if (DaggerfallUnity.Settings.CursorHeight != currentCursorHeight ||
                    Display.main.systemHeight != currentSystemHeight ||
                    Display.main.renderingHeight != currentRenderingHeight ||
                    DaggerfallUnity.Settings.Fullscreen != currentFullScreen)
                    UpdateMouseOffset();
            }

            private void UpdateMouseOffset()
            {
                currentCursorHeight = DaggerfallUnity.Settings.CursorHeight;
                currentSystemHeight = Display.main.systemHeight;
                currentRenderingHeight = Display.main.renderingHeight;
                currentFullScreen = DaggerfallUnity.Settings.Fullscreen;
                MouseOffset = new Vector2(0, 0); //currentCursorHeight * 200f / (currentFullScreen ? currentSystemHeight : currentRenderingHeight));
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
                    widestRow + LeftMargin + RightMargin,
                    font.GlyphHeight * textRows.Length + TopMargin + BottomMargin - 1);

                // Set tooltip position
                Position = new Vector2(Screen.width / 2, currentFullScreen ? currentSystemHeight / 2 : currentRenderingHeight / 2) + MouseOffset;

                // Ensure tooltip inside screen area
                Position = new Vector2(Position.x * 0.9f / LocalScale.x, Position.y * 1f / LocalScale.y);

                // Check if mouse position is in parent's rectangle (to prevent tooltips out of panel's rectangle to be displayed)
                if (Parent != null)
                {
                    // Raise flag to draw tooltip
                    drawToolTip = true;
                }
            }

            public override void Draw()
            {
                if (!Enabled)
                    return;

                if (drawToolTip) {
                    base.Draw();

                    // Set render area for tooltip to whole screen (material might have been changed by other component, i.e. _ScissorRect might have been set to a subarea of screen (e.g. by TextLabel class))
                    Material material = font.GetMaterial();
                    Vector4 scissorRect = new Vector4(0, 1, 0, 1);
                    material.SetVector("_ScissorRect", scissorRect);

                    // Determine text position
                    Rect rect = Rectangle;
                    Vector2 textPos = new Vector2(
                        rect.x + LeftMargin * LocalScale.x,
                        rect.y + TopMargin * LocalScale.y);

                    //if (rect.xMax > Screen.width) textPos.x -= (rect.xMax - Screen.width);

                    // Draw tooltip text
                    for (int i = 0; i < textRows.Length; i++)
                    {
                        float temp = textPos.x;
                        var calc = font.CalculateTextWidth(textRows[i], LocalScale);
                        textPos.x = ((rect.x) + ((widestRow - calc) / 2) * LocalScale.x + LeftMargin * LocalScale.x); //- (rect.width * Scale.x / 3f) - (LeftMargin / 2 * Scale.x);
                        font.DrawText(textRows[i], textPos, LocalScale, TextColor);
                        textPos.y += font.GlyphHeight * LocalScale.y;
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
                    float width = font.CalculateTextWidth(textRows[i], LocalScale);
                    if (width > widestRow)
                        widestRow = width;
                }
                previousSDFState = sdfState;
            }

            #endregion
        }

        #endregion
    }
}