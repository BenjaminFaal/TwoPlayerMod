using GTA;
using GTA.Math;
using GTA.Native;
using SharpDX.DirectInput;
using System;
using NativeUI;
using System.Windows.Forms;
using System.Collections.Generic;
using Benjamin94.Input;
using Benjamin94;
using SharpDX.XInput;
using System.Linq;
using System.Drawing;

/// <summary>
/// The main Script, this will handle all logic
/// </summary>
class TwoPlayerMod : Script
{
    // for ini keys
    public static string ScriptName = "TwoPlayerMod";
    private const string ToggleMenuKey = "ToggleMenuKey";
    private const string CharacterHashKey = "CharacterHash";
    private const string ControllerKey = "Controller";
    private const string CustomCameraKey = "CustomCamera";
    private const string BlipSpriteKey = "BlipSprite";
    private const string BlipColorKey = "BlipColor";
    private const string EnabledKey = "Enabled";

    // Player 1
    private Player player;
    public static Ped player1;

    // Player 2 
    private Ped player2;
    private WeaponHash[] weapons = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
    private WeaponHash[] meleeWeapons = new WeaponHash[] { WeaponHash.PetrolCan, WeaponHash.Knife, WeaponHash.Nightstick, WeaponHash.Hammer, WeaponHash.Bat, WeaponHash.GolfClub, WeaponHash.Crowbar, WeaponHash.Bottle, WeaponHash.SwitchBlade, WeaponHash.Dagger, WeaponHash.Hatchet, WeaponHash.Unarmed, WeaponHash.KnuckleDuster, WeaponHash.Machete, WeaponHash.Flashlight };
    private WeaponHash[] throwables = new WeaponHash[] { WeaponHash.StickyBomb, WeaponHash.Snowball, WeaponHash.SmokeGrenade, WeaponHash.ProximityMine, WeaponHash.Molotov, WeaponHash.Grenade, WeaponHash.Flare, WeaponHash.BZGas, WeaponHash.Ball };
    private int weaponIndex = 0;


    private int WeaponIndex
    {
        get
        {
            if (weaponIndex < 0)
            {
                weaponIndex = weapons.Length - 1;
            }
            if (weaponIndex >= weapons.Length)
            {
                weaponIndex = 0;
            }
            return weaponIndex;
        }
        set
        {
            weaponIndex = value;
        }
    }
    private BlipSprite p2BlipSprite = BlipSprite.Standard;
    private BlipColor p2BlipColor = BlipColor.Green;

    private Ped[] targets = null;
    private int targetIndex = 0;
    private int TargetIndex
    {
        get
        {
            if (targetIndex < 0)
            {
                targetIndex = targets.Length - 1;
            }
            if (targetIndex >= targets.Length)
            {
                targetIndex = 0;
            }
            return targetIndex;
        }
        set
        {
            targetIndex = value;
        }
    }

    /// <summary>
    /// This variable will hold the last VehicleAction in order to not spam the Native calls
    /// </summary>
    private VehicleAction LastVehicleAction = VehicleAction.Brake;

    private Dictionary<Player2Action, int> lastActions = new Dictionary<Player2Action, int>();
    // PlayerPeds
    public static List<PlayerPed> playerPeds = new List<PlayerPed>();

    // Menu
    private UIMenu menu;
    private MenuPool menuPool = new MenuPool();

    // Settings
    private Keys toggleMenuKey = Keys.F11;

    // camera
    public static bool customCamera = false;
    private Camera camera;

    private int camDirection = 0; //0 = South, 1 = West, 2 = North, 3 = East
    // players 
    private readonly UserIndex[] userIndices = new UserIndex[] { UserIndex.Two, UserIndex.Three, UserIndex.Four };

    public TwoPlayerMod()
    {
        ScriptName = Name;
        player = Game.Player;
        player1 = player.Character;
        player1.Task.ClearAll();

        LoadSettings();
        SetupMenu();

        KeyDown += TwoPlayerMod_KeyDown;
        Tick += TwoPlayerMod_Tick;
    }

    /// <summary>
    /// Loads the mod settings, if there is an error it will revert the file back to default
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            toggleMenuKey = (Keys)new KeysConverter().ConvertFromString(Settings.GetValue(Name, ToggleMenuKey, Keys.F11.ToString()));
        }
        catch (Exception)
        {
            toggleMenuKey = Keys.F11;
            UI.Notify("Failed to read '" + ToggleMenuKey + "', reverting to default " + ToggleMenuKey + " F11");

            Settings.SetValue(Name, ToggleMenuKey, Keys.F11.ToString());
            Settings.Save();
        }

        try
        {
            customCamera = bool.Parse(Settings.GetValue(Name, CustomCameraKey, "False"));
        }
        catch (Exception)
        {
            customCamera = false;
            UI.Notify("Failed to read '" + CustomCameraKey + "', reverting to default " + CustomCameraKey + " False");

            Settings.SetValue(Name, CustomCameraKey, "False");
            Settings.Save();
        }
    }

    /// <summary>
    /// Determines if the mod is enabled or disabled
    /// </summary>
    /// <returns>true or false whether the mod is enabled</returns>
    private bool Enabled()
    {
        return playerPeds.Count > 0;
    }

    /// <summary>
    /// Sets up the NativeUI menu
    /// </summary>
    private void SetupMenu()
    {
        if (menuPool != null)
        {
            menuPool.ToList().ForEach(menu => { menu.Clear(); });
        }

        menu = new UIMenu("Two Player Mod", Enabled() ? "~g~Enabled" : "~r~Disabled");
        menuPool.Add(menu);

        UIMenuItem toggleItem = new UIMenuItem("Toggle mod", "Toggle Two Player mode");
        toggleItem.Activated += ToggleMod_Activated;
        menu.AddItem(toggleItem);

        UIMenu allPlayersMenu = menuPool.AddSubMenu(menu, "Players");
        menu.MenuItems.FirstOrDefault(item => { return item.Text.Equals("Players"); }).Description = "Configure players";

        foreach (UserIndex player in userIndices)
        {
            bool check = bool.Parse(PlayerSettings.GetValue(player, EnabledKey, false.ToString()));

            UIMenu playerMenu = menuPool.AddSubMenu(allPlayersMenu, "Player " + player);
            UIMenuItem playerItem = allPlayersMenu.MenuItems.FirstOrDefault(item => { return item.Text.Equals("Player " + player); });

            string controllerGuid = PlayerSettings.GetValue(player, ControllerKey, "");

            playerItem.Description = "Configure player " + player;

            if (!string.IsNullOrEmpty(controllerGuid))
            {
                playerItem.SetRightBadge(UIMenuItem.BadgeStyle.Star);
            }

            UIMenuCheckboxItem togglePlayerItem = new UIMenuCheckboxItem("Toggle player " + player, check, "Enables/disables this player");

            togglePlayerItem.CheckboxEvent += (s, enabled) =>
            {
                PlayerSettings.SetValue(player, EnabledKey, enabled.ToString());

                RefreshSubItems(togglePlayerItem, playerMenu, enabled);
            };

            playerMenu.AddItem(togglePlayerItem);

            playerMenu.AddItem(ConstructSettingsListItem(player, "Character", "Select a character for player " + player, CharacterHashKey, PedHash.Trevor));
            playerMenu.AddItem(ConstructSettingsListItem(player, "Blip sprite", "Select a blip sprite for player " + player, BlipSpriteKey, BlipSprite.Standard));
            playerMenu.AddItem(ConstructSettingsListItem(player, "Blip color", "Select a blip color for player " + player, BlipColorKey, BlipColor.Green));

            UIMenu controllerMenu = menuPool.AddSubMenu(playerMenu, "Assign controller");
            playerMenu.MenuItems.FirstOrDefault(item => { return item.Text.Equals("Assign controller"); }).Description = "Assign controller to player " + player;

            foreach (InputManager manager in InputManager.GetAvailableInputManagers())
            {
                UIMenuItem controllerItem = new UIMenuItem(manager.DeviceName, "Assign this controller to player " + player);

                string guid = manager.DeviceGuid;

                if (PlayerSettings.GetValue(player, ControllerKey, "").Equals(guid))
                {
                    controllerItem.SetRightBadge(UIMenuItem.BadgeStyle.Star);
                }

                if (manager is DirectInputManager)
                {
                    DirectInputManager directManager = (DirectInputManager)manager;
                    bool configured = DirectInputManager.IsConfigured(directManager.device, GetIniFile());
                    controllerItem.Enabled = configured;

                    if (!configured)
                    {
                        controllerItem.Description = "Please configure this controller first from the main menu";
                    }
                }

                controllerItem.Activated += (s, i) =>
                {
                    if (i.Enabled)
                    {
                        PlayerSettings.SetValue(player, ControllerKey, guid);

                        controllerMenu.MenuItems.ForEach(item =>
                        {
                            if (item == controllerItem)
                            {
                                item.SetRightBadge(UIMenuItem.BadgeStyle.Star);
                            }
                            else
                            {
                                item.SetRightBadge(UIMenuItem.BadgeStyle.None);
                            }
                        });
                    }
                };

                controllerMenu.AddItem(controllerItem);
            }

            RefreshSubItems(togglePlayerItem, playerMenu, check);
        }

        UIMenuCheckboxItem cameraItem = new UIMenuCheckboxItem("GTA:SA style camera", customCamera, "Enables/disables the GTA:SA style camera");
        cameraItem.CheckboxEvent += (s, i) =>
        {
            customCamera = !customCamera;
            Settings.SetValue(Name, CustomCameraKey, customCamera.ToString());
            Settings.Save();
        };
        menu.AddItem(cameraItem);

        UIMenu controllersMenu = menuPool.AddSubMenu(menu, "Configure controllers");
        menu.MenuItems.FirstOrDefault(item => { return item.Text.Equals("Configure controllers"); }).Description = "Configure controllers before assigning them to players";
        foreach (Joystick stick in DirectInputManager.GetDevices())
        {
            UIMenuItem stickItem = new UIMenuItem(stick.Information.ProductName, "Configure " + stick.Information.ProductName);

            controllersMenu.AddItem(stickItem);
            stickItem.Activated += (s, i) =>
            {
                ControllerWizard wizard = new ControllerWizard(stick);
                bool success = wizard.StartConfiguration(GetIniFile());
                if (success)
                {
                    UI.Notify("Controller successfully configured, you can now assign this controller");
                }
                else
                {
                    UI.Notify("Controller configuration canceled, please configure your controller before playing");
                }
                SetupMenu();
            };
        }

        menu.RefreshIndex();
    }

    /// <summary>
    /// Refreshes all items in the UIMenu except the parentItem
    /// </summary>
    /// <param name="parentItem">The parent UIMenuItem to ignore</param>
    /// <param name="menu">UIMenu</param>
    /// <param name="enabled">Whether to enable or disable the sub items</param>
    private void RefreshSubItems(UIMenuItem parentItem, UIMenu menu, bool enabled)
    {
        foreach (UIMenuItem item in menu.MenuItems)
        {
            if (!item.Equals(parentItem))
            {
                item.Enabled = enabled;
            }
        }
    }

    /// <summary>
    /// Constructs a new UIMenuListItem with automatic handling of selected value
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum</typeparam>
    /// <param name="player">The </param>
    /// <param name="text">The menu item text</param>
    /// <param name="description">The menu item description</param>
    /// <param name="settingsKey">The settings key</param>
    /// <param name="defaultValue">The initial selected value</param>
    /// <returns>A fully working UIMenuListItem with automatic saving</returns>
    private UIMenuListItem ConstructSettingsListItem<TEnum>(UserIndex player, string text, string description, string settingsKey, TEnum defaultValue)
    {
        List<dynamic> items = GetDynamicEnumList<TEnum>();

        TEnum value = PlayerSettings.GetEnumValue<TEnum>(player, settingsKey, defaultValue.ToString());

        UIMenuListItem menuItem = new UIMenuListItem(text, items, items.IndexOf(value.ToString()), description);
        menuItem.OnListChanged += (s, i) =>
        {
            if (s.Enabled)
            {
                TEnum selectedItem = Enum.Parse(typeof(TEnum), s.IndexToItem(s.Index));
                PlayerSettings.SetValue(player, settingsKey, selectedItem.ToString());
            }
        };
        return menuItem;
    }

    /// <summary>
    /// This method will handle entering vehicles
    /// </summary>
    /// <param name="ped">Target Ped</param>
    public static void HandleEnterVehicle(Ped ped)
    {
        Vehicle v = World.GetClosestVehicle(ped.Position, 15);
        if (v == null)
        {
            return;
        }
        Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);

        if (driver != null && driver.IsPlayerPed())
        {
            ped.Task.EnterVehicle(v, VehicleSeat.Any);
        }
        else
        {
            if (driver != null)
            {
                driver.Delete();
            }
            ped.Task.EnterVehicle(v, VehicleSeat.Any);
        }
    }

    /// <summary>
    /// Helper method to get all values of an Enum for displaying in a menu
    /// </summary>
    /// <param name="type">Type of the Enum</param>
    /// <returns>Sorted list of all possible given Enum values</returns>
    private List<dynamic> GetDynamicEnumList<TEnum>()
    {
        List<dynamic> values = new List<dynamic>();

        foreach (Enum pedHash in Enum.GetValues(typeof(TEnum)))
        {
            values.Add(pedHash.ToString());
        }

        values.Sort();
        return values;
    }

    /// <summary>
    /// Helper method to get the INI file
    /// </summary>
    /// <returns>"scripts//" + Name + ".ini"</returns>
    private string GetIniFile()
    {
        return "scripts//" + Name + ".ini";
    }

    /// <summary>
    /// Gets called by NativeUI when the user selects "Toggle mod" in the menu
    /// </summary>
    private void ToggleMod_Activated(UIMenu sender, UIMenuItem selectedItem)
    {
        if (!Enabled())
        {
            UI.ShowSubtitle("Like this mod? Please consider donating!", 10000);
            SetupPlayerPeds();

            if (playerPeds.Count == 0)
            {
                UI.Notify("Please assign a controller to at least one player");
                UI.Notify("Also make sure you have configured at least one controller");
                return;
            }

            SetupCamera();
        }
        else
        {
            Clean();
        }
        menu.Subtitle.Caption = Enabled() ? "~g~Enabled" : "~r~Disabled";
    }

    /// <summary>
    /// Sets up all correctly configured PlayerPed
    /// </summary>
    private void SetupPlayerPeds()
    {
        foreach (UserIndex player in userIndices)
        {
            if (bool.Parse(PlayerSettings.GetValue(player, EnabledKey, false.ToString())))
            {
                string guid = PlayerSettings.GetValue(player, ControllerKey, "");

                foreach (InputManager input in InputManager.GetAvailableInputManagers())
                {
                    if (input.DeviceGuid.Equals(guid))
                    {
                        InputManager manager = input;
                        if (input is DirectInputManager)
                        {
                            manager = DirectInputManager.LoadConfig(((DirectInputManager)input).device, GetIniFile());
                        }

                        PedHash characterHash = PlayerSettings.GetEnumValue<PedHash>(player, CharacterHashKey, PedHash.Trevor.ToString());
                        BlipSprite blipSprite = PlayerSettings.GetEnumValue<BlipSprite>(player, BlipSpriteKey, BlipSprite.Standard.ToString());
                        BlipColor blipColor = PlayerSettings.GetEnumValue<BlipColor>(player, BlipColorKey, BlipColor.Green.ToString());

                        PlayerPed playerPed = new PlayerPed(player, characterHash, blipSprite, blipColor, player1, manager);
                        playerPeds.Add(playerPed);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// This method will setup the camera system
    /// </summary>
    private void SetupCamera()
    {
        Function.Call(Hash.LOCK_MINIMAP_ANGLE, 0);
        camera = World.CreateCamera(player1.GetOffsetInWorldCoords(new Vector3(0, 10, 10)), Vector3.Zero, GameplayCamera.FieldOfView);
    }

    /// <summary>
    /// This method is overridden so we can cleanup the script
    /// </summary>
    protected override void Dispose(bool A_0)
    {
        Clean();
        base.Dispose(A_0);
    }

    /// <summary>
    /// Toggles the menu
    /// </summary>
    private void TwoPlayerMod_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == toggleMenuKey)
        {
            menu.Visible = !menu.Visible;
        }
    }


    /// <summary>
    /// Cleans up the script, deletes Player2 and cleans the InputManager system
    /// </summary>
    private void Clean()
    {
        Function.Call(Hash.UNLOCK_MINIMAP_ANGLE);

        CleanCamera();
        CleanUpPlayerPeds();
    }

    /// <summary>
    /// Cleans up all PlayerPeds
    /// </summary>
    private void CleanUpPlayerPeds()
    {
        playerPeds.ForEach((playerPed => { playerPed.Clean(); }));
        playerPeds.Clear();
        if (player1 != null)
        {
            player1.Task.ClearAllImmediately();
        }
    }

    /// <summary>
    /// This method will return the center of given vectors
    /// </summary>
    /// <param name="vectors">Vector3 A</param>
    /// <returns>The center Vector3 of the two given Vector3s</returns>
    public Vector3 CenterOfVectors(params Vector3[] vectors)
    {
        Vector3 center = Vector3.Zero;
        foreach (Vector3 vector in vectors)
        {
            center += vector;
        }

        return center / vectors.Length;
    }

    private bool resetWalking = false;

    private void TwoPlayerMod_Tick(object sender, EventArgs e)
    {
        menuPool.ProcessMenus();

        if (Enabled())
        {
            if (!Player1Available())
            {
                CleanCamera();

                while (!Player1Available() || Function.Call<bool>(Hash.IS_SCREEN_FADING_IN))
                {
                    Wait(1000);
                }

                playerPeds.ForEach(playerPed =>
                {
                    playerPed.Respawn();
                    Wait(500);
                });

                SetupCamera();
            }

            playerPeds.ForEach((playerPed => { playerPed.Tick(); }));

            UpdateCamera();

            if (customCamera && player1.IsOnFoot)
            {
                if (Game.IsControlPressed(0, GTA.Control.Jump))
                {
                    player1.Task.Climb();
                }

                Vector2 offset = Vector2.Zero;

                if (Game.IsControlPressed(0, GTA.Control.MoveUpOnly))
                {
                    offset.Y++;
                }

                if (Game.IsControlPressed(0, GTA.Control.MoveLeftOnly))
                {
                    offset.X--;
                }

                if (Game.IsControlPressed(0, GTA.Control.MoveRightOnly))
                {
                    offset.X++;
                }

                if (Game.IsControlPressed(0, GTA.Control.MoveDownOnly))
                {
                    offset.Y--;
                }


                offset = alterInput(offset);

                if (offset != Vector2.Zero)
                {
                    if (Game.IsControlPressed(0, GTA.Control.Sprint))
                    {
                        // needed for running
                        offset *= 10;
                    }
                    Vector3 dest = Vector3.Zero;
                    dest = player1.Position - new Vector3(offset.X, offset.Y, 0);
                    player1.Task.RunTo(dest, true, -1);
                    resetWalking = true;
                }
                else if (resetWalking)
                {
                    player1.Task.ClearAll();
                    resetWalking = false;
                }
            }

            if (Game.IsControlJustReleased(0, GTA.Control.NextCamera))
            {
                customCamera = !customCamera;
            }

            // for letting player get in a vehicle
            if (player1.IsOnFoot && Game.IsControlJustReleased(0, GTA.Control.VehicleExit))
            {
                HandleEnterVehicle(player1);
            }
            //Switches camera direction variable used later to change camera direction
            if (input.isPressed(DeviceButton.DPadDown))//South
            {
                camDirection = 0;
            }
            if (input.isPressed(DeviceButton.DPadLeft))//West
            {
                camDirection = 1;
            }
            if (input.isPressed(DeviceButton.DPadUp))//North
            {
                camDirection = 2;
            }
            if (input.isPressed(DeviceButton.DPadRight))//East
            {
                camDirection = 3;
            }
        }
    }

    /// <summary>
    /// Cleans the Camera system
    /// </summary>
    private void CleanCamera()
    {
        World.DestroyAllCameras();
        World.RenderingCamera = null;
    }

    /// <summary>
    /// Helper method to check if Player 1 is still normally available
    /// </summary>
    /// <returns>True when Player 1 is still normally available to play</returns>
    private bool Player1Available()
    {
        return !player1.IsDead && !Function.Call<bool>(Hash.IS_PLAYER_BEING_ARRESTED, player, true) && !Function.Call<bool>(Hash.IS_PLAYER_BEING_ARRESTED, player, false) && !Function.Call<bool>(Hash.IS_SCREEN_FADING_OUT);
    }

    /// <summary>
    /// Helper method to determine if player 2 is allowed to do the given Player2Action
    /// </summary>
    /// <param name="action">Player2Action to check</param>
    /// <param name="time">minimal time that has to be past before true</param>
    /// <returns>true if time since last Player2Action is more than given time, false otherwise</returns>
    private bool CanDoAction(Player2Action action, int time)
    {
        return Game.GameTime - lastActions[action] >= time;
    }

    /// <summary>
    /// Updates the time of the given Player2Action
    /// </summary>
    /// <param name="action">Target Player2Action</param>
    private void UpdateLastAction(Player2Action action)
    {
        lastActions[action] = Game.GameTime;
    }

    /// <summary>
    /// Updates the selection of weapon for Player 2
    /// </summary>
    /// <returns>true if weaponIndex changed, false otherwise</returns>
    private bool UpdateWeaponIndex()
    {
        Direction dir = input.GetDirection(player2.IsInVehicle() ? DeviceButton.LeftStick : DeviceButton.RightStick);

        if (input.IsDirectionLeft(dir))
        {
            WeaponIndex--;
            return true;
        }
        if (input.IsDirectionRight(dir))
        {
            WeaponIndex++;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// This will update the camera 
    /// </summary>
    private void UpdateCamera()
    {
        // if all in same vehicle
        if (playerPeds.TrueForAll(p => { return p.Ped.CurrentVehicle != null && p.Ped.CurrentVehicle == player1.CurrentVehicle; }))
        {
            World.RenderingCamera = null;
        }
        else if (customCamera)
        {
            PlayerPed furthestPlayer = playerPeds.OrderByDescending(playerPed => { return player1.Position.DistanceTo(playerPed.Ped.Position); }).FirstOrDefault();

            Vector3 center = CenterOfVectors(player1.Position, furthestPlayer.Ped.Position);

            World.RenderingCamera = camera;
            camera.PointAt(center);

            float dist = furthestPlayer.Ped.Position.DistanceTo(player1.Position);

            //Changes position of camera to switch direction
            switch(camDirection)
            {
                case 0:                             //0 = South
                    center.Y += 5f + (dist / 1.6f);
                    Function.Call(Hash.LOCK_MINIMAP_ANGLE, 0);
                    break;
                case 1:                             //1 = West?
                    center.X += 5f + (dist / 1.6f);
                    Function.Call(Hash.LOCK_MINIMAP_ANGLE, 90);
                    break;
                case 2:                             //2 = North
                    center.Y -= 5f + (dist / 1.6f);
                    Function.Call(Hash.LOCK_MINIMAP_ANGLE, 180);
                    break;
                case 3:                             //3 = East?
                    center.X -= 5f + (dist / 1.6f);
                    Function.Call(Hash.LOCK_MINIMAP_ANGLE, 270);
                    break;
                default:                            //Just in case
                    center.Y += 5f + (dist / 1.6f);
                    break;
            }

            center.Z += 2f + (dist / 1.4f);

            camera.Position = center;
        }
        else
        {
            World.RenderingCamera = null;
        }
    }

    /// <summary>
    /// Determines the needed action corresponding to current controller input, e.g. VehicleAction.RevEngine
    /// </summary>
    /// <returns>A VehicleAction enum</returns>
    private VehicleAction GetVehicleAction(Vehicle v)
    {
        Direction dir = input.GetDirection(DeviceButton.LeftStick);
        if (input.isPressed(DeviceButton.A))
        {
            if (input.IsDirectionLeft(dir))
            {
                return VehicleAction.HandBrakeLeft;
            }
            else if (input.IsDirectionRight(dir))
            {
                return VehicleAction.HandBrakeRight;
            }
            else
            {
                return VehicleAction.HandBrakeStraight;
            }
        }

        if (input.isPressed(DeviceButton.RightTrigger))
        {
            if (input.IsDirectionLeft(dir))
            {
                return VehicleAction.GoForwardLeft;
            }
            else if (input.IsDirectionRight(dir))
            {
                return VehicleAction.GoForwardRight;
            }
            else
            {
                return VehicleAction.GoForwardStraightFast;
            }
        }

        if (input.isPressed(DeviceButton.LeftTrigger))
        {
            if (input.IsDirectionLeft(dir))
            {
                return VehicleAction.ReverseLeft;
            }
            else if (input.IsDirectionRight(dir))
            {
                return VehicleAction.ReverseRight;
            }
            else
            {
                return VehicleAction.ReverseStraight;
            }
        }

        if (input.IsDirectionLeft(dir))
        {
            return VehicleAction.SwerveLeft;
        }
        else if (input.IsDirectionRight(dir))
        {
            return VehicleAction.SwerveRight;
        }

        return VehicleAction.RevEngine;
    }


    /// <summary>
    /// Calls the TASK_VEHICLE_TEMP_ACTION native in order to control a Vehicle
    /// </summary>
    /// <param name="ped">The driver</param>
    /// <param name="vehicle">The target Vehicle</param>
    /// <param name="action">Any of enum VehicleAction</param>
    private void PerformVehicleAction(Ped ped, Vehicle vehicle, VehicleAction action)
    {
        Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, ped, vehicle, (int)action, -1);
    }

    /// <summary>
    /// Updates all kind of on foot actions like walking entering exiting vehicles
    /// </summary>
    private void UpdateFoot()
    {
        UpdateCombat(player2, player1, () => { return input.isPressed(DeviceButton.LeftTrigger); }, () => { return input.isPressed(DeviceButton.RightTrigger); });

        Vector2 leftThumb = input.GetState().LeftThumbStick;

        if (leftThumb != Vector2.Zero)
        {
            if (input.isPressed(DeviceButton.A))
            {
                // needed for running
                leftThumb *= 10;
            }
            Vector3 dest = Vector3.Zero;
            if (customCamera)
            {
                dest = player2.Position - alterInput(new Vector3(leftThumb.X, leftThumb.Y, 0));
            }
            else
            {
                dest = player2.GetOffsetInWorldCoords(new Vector3(leftThumb.X, leftThumb.Y, 0));
            }

            player2.Task.RunTo(dest, true, -1);
        }
        else
        {
            player2.Task.GoTo(player2.Position, true, 0);
        }

        if (input.isPressed(DeviceButton.X) && CanDoAction(Player2Action.Jump, 850))
        {
            player2.Task.Climb();
            UpdateLastAction(Player2Action.Jump);
        }

        if (input.isPressed(DeviceButton.LeftShoulder))
        {
            if (CanDoAction(Player2Action.SelectWeapon, 500))
            {
                if (UpdateWeaponIndex())
                {
                    SelectWeapon(player2, weapons[WeaponIndex]);
                }
            }
            NotifyWeapon();
        }
    }

    /// <summary>
    /// Helper method to show the current weapon and show the user what to do to change the weapon
    /// </summary>
    private void NotifyWeapon()
    {
        UI.ShowSubtitle("Player 2 weapon: ~g~" + weapons[WeaponIndex] + "~w~. Use ~g~" + (player2.IsInVehicle() ? DeviceButton.LeftStick : DeviceButton.RightStick) + "~w~ to select weapons.");
    }

    /// <summary>
    /// This will fire at the targeted ped and will handle changing targets
    /// </summary>
    /// <param name="player">The player for which to update the combat mechanism</param>
    /// <param name="exclude">The player which should be ignored</param>
    /// <param name="firstButton">The first (aiming) button which needs to pressed before handling this update</param>
    /// <param name="secondButton">The second (firing) button which needs to pressed before the actual shooting</param>
    private void UpdateCombat(Ped player, Ped exclude, Func<bool> firstButton, Func<bool> secondButton)
    {
        if (input.isPressed(DeviceButton.DPadLeft))
        {
            foreach (WeaponHash projectile in throwables)
            {
                Function.Call(Hash.EXPLODE_PROJECTILES, player, new InputArgument(projectile), true);
            }
        }
        if (firstButton.Invoke())
        {
            if (!secondButton.Invoke())
            {
                targets = GetTargets(player, exclude);
            }
            if (CanDoAction(Player2Action.SelectTarget, 500))
            {
                Direction dir = input.GetDirection(DeviceButton.RightStick);
                if (input.IsDirectionLeft(dir))
                {
                    TargetIndex--;
                    UpdateLastAction(Player2Action.SelectTarget);
                }
                if (input.IsDirectionRight(dir))
                {
                    TargetIndex++;
                    UpdateLastAction(Player2Action.SelectTarget);
                }
            }

            Ped target = targets.ElementAtOrDefault(TargetIndex);

            if (target == null)
            {
                return;
            }

            if (!target.IsAlive)
            {
                targets = GetTargets(player, exclude);
            }

            if (target != null)
            {
                World.DrawMarker(MarkerType.UpsideDownCone, target.GetBoneCoord(Bone.SKEL_Head) + new Vector3(0, 0, 1), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(1, 1, 1), Color.OrangeRed);

                if (secondButton.Invoke())
                {
                    if (IsThrowable(weapons[WeaponIndex]))
                    {
                        if (CanDoAction(Player2Action.ThrowTrowable, 1500))
                        {
                            SelectWeapon(player, weapons[WeaponIndex]);
                            Function.Call(Hash.TASK_THROW_PROJECTILE, player, target.Position.X, target.Position.Y, target.Position.Z);
                            UpdateLastAction(Player2Action.ThrowTrowable);
                        }
                    }
                    else if (CanDoAction(Player2Action.Shoot, 750))
                    {
                        if (player.IsInVehicle())
                        {
                            Function.Call(Hash.TASK_DRIVE_BY, player, target, 0, 0, 0, 0, 50.0f, 100, 1, (uint)FiringPattern.FullAuto);
                        }
                        else if (IsMelee(weapons[WeaponIndex]))
                        {
                            SelectWeapon(player, weapons[weaponIndex]);
                            player.Task.ShootAt(target, 750, FiringPattern.FullAuto);
                            UI.ShowSubtitle("Melee weapons are not supported yet.");
                        }
                        else
                        {
                            player.Task.ShootAt(target, 750, FiringPattern.FullAuto);
                        }

                        UpdateLastAction(Player2Action.Shoot);
                    }
                }
                else
                {
                    player.Task.AimAt(target, 100);
                }
            }
        }
        else
        {
            TargetIndex = 0;
        }
    }

    /// <summary>
    /// Gets targets for the given player in combat situations
    /// </summary>
    /// <param name="player">The player which should not be included in the targets array</param>
    /// <param name="exclude">The player which is the other player</param>
    /// <returns>An array of Peds</returns>
    private Ped[] GetTargets(Ped player, Ped exclude)
    {
        return World.GetNearbyPeds(player, 50).Where(ped => IsValidTarget(ped, exclude)).OrderBy(ped => ped.Position.DistanceTo(player2.Position)).ToArray();
    }

    /// <summary>
    /// Helper method to check if a Ped is a valid target for player 2
    /// </summary>
    /// <param name="target">The target Ped to check</param>
    private bool IsValidTarget(Ped target, Ped exclude)
    {
        return target != null && target != exclude && target.IsAlive && target.IsOnScreen;
    }

    /// <summary>
    /// Method to select another weapon for a Ped
    /// </summary>
    /// <param name="p">target Ped</param>
    /// <param name="weaponHash">target WeaponHash</param>
    private void SelectWeapon(Ped p, WeaponHash weaponHash)
    {
        WeaponIndex = Array.IndexOf(weapons, weaponHash);

        Function.Call(Hash.SET_CURRENT_PED_WEAPON, p, new InputArgument(weaponHash), true);
        UpdateLastAction(Player2Action.SelectWeapon);
    }


    /// <summary>
    /// Switches player input depending on camera orientation (Vector2)
    /// </summary>
    /// <param name="offset">Offset prior to remapping</param>
    private Vector2 alterInput(Vector2 offset)
    {
        float tempX;
        switch (camDirection)
        {
            case 0:                             //No need to change offset
                break;
            case 1:                             //Switch X and Y, make X negative
                tempX = offset.X;
                offset.X = offset.Y;
                offset.Y = -tempX;
                break;
            case 2:                             //Make X and Y negative
                offset.X = -offset.X;
                offset.Y = -offset.Y;
                break;
            case 3:                             //Switch X and Y, make Y negative
                tempX = offset.X;
                offset.X = -offset.Y;
                offset.Y = tempX;
                break;
            default:                            //Just in case
                break;
        }
        return offset;
    }

    /// <summary>
    /// Switches player input depending on camera orientation (Vector3)
    /// </summary>
    /// <param name="offset">Offset prior to remapping</param>
    private Vector3 alterInput(Vector3 offset)
    {
        float tempX;
        switch (camDirection)
        {
            case 0:                             //No need to change offset
                break;
            case 1:                             //Switch X and Y, make X negative
                tempX = offset.X;
                offset.X = offset.Y;
                offset.Y = -tempX;
                break;
            case 2:                             //Make X and Y negative
                offset.X = -offset.X;
                offset.Y = -offset.Y;
                break;
            case 3:                             //Switch X and Y, make Y negative
                tempX = offset.X;
                offset.X = -offset.Y;
                offset.Y = tempX;
                break;
            default:                            //Just in case
                break;
        }
        return offset;
    }

}