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
public class TwoPlayerMod : Script
{
    // for ini keys
    public const string ScriptName = "TwoPlayerMod";
    private const string ToggleMenuKey = "ToggleMenuKey";
    private const string CharacterHashKey = "CharacterHash";
    public const string ControllerKey = "Controller";
    private const string CustomCameraKey = "CustomCamera";
    private const string PlayerTwoBlipSpriteKey = "PlayerTwoBlipSprite";
    private const string PlayerTwoBlipColorKey = "PlayerTwoBlipColor";

    // Player 1
    private Player player;
    private Ped player1;

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

    // Menu
    private UIMenu menu;
    private MenuPool menuPool;

    // Controllers menu
    private UIMenu controllersMenu;
    private UIMenuItem controllersMenuItem;
    private bool controllerConnected = false;
    private const string controllersMenuDescConfig = "Configure controllers, yellow star means the controller is configured successfully and is the default controller", controllersMenuDescNoControllers = "No controllers detected. Press \"Refresh controllers\" if there really is a controller connected";

    // Settings
    private Keys toggleMenuKey = Keys.F11;
    private PedHash characterHash = PedHash.Trevor;

    // Controls
    private InputManager input;

    // camera
    private bool customCamera = false;
    private Camera camera;

    public TwoPlayerMod()
    {
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
        ScriptSettings settings = ScriptSettings.Load(GetIniFile());
        try
        {
            toggleMenuKey = (Keys)new KeysConverter().ConvertFromString(settings.GetValue(Name, ToggleMenuKey, Keys.F11.ToString()));
        }
        catch (Exception)
        {
            toggleMenuKey = Keys.F11;
            UI.Notify("Failed to read '" + ToggleMenuKey + "', reverting to default " + ToggleMenuKey + " F11");

            settings.SetValue(Name, ToggleMenuKey, Keys.F11.ToString());
            settings.Save();
        }
        try
        {
            characterHash = (PedHash)Enum.Parse(typeof(PedHash), settings.GetValue(Name, CharacterHashKey, "Trevor"));
        }
        catch (Exception)
        {
            characterHash = PedHash.Trevor;
            UI.Notify("Failed to read '" + CharacterHashKey + "', reverting to default " + CharacterHashKey + " Trevor");

            settings.SetValue(Name, CharacterHashKey, PedHash.Trevor.ToString());
            settings.Save();
        }

        try
        {
            customCamera = bool.Parse(settings.GetValue(Name, CustomCameraKey, "False"));
        }
        catch (Exception)
        {
            customCamera = false;
            UI.Notify("Failed to read '" + CustomCameraKey + "', reverting to default " + CustomCameraKey + " False");

            settings.SetValue(Name, CustomCameraKey, "False");
            settings.Save();
        }

        try
        {
            p2BlipSprite = settings.GetValue(Name, PlayerTwoBlipSpriteKey, p2BlipSprite);
        }
        catch (Exception)
        {
            p2BlipSprite = BlipSprite.Standard;
            UI.Notify("Failed to read '" + PlayerTwoBlipSpriteKey + "', reverting to default " + PlayerTwoBlipSpriteKey + " Standard");

            settings.SetValue(Name, PlayerTwoBlipSpriteKey, p2BlipSprite);
            settings.Save();
        }

        try
        {
            p2BlipColor = settings.GetValue(Name, PlayerTwoBlipColorKey, p2BlipColor);
        }
        catch (Exception)
        {
            p2BlipColor = BlipColor.Green;
            UI.Notify("Failed to read '" + PlayerTwoBlipColorKey + "', reverting to default " + PlayerTwoBlipColorKey + " Green");

            settings.SetValue(Name, PlayerTwoBlipColorKey, p2BlipColor);
            settings.Save();
        }
    }

    /// <summary>
    /// Determines if the mod is enabled or disabled
    /// </summary>
    /// <returns>true or false whether the mod is enabled</returns>
    private bool Enabled()
    {
        return player2 != null;
    }

    /// <summary>
    /// Gets all of the connected controllers and adds them to the controller menu
    /// </summary>
    private void GetControllers()
    {
        controllerConnected = DirectInputManager.GetDevices().Count > 0 || XInputManager.GetDevices().Count > 0;

        foreach (Joystick stick in DirectInputManager.GetDevices())
        {
            UIMenuItem stickItem = new UIMenuItem(stick.Information.ProductName, "Configure " + stick.Information.ProductName);

            bool def = DirectInputManager.IsDefault(stick, GetIniFile(), Name);

            if (def)
            {
                stickItem.SetLeftBadge(UIMenuItem.BadgeStyle.Star);
            }

            controllersMenu.AddItem(stickItem);
            stickItem.Activated += (s, i) =>
            {
                ControllerWizard wizard = new ControllerWizard(stick);
                bool success = wizard.StartConfiguration(GetIniFile());
                if (success)
                {
                    UI.Notify("Controller successfully configured, you can now start playing");

                    // if enabled the mod already then reload the InputManager so it is up to date with the new configuration
                    if (Enabled())
                    {
                        input = DirectInputManager.LoadConfig(stick, GetIniFile());
                    }
                }
                else
                {
                    UI.Notify("Controller configuration canceled, please configure your controller before playing");
                }
                controllersMenu.GoBack();
            };
        }
    }

    /// <summary>
    /// Sets up the NativeUI menu
    /// </summary>
    private void SetupMenu()
    {
        menuPool = new MenuPool();
        menu = new UIMenu("Two Player Mod", Enabled() ? "~g~Enabled" : "~r~Disabled");
        menuPool.Add(menu);

        UIMenuItem toggleItem = new UIMenuItem("Toggle mod", "Toggle Two Player mode");
        toggleItem.Activated += ToggleMod_Activated;
        menu.AddItem(toggleItem);

        List<dynamic> hashes = new List<dynamic>();

        foreach (PedHash pedHash in Enum.GetValues(typeof(PedHash)))
        {
            hashes.Add(pedHash.ToString());
        }

        hashes.Sort();

        ScriptSettings settings = ScriptSettings.Load(GetIniFile());

        UIMenuListItem characterItem = new UIMenuListItem("Character", hashes, hashes.IndexOf(characterHash.ToString()), "Select a character for player 2");

        characterItem.OnListChanged += (s, i) =>
        {
            PedHash selectedHash = Enum.Parse(typeof(PedHash), s.IndexToItem(s.Index));
            characterHash = selectedHash;
            settings.SetValue(Name, CharacterHashKey, selectedHash.ToString());
            settings.Save();
        };

        menu.AddItem(characterItem);

        List<dynamic> sprites = new List<dynamic>();

        foreach (BlipSprite sprite in Enum.GetValues(typeof(BlipSprite)))
        {
            sprites.Add(sprite.ToString());
        }

        sprites.Sort();

        UIMenuListItem spriteItem = new UIMenuListItem("Character blip sprite", sprites, sprites.IndexOf(p2BlipSprite.ToString()), "Select a blip sprite for player 2");

        spriteItem.OnListChanged += (s, i) =>
        {
            p2BlipSprite = Enum.Parse(typeof(BlipSprite), s.IndexToItem(s.Index));
            settings.SetValue(Name, PlayerTwoBlipSpriteKey, p2BlipSprite);
            settings.Save();

            if (Enabled())
            {
                player2.CurrentBlip.Remove();
                AddBlipToP2();
            }
        };

        menu.AddItem(spriteItem);

        List<dynamic> colors = new List<dynamic>();

        foreach (BlipColor sprite in Enum.GetValues(typeof(BlipColor)))
        {
            colors.Add(sprite.ToString());
        }

        colors.Sort();

        UIMenuListItem colorItem = new UIMenuListItem("Character blip color", colors, colors.IndexOf(p2BlipColor.ToString()), "Select a blip color for player 2");

        colorItem.OnListChanged += (s, i) =>
        {
            p2BlipColor = Enum.Parse(typeof(BlipColor), s.IndexToItem(s.Index));
            settings.SetValue(Name, PlayerTwoBlipColorKey, p2BlipColor);
            settings.Save();

            if (Enabled())
            {
                player2.CurrentBlip.Remove(); //sometimes the color changes to the selected color, other times it will change to white. 
                AddBlipToP2(); //if you keep changing the colors (in one direction) and get back to the original item it might change, but if you go back or forward an item and then go back to the original it will not.
            }
        };

        menu.AddItem(colorItem);

        UIMenuCheckboxItem cameraItem = new UIMenuCheckboxItem("Toggle GTA:SA camera", customCamera, "This enables/disables the GTA:SA style camera");
        cameraItem.CheckboxEvent += (s, i) =>
        {
            customCamera = !customCamera;
            settings.SetValue(Name, CustomCameraKey, customCamera.ToString());
            settings.Save();
        };
        menu.AddItem(cameraItem);

        controllersMenu = menuPool.AddSubMenu(menu, "Controllers");
        controllersMenuItem = menu.MenuItems.Where(item => item.Text == "Controllers").FirstOrDefault();
        controllersMenuItem.Activated += (s, i) =>
        {
            controllersMenu.MenuItems.Clear();
            GetControllers();
        };
        GetControllers();

        UIMenuItem refreshControllersItem = new UIMenuItem("Refresh controllers", "Get recently connected controllers");
        refreshControllersItem.Activated += (s, i) =>
        {
            controllersMenu.MenuItems.Clear();
            GetControllers();
        };
        menu.AddItem(refreshControllersItem);

        menu.RefreshIndex();
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
    /// Gets called by NativeUI when the user selects "Toggle Mod" in the menu
    /// </summary>
    private void ToggleMod_Activated(UIMenu sender, UIMenuItem selectedItem)
    {
        if (!Enabled())
        {
            try
            {
                SetupController();
            }
            catch (Exception e)
            {
                UI.Notify("Error occured while loading controller: " + e.Message);
                return;
            }
            SetupPlayer2();
        }
        else
        {
            Clean();
        }
        menu.Subtitle.Caption = Enabled() ? "~g~Enabled" : "~r~Disabled";
    }

    /// <summary>
    /// Adds the blip to Player 2
    /// </summary>
    private void AddBlipToP2()
    {
        player2.AddBlip().Sprite = p2BlipSprite;
        player2.CurrentBlip.Color = p2BlipColor;
    }

    /// <summary>
    /// Sets up Player 2
    /// </summary>
    private void SetupPlayer2()
    {
        player = Game.Player;
        player1 = player.Character;
        player1.IsInvincible = true;
        player1.Task.ClearAll();

        player2 = World.CreatePed(characterHash, player1.GetOffsetInWorldCoords(new Vector3(0, 5, 0)));
        
        while (!player2.Exists())
        {
            UI.ShowSubtitle("Setting up Player 2");
            Wait(100);
        }

        player2.IsEnemy = false;
        player2.IsInvincible = true;
        player2.DropsWeaponsOnDeath = false;

        // dont let the player2 ped decide what to do when there is combat etc.
        Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, player2, true);
        Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, player2, 0, 0);
        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, player2, 46, true);

        foreach (WeaponHash hash in Enum.GetValues(typeof(WeaponHash)))
        {
            try
            {
                Weapon weapon = player2.Weapons.Give(hash, int.MaxValue, true, true);
                weapon.InfiniteAmmo = true;
                weapon.InfiniteAmmoClip = true;
            }
            catch (Exception)
            {
            }
        }

        SelectWeapon(player2, WeaponHash.SMG);

        player2.Task.ClearAllImmediately();

        player2.AlwaysKeepTask = true;

        player2.NeverLeavesGroup = true;
        player2.RelationshipGroup = player1.RelationshipGroup;
        AddBlipToP2();

        if (player1.IsInVehicle())
        {
            Vehicle v = player1.CurrentVehicle;
            if (v.GetPedOnSeat(VehicleSeat.Driver) == player1)
            {
                player2.SetIntoVehicle(v, VehicleSeat.Passenger);
            }
            else
            {
                player2.SetIntoVehicle(v, VehicleSeat.Driver);
            }
        }

        SetupCamera();

        lastActions.Clear();
        foreach (Player2Action action in Enum.GetValues(typeof(Player2Action)))
        {
            lastActions.Add(action, Game.GameTime);
        }
        targets = GetTargets();
    }

    /// <summary>
    /// This method will setup the camera system
    /// </summary>
    private void SetupCamera()
    {
        camera = World.CreateCamera(player2.GetOffsetInWorldCoords(new Vector3(0, 10, 10)), Vector3.Zero, GameplayCamera.FieldOfView);
    }

    /// <summary>
    /// Iterates over all connected controllers and loads the InputManager if there is a valid configuration
    /// </summary>
    private void SetupController()
    {
        if (XInputManager.GetDevices().Count > 0)
        {
            foreach (Controller ctrl in XInputManager.GetDevices())
            {
                input = new XInputManager(ctrl);
            }
        }
        else
        {
            foreach (Joystick stick in DirectInputManager.GetDevices())
            {
                if (input != null)
                {
                    break;
                }
                try
                {
                    if (DirectInputManager.IsConfigured(stick, GetIniFile()))
                    {
                        if (DirectInputManager.IsDefault(stick, GetIniFile(), Name))
                        {
                            input = DirectInputManager.LoadConfig(stick, GetIniFile());
                        }
                    }
                    else
                    {
                        input = DirectInputManager.LoadConfig(stick, GetIniFile());
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message);
                    throw;
                }
            }

            if (input == null)
            {
                throw new Exception("No valid controller configuration found, please configure one from the menu.");
            }

        }
        UI.Notify("Using controller: " + input.DeviceName);
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
        CleanCamera();
        if (input != null)
        {
            input.Cleanup();
            input = null;
        }
        CleanUpPlayer2();
        if (player1 != null)
        {
            player1.IsInvincible = false;
        }
    }


    /// <summary>
    /// Cleans up player 2
    /// </summary>
    private void CleanUpPlayer2()
    {
        if (player2 != null)
        {
            player2.Delete();
            player2 = null;
        }
    }

    /// <summary>
    /// This method will return the center of two given vectors
    /// </summary>
    /// <param name="start">Vector3 A</param>
    /// <param name="end">Vector3 B</param>
    /// <returns>The center Vector3 of the two given Vector3s</returns>
    public Vector3 CenterOfVectors(Vector3 start, Vector3 end)
    {
        return (start + end) * 0.5f;
    }

    /// <summary>
    /// Makes player2 leave his vehicle normally when the vehicle is stationary/traveling slowly or jump out when not
    /// </summary>
    private void P2LeaveVehicle(Vehicle v)
    {
        if (v.Speed > 7f)
        {
            Function.Call(Hash.TASK_LEAVE_VEHICLE, player2, v, 4160); //4160 = ped is throwing himself out, even when the vehicle is still (that's what the speed check is for)
        }
        else
        {
            player2.Task.LeaveVehicle();
        }
    }

    private void TwoPlayerMod_Tick(object sender, EventArgs e)
    {
        menuPool.ProcessMenus();
        controllersMenuItem.Description = (controllerConnected ? controllersMenuDescConfig : controllersMenuDescNoControllers);

        if (Enabled())
        {
            if (!Player1Available())
            {
                CleanCamera();

                while (!Player1Available() || Function.Call<bool>(Hash.IS_SCREEN_FADING_IN))
                {
                    Wait(500);
                }

                player2.Position = World.GetNextPositionOnStreet(player1.GetOffsetInWorldCoords(new Vector3(0, 5, 0)));

                SetupCamera();
            }

            UpdateCamera();

            if (player2.IsInVehicle())
            {
                UpdateCombat(DeviceButton.LeftShoulder, DeviceButton.RightShoulder);

                Vehicle v = player2.CurrentVehicle;

                if (v.GetPedOnSeat(VehicleSeat.Driver) == player2)
                {
                    VehicleAction action = GetVehicleAction(v);
                    if (action != LastVehicleAction)
                    {
                        LastVehicleAction = action;
                        
                        PerformVehicleAction(player2, v, action);
                    }
                }

                if (input.isPressed(DeviceButton.X))
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
            else
            {
                UpdateFoot();
            }

            // for player2 entering / leaving a vehicle
            if (input.isPressed(DeviceButton.Y))
            {
                if (player2.IsInVehicle())
                {
                    P2LeaveVehicle(player2.CurrentVehicle);
                }
                else
                {
                    Vehicle v = World.GetClosestVehicle(player2.Position, 15);
                    if (v != null && v == player1.CurrentVehicle)
                    {
                        if (v.GetPedOnSeat(VehicleSeat.Passenger) == player1)
                        {
                            player2.Task.EnterVehicle(v, VehicleSeat.Driver);
                        }
                        else
                        {
                            player2.Task.EnterVehicle(v, VehicleSeat.Passenger);
                        }
                    }
                    else if (v != null)
                    {
                        Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);
                        if (driver != null && driver != player1)
                        {
                            driver.Delete();
                        }
                        player2.Task.EnterVehicle(v, VehicleSeat.Driver);
                    }
                }
            }

            // for letting player get in a vehicle
            if (player1.IsOnFoot && (Game.IsKeyPressed(Keys.G) || Game.IsControlJustReleased(0, GTA.Control.VehicleExit)))
            {
                Vehicle v = player2.CurrentVehicle;

                if (v != null)
                {
                    player1.Task.EnterVehicle(v, VehicleSeat.Passenger);
                }
                else if (v != null)
                {
                    player1.Task.EnterVehicle(v, VehicleSeat.Driver);
                }
                else
                {
                    v = World.GetClosestVehicle(player1.Position, 15);
                    if (v != null)
                    {
                        Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);
                        if (driver == null)
                        {
                            player1.Task.EnterVehicle(v, VehicleSeat.Driver);
                        }
                        else if (driver != player2)
                        {
                            driver.Delete();
                        }
                        player1.Task.EnterVehicle(v, VehicleSeat.Passenger);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Helper method to check if a WeaponHash is a throwable like Grenade or Molotov
    /// </summary>
    /// <param name="hash">WeaponHash to check</param>
    /// <returns>true if the given WeaponHash  is throwable, false otherwise</returns>
    private bool IsThrowable(WeaponHash hash)
    {
        return throwables.Contains(hash);
    }

    /// <summary>
    /// Helper method to check if a WeaponHash is a melee weapon
    /// </summary>
    /// <param name="hash">WeaponHash to check</param>
    /// <returns>true if the given WeaponHash  is melee, false otherwise</returns>
    private bool IsMelee(WeaponHash hash)
    {
        return meleeWeapons.Contains(hash);
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
        if (customCamera)
        {
            World.RenderingCamera = camera;
            Vector3 p1Pos = player1.Position;
            Vector3 p2Pos = player2.Position;

            Vector3 center = CenterOfVectors(p1Pos, p2Pos);
            camera.PointAt(center);

            float dist = p1Pos.DistanceTo(p2Pos);

            if (dist > 30)
            {
                UI.ShowSubtitle("Press backspace to teleport player 2 back to player 1.");
                if (Game.IsKeyPressed(Keys.Back))
                {
                    player2.Position = player1.GetOffsetInWorldCoords(new Vector3(0, 5, 0));
                }
            }

            center.Y += 5f + (dist / 1.6f);
            center.Z += 2f + (dist / 1.4f);

            camera.Position = center;

            if (player1.IsInVehicle() && player2.IsInVehicle() && (player1.CurrentVehicle == player2.CurrentVehicle))
            {
                World.RenderingCamera = null;
            }
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
    /// TODO: This method needs to be made better for more natural movement.
    /// </summary>
    private void UpdateFoot()
    {
        UpdateCombat(DeviceButton.LeftTrigger, DeviceButton.RightTrigger);

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
                dest = player2.Position - new Vector3(leftThumb.X, leftThumb.Y, 0);
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
    /// <param name="firstButton">The first (aiming) button which needs to pressed before handling this update</param>
    /// <param name="secondButton">The second (firing) button which needs to pressed before the actual shooting</param>
    private void UpdateCombat(DeviceButton firstButton, DeviceButton secondButton)
    {
        if (input.isPressed(DeviceButton.DPadLeft))
        {
            foreach (WeaponHash projectile in throwables)
            {
                Function.Call(Hash.EXPLODE_PROJECTILES, player2, new InputArgument(projectile), true);
            }
        }
        if (input.isPressed(firstButton))
        {
            if (!input.isPressed(secondButton))
            {
                targets = GetTargets();
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
                targets = GetTargets();
            }

            if (target != null)
            {
                World.DrawMarker(MarkerType.UpsideDownCone, target.GetBoneCoord(Bone.SKEL_Head) + new Vector3(0, 0, 1), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(1, 1, 1), Color.OrangeRed);

                if (input.isPressed(secondButton))
                {
                    if (IsThrowable(weapons[WeaponIndex]))
                    {
                        if (CanDoAction(Player2Action.ThrowTrowable, 1500))
                        {
                            SelectWeapon(player2, weapons[WeaponIndex]);
                            Function.Call(Hash.TASK_THROW_PROJECTILE, player2, target.Position.X, target.Position.Y, target.Position.Z);
                            UpdateLastAction(Player2Action.ThrowTrowable);
                        }
                    }
                    else if (CanDoAction(Player2Action.Shoot, 750))
                    {
                        if (player2.IsInVehicle())
                        {
                            Function.Call(Hash.TASK_DRIVE_BY, player2, target, 0, 0, 0, 0, 50.0f, 100, 1, (uint)FiringPattern.FullAuto);
                        }
                        else if (IsMelee(weapons[WeaponIndex]))
                        {
                            UI.ShowSubtitle("Melee weapons are not supported yet.");
                        }
                        else
                        {
                            player2.Task.ShootAt(target, 750, FiringPattern.FullAuto);
                        }

                        UpdateLastAction(Player2Action.Shoot);
                    }
                }
                else
                {
                    player2.Task.AimAt(target, 100);
                }
            }
        }
        else
        {
            TargetIndex = 0;
        }
    }

    /// <summary>
    /// Gets targets for player 2 in combat situations
    /// </summary>
    /// <returns>An array of Peds</returns>
    private Ped[] GetTargets()
    {
        return World.GetNearbyPeds(player2, 50).Where(ped => IsValidTarget(ped)).OrderBy(ped => ped.Position.DistanceTo(player2.Position)).ToArray();
    }

    /// <summary>
    /// Helper method to check if a Ped is a valid target for player 2
    /// </summary>
    /// <param name="target">The target Ped to check</param>
    private bool IsValidTarget(Ped target)
    {
        return target != null && target != player1 && target.IsAlive && target.IsOnScreen;
    }

    /// <summary>
    /// Method to select another weapon for a Ped
    /// </summary>
    /// <param name="p">target Ped</param>
    /// <param name="weaponHash">target WeaponHash</param>
    private void SelectWeapon(Ped p, WeaponHash weaponHash)
    {
        WeaponIndex = Array.IndexOf(weapons, weaponHash);

        if (p.IsInVehicle() && IsMelee(weaponHash))
        {
            while (IsMelee(weapons[WeaponIndex]))
            {
                WeaponIndex++;
            }
        }

        Function.Call(Hash.SET_CURRENT_PED_WEAPON, p, new InputArgument(weaponHash), true);
        UpdateLastAction(Player2Action.SelectWeapon);
    }
}