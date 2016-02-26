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
        /// To determine what type of dpad a controller has
        /// </summary>
        enum DpadType
        {
            /// <summary>
            /// For generic usb gamepads
            /// </summary>
            ButtonsDpad,

            /// <summary>
            /// For controllers like a PS4 or with digital dpad
            /// </summary>
            DigitalDpad,

            /// <summary>
            /// For unrecognized controllers
            /// </summary>
            Unknown
        }
    }
}