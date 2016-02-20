using SharpDX.XInput;
using System;
using System.Collections.Generic;
using GTA.Math;
using GTA;

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

                    state.LeftThumbStick = new Vector2(pad.LeftThumbX, pad.LeftThumbY);
                    state.RightThumbStick = new Vector2(pad.RightThumbX, pad.RightThumbY);

                    if (pad.LeftTrigger != 0)
                    {
                        state.Buttons.Add(DeviceButton.LeftTrigger);
                    }
                    if (pad.RightTrigger != 0)
                    {
                        state.Buttons.Add(DeviceButton.RightTrigger);
                    }

                    switch (pad.Buttons)
                    {
                        case GamepadButtonFlags.DPadUp:
                            state.Buttons.Add(DeviceButton.DPadUp);
                            break;
                        case GamepadButtonFlags.DPadDown:
                            state.Buttons.Add(DeviceButton.DPadDown);
                            break;
                        case GamepadButtonFlags.DPadLeft:
                            state.Buttons.Add(DeviceButton.DPadLeft);
                            break;
                        case GamepadButtonFlags.DPadRight:
                            state.Buttons.Add(DeviceButton.DPadRight);
                            break;
                        case GamepadButtonFlags.Start:
                            state.Buttons.Add(DeviceButton.Start);
                            break;
                        case GamepadButtonFlags.Back:
                            state.Buttons.Add(DeviceButton.Back);
                            break;
                        case GamepadButtonFlags.LeftThumb:
                            state.Buttons.Add(DeviceButton.LeftStick);
                            break;
                        case GamepadButtonFlags.RightThumb:
                            state.Buttons.Add(DeviceButton.RightStick);
                            break;
                        case GamepadButtonFlags.LeftShoulder:
                            state.Buttons.Add(DeviceButton.LeftShoulder);
                            break;
                        case GamepadButtonFlags.RightShoulder:
                            state.Buttons.Add(DeviceButton.RightShoulder);
                            break;
                        case GamepadButtonFlags.A:
                            state.Buttons.Add(DeviceButton.A);
                            break;
                        case GamepadButtonFlags.B:
                            state.Buttons.Add(DeviceButton.B);
                            break;
                        case GamepadButtonFlags.X:
                            state.Buttons.Add(DeviceButton.X);
                            break;
                        case GamepadButtonFlags.Y:
                            state.Buttons.Add(DeviceButton.Y);
                            break;
                    }

                    return state;
                }
                catch (Exception)
                {
                    return new DeviceState();
                }
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
        }
    }
}
