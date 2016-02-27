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
    private const string CustomCameraZoomKey = "CustomCameraZoom";

    // Player 1
    private Player player;
    private Ped player1;

    // Player 2 
    private Ped player2;
    private WeaponHash[] weapons = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
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
    private VehicleAction LastVehicleAction = VehicleAction.Wait;

    private Dictionary<Player2Action, int> lastActions = new Dictionary<Player2Action, int>();

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
    private const int DefaultCameraZoom = 7;
    private int cameraZoom = DefaultCameraZoom;
    private int CameraZoom
    {
        get
        {
            return cameraZoom;
        }
        set
        {
            cameraZoom = value;

            if (cameraZoom <= DefaultCameraZoom)
            {
                cameraZoom = DefaultCameraZoom;
            }
            if (cameraZoom > 20)
            {
                cameraZoom = DefaultCameraZoom;
            }
        }
    }

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
            CameraZoom = int.Parse(settings.GetValue(Name, CustomCameraZoomKey, DefaultCameraZoom.ToString()));
        }
        catch (Exception)
        {
            CameraZoom = DefaultCameraZoom;
            UI.Notify("Failed to read '" + CustomCameraZoomKey + "', reverting to default " + CustomCameraZoomKey + " " + DefaultCameraZoom);

            settings.SetValue(Name, CustomCameraZoomKey, DefaultCameraZoom);
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

        ScriptSettings settings = ScriptSettings.Load(GetIniFile());

        UIMenuListItem characterItem = new UIMenuListItem("Character", hashes, index, "Select a character for player 2.");

        characterItem.OnListChanged += (s, i) =>
        {
            PedHash selectedHash = Enum.Parse(typeof(PedHash), s.IndexToItem(s.Index));
            characterHash = selectedHash;
            settings.SetValue(Name, CharacterHashKey, selectedHash.ToString());
            settings.Save();
        };

        menu.AddItem(characterItem);

        UIMenuItem cameraZoomItem = new UIMenuItem("Custom camera zoom " + CameraZoom, "Sets the custom camera zoom, from " + DefaultCameraZoom + " to 20");
        cameraZoomItem.Enabled = customCamera;
        cameraZoomItem.Activated += (s, i) =>
        {
            if (Enabled() && customCamera)
            {
                while (!Game.IsKeyPressed(Keys.Space))
                {
                    UI.ShowSubtitle("Camera zoom ~g~(" + CameraZoom + ")~w~, press ~g~Plus~w~ or ~g~Min~w~ to change. Press ~g~" + Keys.Space + " ~w~to confirm.");
                    if (Game.IsKeyPressed(Keys.Subtract))
                    {
                        CameraZoom--;
                        Wait(250);
                    }
                    if (Game.IsKeyPressed(Keys.Add))
                    {
                        CameraZoom++;
                        Wait(250);
                    }

                    UpdateCamera();
                    Yield();
                }

                UI.ShowSubtitle(string.Empty);
                settings.SetValue(Name, CustomCameraZoomKey, CameraZoom);
                settings.Save();
            }
            else
            {
                UI.Notify("Please enable the mod and custom camera first before setting a zoom level.");
            }
        };

        UIMenuCheckboxItem cameraItem = new UIMenuCheckboxItem("Toggle custom camera", customCamera, "This enables/disables the custom camera");
        cameraItem.CheckboxEvent += (s, i) =>
        {
            customCamera = !customCamera;
            cameraZoomItem.Enabled = customCamera;
            settings.SetValue(Name, CustomCameraKey, customCamera.ToString());
            settings.Save();
        };
        menu.AddItem(cameraItem);


        menu.AddItem(cameraZoomItem);

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

        foreach (WeaponHash hash in Enum.GetValues(typeof(WeaponHash)))
        {
            Weapon weapon = player2.Weapons.Give(hash, int.MaxValue, true, true);
            weapon.InfiniteAmmo = true;
            weapon.InfiniteAmmoClip = true;
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

        lastActions.Clear();
        foreach (Player2Action action in Enum.GetValues(typeof(Player2Action)))
        {
            lastActions.Add(action, Game.GameTime);
        }
        targets = GetTargets();
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

    private void TwoPlayerMod_Tick(object sender, EventArgs e)
    {
        menuPool.ProcessMenus();
        if (Enabled())
        {
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
                        PerformVehicleAction(player2, v, action);
                        LastVehicleAction = action;
                    }
                }

                if (input.isPressed(DeviceButton.X))
                {
                    if (CanDoAction(Player2Action.SelectWeapon, 500))
                    {
                        WeaponIndex++;
                        SelectWeapon(player2, weapons[WeaponIndex]);
                    }

                    UI.ShowSubtitle("Player 2 weapon: ~g~" + weapons[WeaponIndex]);
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
        Direction dir = input.GetDirection(DeviceButton.RightStick);

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

            center.Y += CameraZoom;
            center.Z += CameraZoom;

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
        if (input.isAnyPressed(DeviceButton.A, DeviceButton.RightShoulder))
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

        if (input.isPressed(DeviceButton.RightTrigger))
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

        if (input.isPressed(DeviceButton.LeftTrigger))
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

        if (input.IsDirectionLeft(dir))
        {
            return VehicleAction.SwerveRight;
        }
        if (input.IsDirectionRight(dir))
        {
            return VehicleAction.SwerveLeft;
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
        UpdateCombat(DeviceButton.LeftTrigger, DeviceButton.RightTrigger);

        Vector2 vector = input.GetState().LeftThumbStick;
        Vector3 newPos = new Vector3(vector.X, vector.Y, 0);

        if (newPos != Vector3.Zero)
        {
            newPos = player2.GetOffsetInWorldCoords(newPos);

            float speed = input.isPressed(DeviceButton.A) ? 2f : 1f;

            Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, player2.Handle, newPos.X, newPos.Y, newPos.Z, speed, 750, player2.Heading, 0);
        }

        if (input.isPressed(DeviceButton.X) && CanDoAction(Player2Action.Jump, 850))
        {
            player2.Task.Jump();
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
            UI.ShowSubtitle("Player 2 weapon: ~g~" + weapons[WeaponIndex]);
        }
    }


    /// <summary>
    /// This will fire at the targeted ped and will handle changing targets
    /// </summary>
    /// <param name="firstButton">The first (aiming) button which needs to pressed before handling this update</param>
    /// <param name="secondButton">The second (firing) button which needs to pressed before the actual shooting</param>
    private void UpdateCombat(DeviceButton firstButton, DeviceButton secondButton)
    {
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
                World.DrawMarker(MarkerType.HorizontalSplitArrowCircle, target.Position, GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(2.5f, 2.5f, 2.5f), Color.OrangeRed);

                if (input.isPressed(secondButton))
                {
                    if (CanDoAction(Player2Action.Shoot, 750))
                    {
                        if (player2.IsInVehicle())
                        {
                            Function.Call(Hash.TASK_DRIVE_BY, player2, target, 0, 0, 0, 0, 50.0f, 100, 1, (uint)FiringPattern.FullAuto);
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
    /// <returns></returns>
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
        Function.Call(Hash.SET_CURRENT_PED_WEAPON, p, new InputArgument(weaponHash), true);
        UpdateLastAction(Player2Action.SelectWeapon);
    }
}