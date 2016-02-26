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
    public const string CustomCameraKey = "CustomCamera";

    // Player 1
    private Player player;
    private Ped player1;

    // Player 2 
    private Ped player2;
    private WeaponHash[] weapons = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
    private int weaponIndex = 0;

    /// <summary>
    /// This variable will hold the last VehicleAction in order to not spam the Native calls
    /// </summary>
    private VehicleAction LastVehicleAction = VehicleAction.Wait;
    private int lastTimeWeaponChanged, lastTimeJumped;

    // Menu
    private UIMenu menu;
    private MenuPool menuPool;

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

        int index = 0;

        foreach (string item in hashes)
        {
            if (item.Equals(characterHash.ToString()))
            {
                index = hashes.IndexOf(item);
                break;
            }
        }

        UIMenuListItem characterItem = new UIMenuListItem("Character", hashes, index, "Select a character for player 2.");

        characterItem.OnListChanged += (s, i) =>
        {
            PedHash selectedHash = Enum.Parse(typeof(PedHash), s.IndexToItem(s.Index));
            characterHash = selectedHash;

            ScriptSettings settings = ScriptSettings.Load(GetIniFile());
            settings.SetValue(Name, CharacterHashKey, selectedHash.ToString());
            settings.Save();
        };

        menu.AddItem(characterItem);

        UIMenuCheckboxItem cameraItem = new UIMenuCheckboxItem("Toggle custom camera", customCamera, "This enables/disables the custom camera");
        cameraItem.CheckboxEvent += (s, i) =>
        {
            customCamera = !customCamera;
            ScriptSettings settings = ScriptSettings.Load(GetIniFile());
            settings.SetValue(Name, CustomCameraKey, customCamera.ToString());
            settings.Save();
        };
        menu.AddItem(cameraItem);

        bool controllersAvailable = DirectInputManager.GetDevices().Count > 0 || XInputManager.GetDevices().Count > 0;

        UIMenu controllersMenu = menuPool.AddSubMenu(menu, controllersAvailable ? "Controllers" : "No controllers found, please connect one");
        menu.MenuItems[1].Description = "Configure controllers, yellow star means configured successfully and default controller.";

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
                bool succes = wizard.StartConfiguration(GetIniFile());
                if (succes)
                {
                    UI.Notify("Controller successfully configured, you can now start playing.");

                    // if enabled the mod already then reload the InputManager so it is up to date with the new configuration
                    if (Enabled())
                    {
                        input = DirectInputManager.LoadConfig(stick, GetIniFile());
                    }
                }
                else
                {
                    UI.Notify("Controller configuration canceled, please configure your controller before playing.");
                }
                controllersMenu.GoBack();
            };
        }
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
    /// <param name="sender"></param>
    /// <param name="selectedItem"></param>
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
    /// Sets up Player 2
    /// </summary>
    private void SetupPlayer2()
    {
        player = Game.Player;
        player1 = player.Character;
        player1.IsInvincible = true;

        player2 = World.CreatePed(characterHash, player1.GetOffsetInWorldCoords(new Vector3(0, 5, 0)));
        player2.IsEnemy = false;
        player2.IsInvincible = true;
        player2.DropsWeaponsOnDeath = false;

        // dont let the player2 ped decide what to do when there is combat etc.
        Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, player2, true);
        Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, player2, 0, 0);
        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, player2, 46, true);

        // TODO: make player2 able to aim and shoot
        foreach (WeaponHash w in Enum.GetValues(typeof(WeaponHash)))
        {
            player2.Weapons.Give(w, int.MaxValue, w == WeaponHash.SMG, true);
        }

        SelectWeapon(player2, WeaponHash.SMG);

        player2.Task.ClearAllImmediately();

        player2.AlwaysKeepTask = true;

        player2.NeverLeavesGroup = true;
        player2.RelationshipGroup = player1.RelationshipGroup;
        player2.AddBlip().Sprite = BlipSprite.Standard;
        player2.CurrentBlip.Color = BlipColor.Green;

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
                catch (Exception)
                {
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
    /// <param name="A_0"></param>
    protected override void Dispose(bool A_0)
    {
        Clean();
        base.Dispose(A_0);
    }


    /// <summary>
    /// Toggles the menu
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
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
        World.DestroyAllCameras();
        World.RenderingCamera = null;
        if (input != null)
        {
            input.Cleanup();
            input = null;
        }
        if (player2 != null)
        {
            player2.Delete();
            player2 = null;
        }
        if (player1 != null)
        {
            player1.IsInvincible = false;
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

    private void TwoPlayerMod_Tick(object sender, EventArgs e)
    {
        menuPool.ProcessMenus();
        if (Enabled())
        {
            UpdateCamera();

            if (player2.IsInVehicle())
            {
                Vehicle v = player2.CurrentVehicle;

                if (v.GetPedOnSeat(VehicleSeat.Driver) == player2)
                {
                    VehicleAction action = GetVehicleAction(v);
                    if (action != LastVehicleAction)
                    {
                        PerformVehicleAction(player2, v, action);
                        LastVehicleAction = action;
                    }
                }

                if (input.IsPressed(DeviceButton.X) && CanSelectWeapon())
                {
                    weaponIndex++;
                    if (weaponIndex < 0)
                    {
                        weaponIndex = weapons.Length - 1;
                    }
                    if (weaponIndex >= weapons.Length)
                    {
                        weaponIndex = 0;
                    }
                    SelectWeapon(player2, weapons[weaponIndex]);
                }
            }
            else
            {
                UpdateFoot();
            }

            // for player2 entering / leaving a vehicle
            if (input.IsPressed(DeviceButton.Y))
            {
                if (player2.IsInVehicle())
                {
                    player2.Task.LeaveVehicle();
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
    /// Helper method to determin if the player should change to another weapon
    /// </summary>
    /// <returns>true if time since last change is more than 500 Game.GameTime, false otherwise</returns>
    private bool CanSelectWeapon()
    {
        return Game.GameTime - lastTimeWeaponChanged >= 500;
    }

    /// <summary>
    /// Helper method to determin if the player is able to jump again
    /// </summary>
    /// <returns>true if time since last jump is more than 1000 Game.GameTime, false otherwise</returns>
    private bool CanJump()
    {
        return Game.GameTime - lastTimeJumped >= 850;
    }

    /// <summary>
    /// Updates the selection of weapon for Player 2
    /// </summary>
    /// <returns>true if weaponIndex changed, false otherwise</returns>
    private bool UpdateWeaponIndex()
    {
        Direction dir = input.GetDirection(DeviceButton.RightStick);
        switch (dir)
        {
            case Direction.Left:
                weaponIndex--;
                break;
            case Direction.Right:
                weaponIndex++;
                break;
            case Direction.ForwardLeft:
                weaponIndex--;
                break;
            case Direction.ForwardRight:
                weaponIndex++;
                break;
            case Direction.BackwardLeft:
                weaponIndex--;
                break;
            case Direction.BackwardRight:
                weaponIndex++;
                break;
        }

        if (weaponIndex < 0)
        {
            weaponIndex = weapons.Length - 1;
        }
        if (weaponIndex >= weapons.Length)
        {
            weaponIndex = 0;
        }
        return dir != Direction.None;
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

            center.Y += 7.5f;
            center.Z += 7.5f;

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
        if (input.IsPressed(DeviceButton.LeftTrigger) && input.IsPressed(DeviceButton.RightTrigger))
        {
            return VehicleAction.Wait;
        }

        Direction dir = input.GetDirection(DeviceButton.LeftStick);
        if (input.IsPressed(DeviceButton.A) || input.IsPressed(DeviceButton.RightShoulder))
        {
            if (dir == Direction.Left || dir == Direction.BackwardLeft || dir == Direction.ForwardLeft)
            {
                return VehicleAction.HandBrakeLeft;
            }
            else if (dir == Direction.Right || dir == Direction.BackwardRight || dir == Direction.ForwardRight)
            {
                return VehicleAction.HandBrakeRight;
            }
            else
            {
                return VehicleAction.HandBrakeStraight;
            }
        }

        if (input.IsPressed(DeviceButton.RightTrigger))
        {
            if (dir == Direction.Left || dir == Direction.BackwardLeft || dir == Direction.ForwardLeft)
            {
                return VehicleAction.GoForwardLeft;
            }
            else if (dir == Direction.Right || dir == Direction.BackwardRight || dir == Direction.ForwardRight)
            {
                return VehicleAction.GoForwardRight;
            }
            else
            {
                return VehicleAction.RevEngineFast;
            }
        }

        if (input.IsPressed(DeviceButton.LeftTrigger))
        {
            if (dir == Direction.Left || dir == Direction.BackwardLeft || dir == Direction.ForwardLeft)
            {
                return VehicleAction.ReverseLeft;
            }
            if (dir == Direction.Right || dir == Direction.BackwardRight || dir == Direction.ForwardRight)
            {
                return VehicleAction.ReverseRight;
            }
            else
            {
                return VehicleAction.ReverseStraight;
            }
        }

        if (dir == Direction.Left || dir == Direction.BackwardLeft || dir == Direction.ForwardLeft)
        {
            return VehicleAction.GoForwardLeft;
        }
        if (dir == Direction.Right || dir == Direction.BackwardRight || dir == Direction.ForwardRight)
        {
            return VehicleAction.GoForwardRight;
        }

        return VehicleAction.Wait;
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
        Vector2 vector = input.GetState().LeftThumbStick;
        Vector3 newPos = new Vector3(vector.X, vector.Y, 0);

        if (newPos != Vector3.Zero)
        {
            newPos = player2.GetOffsetInWorldCoords(newPos);
            player2.Task.GoTo(newPos, true, 1);
            player2.Task.ClearAll();
        }

        if (input.IsPressed(DeviceButton.X) && CanJump())
        {
            player2.Task.Jump();
            lastTimeJumped = Game.GameTime;
        }

        if (input.IsPressed(DeviceButton.LeftShoulder) && CanSelectWeapon())
        {
            if (UpdateWeaponIndex())
            {
                SelectWeapon(player2, weapons[weaponIndex]);
            }
        }
    }

    /// <summary>
    /// Method to select another weapon for a Ped
    /// </summary>
    /// <param name="p">target Ped</param>
    /// <param name="weaponHash">target WeaponHash</param>
    private void SelectWeapon(Ped p, WeaponHash weaponHash)
    {
        Function.Call(Hash.SET_CURRENT_PED_WEAPON, p, new InputArgument(weaponHash), true);
        UI.ShowSubtitle("Player 2 weapon: ~g~" + weaponHash);
        lastTimeWeaponChanged = Game.GameTime;
    }
}