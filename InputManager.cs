using Benjamin94.Input;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            /// To determine if a DeviceButton is pressed at this moment or not
            /// </summary>
            /// <param name="btn">The DeviceButton to check</param>
            /// <returns>true or false whether the DeviceButton is pressed</returns>
            public bool IsPressed(DeviceButton btn)
            {
                return GetState().Buttons.Contains(btn);
            }

            /// <summary>
            /// This method is useful when you want to do more advanced things with the device
            /// </summary>
            /// <returns>A DeviceState object which will contain all data</returns>
            public abstract DeviceState GetState();

            /// <summary>
            /// Gets the direction that belongs to one of the Sticks
            /// </summary>
            /// <param name="X">The input X value</param>
            /// <param name="Y">The input Y value</param>
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
            /// Gets the DirectInput direction from a X and Y value
            /// </summary>
            /// <param name="X">The input X value</param>
            /// <param name="Y">The input Y value</param>
            /// <param name="xCenter">The start value of X</param>
            /// <param name="yCenter">The start value of Y</param>
            /// <returns>The Direction corresponding with the X and Y value</returns>
            protected abstract Direction GetDirection(float X, float Y, float xCenter, float yCenter);
        }
    }
}