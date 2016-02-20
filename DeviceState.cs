using Benjamin94.Input;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benjamin94
{
    namespace Input
    {
        public class DeviceState
        {
            // the X and Y values of the sticks
            public Vector2 LeftThumbStick = new Vector2(), RightThumbStick = new Vector2();
            // the buttons
            public List<DeviceButton> Buttons = new List<DeviceButton>();

            public override string ToString()
            {
                return "LeftStick: " + LeftThumbStick + ", RightStick: " + RightThumbStick + ", Buttons: " + string.Join(",", Buttons);
            }

        }
    }
}