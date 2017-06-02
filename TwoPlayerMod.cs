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
        //Function.Call(Hash.LOCK_MINIMAP_ANGLE, 0);
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
                if (!customCamera) UI.Notify("In Normal MODE");
                if (customCamera) UI.Notify("In SA MODE");
            }

            // for letting player get in a vehicle
            if (player1.IsOnFoot && Game.IsControlJustReleased(0, GTA.Control.VehicleExit))
            {
                HandleEnterVehicle(player1);
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
    /// This will update the camera 
    /// </summary>
    private void UpdateCamera()
    {
        // if all in same vehicle
        if (playerPeds.TrueForAll(p => { return p.Ped.CurrentVehicle != null && p.Ped.CurrentVehicle == player1.CurrentVehicle; }))
        {
            World.RenderingCamera = null;
            Function.Call(Hash.UNLOCK_MINIMAP_ANGLE, 0);
            //if (!customCamera && Game.IsControlJustReleased(0, GTA.Control.NextCamera)) UI.Notify("Normal Mode");
        }
        else if (customCamera)
        {
            Function.Call(Hash.LOCK_MINIMAP_ANGLE, 0);
            PlayerPed furthestPlayer = playerPeds.OrderByDescending(playerPed => { return player1.Position.DistanceTo(playerPed.Ped.Position); }).FirstOrDefault();

            Vector3 center = CenterOfVectors(player1.Position, furthestPlayer.Ped.Position);
            World.RenderingCamera = camera;
            //if (Game.IsControlJustReleased(0, GTA.Control.NextCamera) && World.RenderingCamera == camera) UI.Notify("In SA MODE");

            camera.PointAt(center);

            float dist = furthestPlayer.Ped.Position.DistanceTo(player1.Position);

            center.Y += 5f + (dist / 1.6f);
            center.Z += 2f + (dist / 1.4f);

            camera.Position = center;
        }
        else
        {
            World.RenderingCamera = null;
            //if (Game.IsControlJustReleased(0, GTA.Control.NextCamera) && World.RenderingCamera == null) UI.Notify("Normal Mode");
            Function.Call(Hash.UNLOCK_MINIMAP_ANGLE, 0);
        }
    }
}