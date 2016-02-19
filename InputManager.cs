using GTA;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;

namespace Benjamin94
{
    namespace Input
    {
        /// <summary>
        /// Handles all controller related things
        /// </summary>
        public class InputManager
        {
            private DeviceButton[] config;

            /// <summary>
            /// The Joystick which will be managed by this InputManager
            /// </summary>
            private Joystick stick;

            /// <summary>
            /// Public getter for the Joystick instance
            /// </summary>
            public Joystick Stick { get { return stick; } private set { stick = value; value.Acquire(); } }


            /// <summary>
            /// Getter for the device name
            /// </summary>
            public string DeviceName { get { return stick.Information.ProductName; } }

            /// <summary>
            /// Initializes a default InputManager without config.
            /// This means almost none of the methods will work such as IsPressed()
            /// </summary>
            /// <param name="stick">The target Joystick which needs to be managed</param>
            public InputManager(Joystick stick)
            {
                Stick = stick;
                config = new DeviceButton[GetState().Buttons.Length];
            }

            /// <summary>
            /// Helper method to check if a Joystick is configured correctly
            /// </summary>
            /// <param name="stick">The Joystick which needs to be configured</param>
            /// <param name="file">The path to the config file</param>
            /// <returns>true or false whether the config file contains a valid configuration</returns>
            public static bool IsConfigured(Joystick stick, string file)
            {
                try
                {
                    LoadConfig(stick, file);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }


            /// <summary>
            /// Helper method to check if a controller is configured as default in a config file
            /// </summary>
            /// <param name="stick">The Joystick which needs to be checked</param>
            /// <param name="file">The path to the config file</param>
            /// <param name="section">The INI section in which the 'Controller' key will be, it will typically have a GUID as value</param>
            /// <returns></returns>
            public static bool IsDefault(Joystick stick, string file, string section)
            {
                ScriptSettings settings = ScriptSettings.Load(file);
                return IsConfigured(stick, file) && settings.GetValue(section, TwoPlayerMod.ControllerKey, "").Equals(stick.Information.ProductGuid.ToString());
            }

            /// <summary>
            /// Constructs a new InputManager by reading the config from a file
            /// 
            /// The file must contain something similar to this:
            /// 
            ///[0268054C-0000-0000-0000-504944564944]
            ///DPADUP = 5
            ///DPADDOWN = 7
            ///DPADLEFT = 8
            ///DPADRIGHT = 6
            ///BACK = 1
            ///BIGBUTTON = 17
            ///START = 4
            ///LEFTSTICK = 2
            ///RIGHTSTICK = 3
            ///A = 15
            ///B = 14
            ///X = 16
            ///Y = 13
            ///LEFTSHOULDER = 11
            ///LEFTTRIGGER = 9
            ///RIGHTSHOULDER = 12
            ///RIGHTTRIGGER = 10
            /// 
            /// </summary>
            /// <param name="stick">The Joystick instance which should be managed by this InputManager</param>
            /// <param name="file">Path to the file containing the config</param>
            /// <returns></returns>
            public static InputManager LoadConfig(Joystick stick, string file)
            {
                InputManager manager = new InputManager(stick);

                stick.Acquire();
                string name = stick.Information.ProductGuid.ToString();

                try
                {
                    ScriptSettings data = ScriptSettings.Load(file);
                    Array.Clear(manager.config, 0, manager.config.Length);
                    foreach (DeviceButton btn in Enum.GetValues(typeof(DeviceButton)))
                    {
                        int btnIndex = data.GetValue(name, btn.ToString(), -1);

                        try
                        {
                            manager.config[btnIndex] = btn;
                        }
                        catch (Exception)
                        {
                            throw new Exception("Invalid controller config, please reconfigure your controller from the menu.");
                        }
                    }
                }
                catch (Exception)
                {
                    throw new Exception("Error reading controller config, make sure the file contains a valid controller config.");
                }
                return manager;
            }

            /// <summary>
            /// Helper method to get all connected devices
            /// </summary>
            /// <returns>A List with Joysticks in it that are connected</returns>
            public static List<Joystick> GetDevices()
            {
                DirectInput directInput = new DirectInput();
                List<Joystick> sticks = new List<Joystick>();
                foreach (DeviceInstance dev in directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
                {
                    var joystick = new Joystick(directInput, dev.InstanceGuid);
                    sticks.Add(joystick);
                }
                return sticks;
            }


            /// <summary>
            /// To determine if a DeviceButton is pressed at this moment or not
            /// </summary>
            /// <param name="btn">The DeviceButton to check</param>
            /// <returns>true or false whether the DeviceButton is pressed</returns>
            public bool IsPressed(DeviceButton btn)
            {
                JoystickState state = GetState();

                if (state == null)
                {
                    return false;
                }

                bool[] buttons = state.Buttons;
                for (int i = 0; i < buttons.Length; i++)
                {
                    int button = i + 1;
                    if (buttons[i] && btn == config[i+1])
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Determines the first pressed button
            /// </summary>
            /// <returns>-1 if no pressed button or the button number if at least one pressed button</returns>
            public int GetPressedButton()
            {
                JoystickState state = GetState();

                if (state == null)
                {
                    return -1;
                }

                bool[] buttons = state.Buttons;

                for (int i = 0; i < buttons.Length; i++)
                {
                    int button = i + 1;

                    if (buttons[i])
                    {
                        return button;
                    }
                }
                return -1;
            }


            /// <summary>
            /// This method is useful when you want to do more advanced things with the Joystick
            /// </summary>
            /// <returns>A JoystickState object which will contain all data</returns>
            public JoystickState GetState()
            {
                try
                {
                    stick.Poll();
                    return stick.GetCurrentState();
                }
                catch (Exception)
                {
                    try
                    {
                        stick.Acquire();
                    }
                    catch (Exception)
                    {
                    }
                    return null;
                }
            }

            /// <summary>
            /// Gets the direction that belongs to one of the Sticks
            /// </summary>
            /// <param name="X">The input X value</param>
            /// <param name="Y">The input Y value</param>
            /// <returns>The Direction corresponding with the X and Y value</returns>
            public Direction GetDirection(DeviceButton stick)
            {
                JoystickState state = GetState();

                if (stick == DeviceButton.LeftStick)
                {
                    return GetDirection(state.X, state.Y);
                }
                else if (stick == DeviceButton.RightStick)
                {
                    return GetDirection(state.Z, state.RotationZ);
                }

                return Direction.None;
            }

            private const int JOY_32767 = 32767;
            private const int JOY_0 = 0;
            private const int JOY_65535 = 65535;

            /// <summary>
            /// Gets the direction from a X and Y value
            /// </summary>
            /// <param name="X">The input X value</param>
            /// <param name="Y">The input Y value</param>
            /// <returns>The Direction corresponding with the X and Y value</returns>
            private Direction GetDirection(float X, float Y)
            {
                if (X == JOY_0 && Y == JOY_0)
                {
                    return Direction.ForwardLeft;
                }
                if (X == JOY_0)
                {
                    return Direction.Left;
                }
                if (Y == JOY_0)
                {
                    return Direction.Forward;
                }

                if (X == JOY_0 && Y == JOY_65535)
                {
                    return Direction.BackwardLeft;
                }
                if (X == JOY_32767 && Y == JOY_65535)
                {
                    return Direction.Backward;
                }
                if (X == JOY_65535 && Y == JOY_65535)
                {
                    return Direction.BackwardRight;
                }
                if (X == JOY_65535 && Y == JOY_32767)
                {
                    return Direction.Right;
                }

                return Direction.None;
            }

            /// <summary>
            /// Call this method after you are done with this InputManager
            /// </summary>
            public void Release()
            {
                stick.Unacquire();
            }
        }
    }
}