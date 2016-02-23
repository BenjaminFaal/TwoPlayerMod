using SharpDX.XInput;
using System;
using System.Collections.Generic;
using GTA.Math;
using GTA;
using FakeXboxController;

namespace Benjamin94
{
    namespace Input
    {
        /// <summary>
        /// This class will be the InputManager for all XInput devices
        /// </summary>
        class XInputManager : InputManager
        {
            /// <summary>
            /// The XInput Controller which will be managed by this InputManager
            /// </summary>
            private Controller device;

            public XInputManager(Controller device)
            {
                this.device = device;
                DeviceState state = GetState();
                X_CENTER_L = state.LeftThumbStick.X;
                Y_CENTER_L = state.LeftThumbStick.Y;
                X_CENTER_R = state.RightThumbStick.X;
                Y_CENTER_R = state.RightThumbStick.Y;    
            }

            public override string DeviceName
            {
                get
                {
                    return "XInput controller user: " + device.UserIndex;
                }
            }

            /// <summary>
            /// Helper method to get all connected XInput devices
            /// </summary>
            /// <returns>A List with connected Controllers</returns>
            public static List<Controller> GetDevices()
            {
                List<Controller> controllers = new List<Controller>();
                foreach (UserIndex index in Enum.GetValues(typeof(UserIndex)))
                {
                    if (index != UserIndex.Any)
                    {
                        Controller controller = new Controller(index);
                        if (controller.IsConnected)
                        {
                            controllers.Add(controller);
                        }
                    }
                }
                return controllers;
            }

            public override DeviceState GetState()
            {
                try
                {
                    Gamepad pad = device.GetState().Gamepad;

                    DeviceState state = new DeviceState();

                    state.LeftThumbStick = NormalizeThumbStick(pad.LeftThumbX, pad.LeftThumbY, Gamepad.LeftThumbDeadZone);
                    state.RightThumbStick = NormalizeThumbStick(pad.RightThumbX, pad.RightThumbY, Gamepad.RightThumbDeadZone);

                    if (pad.LeftTrigger > Gamepad.TriggerThreshold)
                    {
                        state.Buttons.Add(DeviceButton.LeftTrigger);
                    }
                    if (pad.RightTrigger > Gamepad.TriggerThreshold)
                    {
                        state.Buttons.Add(DeviceButton.RightTrigger);
                    }

                    if (pad.Buttons != GamepadButtonFlags.None)
                    {
                        if ((pad.Buttons & GamepadButtonFlags.DPadUp) != 0)
                        {
                            state.Buttons.Add(DeviceButton.DPadUp);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.DPadLeft) != 0)
                        {
                            state.Buttons.Add(DeviceButton.DPadLeft);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.DPadRight) != 0)
                        {
                            state.Buttons.Add(DeviceButton.DPadRight);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.DPadDown) != 0)
                        {
                            state.Buttons.Add(DeviceButton.DPadDown);
                        }

                        if ((pad.Buttons & GamepadButtonFlags.A) != 0)
                        {
                            state.Buttons.Add(DeviceButton.A);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.B) != 0)
                        {
                            state.Buttons.Add(DeviceButton.B);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.X) != 0)
                        {
                            state.Buttons.Add(DeviceButton.X);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.Y) != 0)
                        {
                            state.Buttons.Add(DeviceButton.Y);
                        }

                        if ((pad.Buttons & GamepadButtonFlags.Start) != 0)
                        {
                            state.Buttons.Add(DeviceButton.Start);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.Back) != 0)
                        {
                            state.Buttons.Add(DeviceButton.Back);
                        }

                        if ((pad.Buttons & GamepadButtonFlags.LeftShoulder) != 0)
                        {
                            state.Buttons.Add(DeviceButton.LeftShoulder);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.RightShoulder) != 0)
                        {
                            state.Buttons.Add(DeviceButton.RightShoulder);
                        }

                        if ((pad.Buttons & GamepadButtonFlags.LeftThumb) != 0)
                        {
                            state.Buttons.Add(DeviceButton.LeftStick);
                        }
                        if ((pad.Buttons & GamepadButtonFlags.RightThumb) != 0)
                        {
                            state.Buttons.Add(DeviceButton.RightStick);
                        }
                    }
                    return state;
                }
                catch (Exception)
                {
                    return new DeviceState();
                }
            }


            /// <summary>
            /// Removes deadzone from the input X and Y
            /// </summary>
            /// <param name="x">input X</param>
            /// <param name="y">input Y</param>
            /// <param name="deadZone">the deadzone to remove</param>
            /// <returns></returns>
            private static Vector2 NormalizeThumbStick(short x, short y, short deadZone)
            {
                int fx = x;
                int fy = y;
                int fdz = deadZone;
                if (fx * fx < fdz * fdz)
                    x = 0;
                if (fy * fy < fdz * fdz)
                    y = 0;
                return new Vector2(x < 0 ? -((float)x / (float)short.MinValue) : (float)x / (float)short.MaxValue,
                                   y < 0 ? -((float)y / (float)short.MinValue) : (float)y / (float)short.MaxValue);
            }

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
                // nothing to do here
            }
        }
    }
}
