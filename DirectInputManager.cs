using GTA;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using GTA.Math;
using System.Linq;

namespace Benjamin94
{
    namespace Input
    {

        /// <summary>
        /// This class will be the InputManager for all DirectInput devices
        /// </summary>
        class DirectInputManager : InputManager
        {
            public const string DpadTypeKey = "DpadType";

            /// <summary>
            /// The Joystick which will be managed by this InputManager
            /// </summary>
            public Joystick device;

            /// <summary>
            /// Here the DeviceButton config from the ini file will be stored
            /// </summary>
            private List<Tuple<int, DeviceButton>> config;

            public override string DeviceName
            {
                get
                {
                    return device.Information.ProductName;
                }
            }

            /// <summary>
            /// Initializes a default InputManager without config.
            /// This means almost none of the methods will work such as IsPressed()
            /// </summary>
            /// <param name="device">The target Joystick which needs to be managed</param>
            public DirectInputManager(Joystick device)
            {
                this.device = device;
                if (device != null)
                {
                    device.Acquire();
                }
                config = new List<Tuple<int, DeviceButton>>(); // for digital dpads

                DeviceState state = GetState();
                X_CENTER_L = state.LeftThumbStick.X;
                Y_CENTER_L = state.LeftThumbStick.Y;
                X_CENTER_R = state.RightThumbStick.X;
                Y_CENTER_R = state.RightThumbStick.Y;
            }

            /// <summary>
            /// Helper method to get all connected DirectInput devices
            /// </summary>
            /// <returns>A List with connected Joysticks</returns>
            public static List<Joystick> GetDevices()
            {
                DirectInput directInput = new DirectInput();
                List<Joystick> sticks = new List<Joystick>();
                foreach (DeviceInstance dev in directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
                {
                    var joystick = new Joystick(directInput, dev.InstanceGuid);

                    // dont include Xbox controllers
                    string name = joystick.Information.ProductName.ToLower();
                    if (!name.Contains("xbox") && !name.Contains("360"))
                    {
                        sticks.Add(joystick);
                    }

                }
                return sticks;
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
            public static DirectInputManager LoadConfig(Joystick stick, string file)
            {
                DirectInputManager manager = new DirectInputManager(stick);
                try
                {
                    string name = stick.Information.ProductGuid.ToString();
                    ScriptSettings data = ScriptSettings.Load(file);

                    DpadType dpadType = DpadType.Unknown;
                    try
                    {
                        dpadType = (DpadType)Enum.Parse(typeof(DpadType), data.GetValue(name, DpadTypeKey, DpadType.Unknown.ToString()));
                    }
                    catch (Exception)
                    {
                        throw new Exception("Invalid controller config, unknown " + DpadTypeKey + " reconfigure your controller from the menu.");
                    }

                    // Array.Clear(manager.config, 0, manager.config.Length);
                    foreach (DeviceButton btn in Enum.GetValues(typeof(DeviceButton)))
                    {
                        int btnIndex = data.GetValue(name, btn.ToString(), -1);

                        try
                        {
                            manager.config.Add(new Tuple<int, DeviceButton>(btnIndex, btn));
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

            public int GetDpadValue()
            {
                try
                {
                    device.Poll();
                    JoystickState state = device.GetCurrentState();
                    foreach (int value in device.GetCurrentState().PointOfViewControllers)
                    {
                        if (value != -1)
                        {
                            return value;
                        }
                    }
                }
                catch (Exception)
                {
                }
                return -1;
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
                return IsConfigured(stick, file) && settings.GetValue(section, TwoPlayerMod.ControllerKey, "").ToLower().Equals(stick.Information.ProductGuid.ToString().ToLower());
            }

            /// <summary>
            /// Determines the first pressed button
            /// </summary>
            /// <returns>-1 if no pressed button or the button number if at least one pressed button</returns>
            public int GetPressedButton()
            {
                try
                {
                    JoystickState state = device.GetCurrentState();
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
                }
                catch (Exception)
                {
                }
                return -1;
            }

            /// <summary>
            /// Removes deadzone from the input X and Y
            /// </summary>
            /// <param name="x">input X</param>
            /// <param name="y">input Y</param>
            /// <returns></returns>
            private static Vector2 NormalizeThumbStick(float x, float y)
            {
                Vector2 vector = new Vector2((x - JOY_32767) / JOY_32767, -(y - JOY_32767) / JOY_32767);

                if (Math.Abs(vector.X) < 0.4f)
                {
                    vector.X = 0;
                }
                if (Math.Abs(vector.Y) < 0.4f)
                {
                    vector.Y = 0;
                }
                return vector;
            }

            public override DeviceState GetState()
            {
                try
                {
                    device.Poll();
                    JoystickState state = device.GetCurrentState();

                    DeviceState devState = new DeviceState();

                    bool[] buttons = state.Buttons;

                    for (int i = 0; i < buttons.Length; i++)
                    {
                        if (buttons[i])
                        {
                            Tuple<int, DeviceButton> tuple = config.FirstOrDefault(item => item.Item1 == i + 1);
                            if (tuple != null)
                            {
                                devState.Buttons.Add(tuple.Item2);
                            }
                        }
                    }

                    // for ps4 controllers or devices with dpads that are not recognized as buttons
                    foreach (int pov in state.PointOfViewControllers)
                    {
                        Tuple<int, DeviceButton> tuple = config.FirstOrDefault(item => item.Item1 == pov);
                        if (tuple != null)
                        {
                            devState.Buttons.Add(tuple.Item2);
                        }
                    }

                    devState.LeftThumbStick = NormalizeThumbStick(state.X, state.Y);
                    devState.RightThumbStick = NormalizeThumbStick(state.Z, state.RotationZ);

                    return devState;
                }
                catch (Exception)
                {
                    // most of times this exception occurs when its disconnected unexpectedly 
                    // by calling Acquire() it will always be reconnected as soon as the controller is plugged in again
                    try
                    {
                        device.Acquire();
                    }
                    catch (Exception)
                    {
                    }
                }
                return null;
            }

            private const int JOY_32767 = 32767;
            private const int JOY_0 = 0;
            private const int JOY_65535 = 65535;

            protected override Direction GetDirection(float X, float Y, float xCenter, float yCenter)
            {
                if (X < xCenter && Y > yCenter)
                {
                    return Direction.ForwardLeft;
                }
                if (X > xCenter && Y > yCenter)
                {
                    return Direction.ForwardRight;
                }
                if (X < xCenter && Y == yCenter)
                {
                    return Direction.Left;
                }
                if (X == xCenter && Y > yCenter)
                {
                    return Direction.Forward;
                }
                if (X == xCenter && Y < yCenter)
                {
                    return Direction.Backward;
                }
                if (X < xCenter && Y < yCenter)
                {
                    return Direction.BackwardLeft;
                }
                if (X > xCenter && Y < yCenter)
                {
                    return Direction.BackwardRight;
                }
                if (X > xCenter && Y == yCenter)
                {
                    return Direction.Right;
                }
                return Direction.None;
            }

            public override void Cleanup()
            {
                device.Unacquire();
                device = null;
                config = null;
            }
        }
    }
}
