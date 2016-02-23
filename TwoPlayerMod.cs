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
using FakeXboxController;
using System.Threading;

/// <summary>
/// The main Script, this will handle all logic
/// </summary>
public class TwoPlayerMod : Script
{
    // for ini keys
    public const string ScriptName = "TwoPlayerMod";
    private const string ToggleKeyKey = "ToggleMenuKey";
    public const string ControllerKey = "Controller";

    // Players
    private Player player;
    private Ped player1;
    private Ped player2;

    // Menu
    private UIMenu menu;
    private MenuPool menuPool;

    // Settings
    private Keys toggleMenuKey = Keys.F11;

    // Controls
    private InputManager input;

    // fake xbox controller to disable gta v from controlling player 1
    private ScpDevice fakeDevice;

    public TwoPlayerMod()
    {
        LoadSettings();
        SetupMenu();

        KeyDown += TwoPlayerMod_KeyDown;
        Tick += TwoPlayerMod_Tick;
    }

    // fake xbox controller to disable gta v from controlling player 1
    private void SetupFakeDevice()
    {
        if (fakeDevice == null)
        {
            fakeDevice = new ScpDevice("{F679F562-3164-42CE-A4DB-E7DDBE723909}");
            fakeDevice.Start();
            UI.Notify("If the controller is controlling both characters, please reconnect it.");
        }
    }

    /// <summary>
    /// Loads the mod settings, if there is an error it will revert the file back to default
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            ScriptSettings settings = ScriptSettings.Load(GetIniFile());
            toggleMenuKey = (Keys)new KeysConverter().ConvertFromString(settings.GetValue(Name, ToggleKeyKey, Keys.F11.ToString()));
        }
        catch (Exception)
        {
            toggleMenuKey = Keys.F11;
            UI.Notify("Failed to load config, default " + ToggleKeyKey + " is F11");

            WriteDefaultConfig();
        }
    }

    /// <summary>
    /// Writes default config file
    /// </summary>
    private void WriteDefaultConfig()
    {
        ScriptSettings settings = ScriptSettings.Load(GetIniFile());
        settings.SetValue(Name, ToggleKeyKey, Keys.F11.ToString());
        settings.Save();
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
    /// Method to configure controllers buttons
    /// </summary>
    /// <param name="btn">DeviceButton which to configure</param>
    /// <param name="input">InputManager instance</param>
    /// <param name="settings">The ScriptSettings object where the configuration needs to be saved</param>
    /// <param name="guid">The guid of the Joystick</param>
    /// <param name="btns">A List with already pressed buttons so that no double mappings can be made</param>
    private void ConfigureButton(DeviceButton btn, DirectInputManager input, ScriptSettings settings, string guid, List<int> btns)
    {
        while (input.GetPressedButton() == -1)
        {
            UI.ShowSubtitle("Press ~g~'" + btn + "' ~w~on the controller.");
            Wait(100);
        }

        int button = input.GetPressedButton();

        // check if the configuration contains no mapping already
        if (btns.Contains(button))
        {
            UI.Notify("Button already in use, please choose another button!");
            ConfigureButton(btn, input, settings, guid, btns);
        }
        else
        {
            settings.SetValue(guid, btn.ToString(), button);
            btns.Add(button);
        }
        Wait(250);
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
                SetupPlayer2();
            }
            catch (Exception e)
            {
                UI.Notify(e.Message);
            }
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

        player2 = World.CreateRandomPed(player1.GetOffsetInWorldCoords(new Vector3(0, 5, 0)));
        player2.ShootRate = 0;
        player2.IsEnemy = false;
        player2.IsInvincible = true;
        player2.DropsWeaponsOnDeath = false;

        // dont let the player2 ped decide what to do when there is combat etc.
        Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, player2, true);
        Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, player2, 0, 0);
        Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, player2, 46, true);

        // TODO: make player2 able to aim and shoot
        //foreach (WeaponHash w in Enum.GetValues(typeof(WeaponHash)))
        //{
        //    player2.Weapons.Give(w, int.MaxValue, w == WeaponHash.Pistol, true);
        //}

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
    }

    /// <summary>
    /// Iterates over all connected controllers and loads the InputManager if there is a valid configuration
    /// </summary>
    private void SetupController()
    {
        if (XInputManager.GetDevices().Count > 0)
        {
            SetupFakeDevice();

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
                        throw new Exception("No valid controller configuration found, please configure one from the menu.");
                    }
                }
                catch (Exception)
                {
                    throw;
                }
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
        if (fakeDevice != null)
        {
            fakeDevice.Stop();
            fakeDevice = null;
        }

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
    /// This method will be used later to edit the camera and center it between the two players
    /// </summary>
    /// <param name="start">Vector3 A</param>
    /// <param name="end">Vector3 B</param>
    /// <returns>The center Vector3 of the two given Vector3s</returns>
    public Vector3 CenterOfVectors(Vector3 start, Vector3 end)
    {
        Vector3 sum = Vector3.Zero;
        var difference = start - end;
        var midPoint = start + difference * 1;
        return midPoint;
    }



    /// <summary>
    /// This variable will hold the last VehicleAction in order to not spam the Native calls
    /// </summary>
    private VehicleAction LastVehicleAction = VehicleAction.Wait;

    private void TwoPlayerMod_Tick(object sender, EventArgs e)
    {
        if (input != null)
        {
            UI.ShowSubtitle("normal: " + Game.GetControlNormal(1, GTA.Control.VehicleAccelerate) + "state: " + input.GetState());
        }

        menuPool.ProcessMenus();
        if (Enabled())
        {
            if (player2.IsInVehicle())
            {
                Vehicle v = player2.CurrentVehicle;

                if (v.GetPedOnSeat(VehicleSeat.Driver) == player2)
                {
                    VehicleAction action = GetVehicleAction();
                    if (action != LastVehicleAction)
                    {
                        PerformVehicleAction(player2, v, action);
                        LastVehicleAction = action;
                    }
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
    /// Determines the needed action corresponding to current controller input, e.g. VehicleAction.RevEngine
    /// </summary>
    /// <returns>A VehicleAction enum</returns>
    private VehicleAction GetVehicleAction()
    {
        Direction dir = input.GetDirection(DeviceButton.LeftStick);
        if (input.IsPressed(DeviceButton.A) || input.IsPressed(DeviceButton.RightShoulder))
        {
            if (dir == Direction.Left)
            {
                return VehicleAction.HandBrakeLeft;
            }
            else if (dir == Direction.Right)
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
            if (dir == Direction.Left)
            {
                return VehicleAction.GoForwardLeft;
            }
            else if (dir == Direction.Right)
            {
                return VehicleAction.GoForwardRight;
            }
            else
            {
                return VehicleAction.GoForwardStraight;
            }
        }

        if (input.IsPressed(DeviceButton.LeftTrigger))
        {
            if (dir == Direction.Left)
            {
                return VehicleAction.ReverseLeft;
            }
            if (dir == Direction.Right)
            {
                return VehicleAction.ReverseRight;
            }
            else
            {
                return VehicleAction.ReverseStraight;
            }
        }

        if (dir == Direction.Left)
        {
            return VehicleAction.SwerveRight;
        }
        if (dir == Direction.Right)
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
        Vector2 vector = input.GetState().LeftThumbStick;
        Vector3 newPos = new Vector3(vector.X, vector.Y, 0);
        if (newPos != Vector3.Zero)
        {
            newPos = player2.GetOffsetInWorldCoords(newPos);
            player2.Task.GoTo(newPos, true, 1);
        }

        if (input.IsPressed(DeviceButton.X))
        {
            player2.Task.Jump();
            Wait(750);
        }
    }
}