using GTA.Math;
using System.Collections.Generic;

namespace Benjamin94
{
    namespace Input
    {
        public class DeviceState
        {
            // the X and Y values of the sticks
            public Vector2 LeftThumbStick, RightThumbStick;
            // the buttons
            public List<DeviceButton> Buttons = new List<DeviceButton>();

            public override string ToString()
            {
                return "LeftStick: " + LeftThumbStick + ", RightStick: " + RightThumbStick + ", Buttons: " + string.Join(",", Buttons);
            }
        }
    }
}