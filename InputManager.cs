using SharpDX.DirectInput;
using SharpDX.XInput;
using System.Collections.Generic;

namespace Benjamin94
{
    namespace Input
    {
        /// <summary>
        /// Abstract class for managing any type of input device
        /// </summary>
        public abstract class InputManager
        {
            protected float X_CENTER_L = 0;
            protected float Y_CENTER_L = 0;
            protected float X_CENTER_R = 0;
            protected float Y_CENTER_R = 0;

            /// <summary>
            /// Getter for user friendly device name
            /// </summary>
            public abstract string DeviceName { get; }

            /// <summary>
            /// Getter for GUID
            /// </summary>
            public abstract string DeviceGuid { get; }

            /// <summary>
            /// To determine if one or more DeviceButtons is/are pressed at this moment or not
            /// </summary>
            /// <param name="allPressed">True or false whether all given DeviceButtons need to pressed or just any of the DeviceButtons</param>
            /// <param name="btns">The DeviceButtons to check</param>
            /// <returns>true or false whether the DeviceButton is pressed</returns>
            private bool IsPressed(bool allPressed, params DeviceButton[] btns)
            {
                DeviceState state = GetState();

                if (allPressed)
                {
                    foreach (DeviceButton btn in btns)
                    {
                        allPressed = state.Buttons.Contains(btn);
                        if (!allPressed)
                        {
                            return false;
                        }
                    }
                    return allPressed;
                }
                else
                {
                    foreach (DeviceButton btn in btns)
                    {
                        if (state.Buttons.Contains(btn))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// Checks if a single of the given DeviceButtons is pressed
            /// </summary>
            /// <param name="btn">The DeviceButton to check</param>
            /// <returns></returns>
            public bool isPressed(DeviceButton btn)
            {
                return GetState().Buttons.Contains(btn);
            }

            /// <summary>
            /// Checks if all of the given DeviceButtons are pressed
            /// </summary>
            /// <param name="btns">The DeviceButtons to check</param>
            /// <returns></returns>
            public bool isAllPressed(params DeviceButton[] btns)
            {
                return IsPressed(true, btns);
            }

            /// <summary>
            /// Checks if one of the given DeviceButtons is pressed
            /// </summary>
            /// <param name="btns">The DeviceButtons to check</param>
            /// <returns></returns>
            public bool isAnyPressed(params DeviceButton[] btns)
            {
                return IsPressed(false, btns);
            }

            /// <summary>
            /// This method is useful when you want to do more advanced things with the device
            /// </summary>
            /// <returns>A DeviceState object which will contain all data</returns>
            public abstract DeviceState GetState();

            /// <summary>
            /// Gets the direction that belongs to one of the Sticks
            /// </summary>
            /// <returns>The Direction corresponding with the X and Y value</returns>
            public Direction GetDirection(DeviceButton stick)
            {
                DeviceState state = GetState();

                if (stick == DeviceButton.LeftStick)
                {
                    return GetDirection(state.LeftThumbStick.X, state.LeftThumbStick.Y, X_CENTER_L, Y_CENTER_L);
                }
                else if (stick == DeviceButton.RightStick)
                {
                    return GetDirection(state.RightThumbStick.X, state.RightThumbStick.Y, X_CENTER_R, Y_CENTER_R);
                }

                return Direction.None;
            }

            /// <summary>
            /// Helper method to check if any given Direction is left
            /// </summary>
            /// <param name="dir">The Direction to check</param>
            /// <returns></returns>
            public bool IsDirectionLeft(Direction dir)
            {
                return dir == Direction.Left || dir == Direction.BackwardLeft || dir == Direction.ForwardLeft;
            }
            /// <summary>
            /// Helper method to check if any given Direction is right
            /// </summary>
            /// <param name="dir">The Direction to check</param>
            /// <returns></returns>
            public bool IsDirectionRight(Direction dir)
            {
                return dir == Direction.Right || dir == Direction.BackwardRight || dir == Direction.ForwardRight;
            }

            /// <summary>
            /// Returns both XInput and DirectInput InputManager
            /// </summary>
            /// <returns>A List of InputManagers</returns>
            public static List<InputManager> GetAvailableInputManagers()
            {
                List<InputManager> managers = new List<InputManager>();
                foreach (Controller ctrl in XInputManager.GetDevices())
                {
                    managers.Add(new XInputManager(ctrl));
                }

                foreach (Joystick stick in DirectInputManager.GetDevices())
                {
                    managers.Add(new DirectInputManager(stick));
                }
                return managers;
            }

            /// <summary>
            /// Gets the DirectInput direction from a X and Y value
            /// </summary>
            /// <param name="X">The input X value</param>
            /// <param name="Y">The input Y value</param>
            /// <param name="xCenter">The start value of X</param>
            /// <param name="yCenter">The start value of Y</param>
            /// <returns>The Direction corresponding with the X and Y value</returns>
            protected abstract Direction GetDirection(float X, float Y, float xCenter, float yCenter);


            /// <summary>
            /// Call this method to cleanup this InputManager
            /// </summary>
            public abstract void Cleanup();
        }
    }
}