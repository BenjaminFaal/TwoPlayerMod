using System;
using SharpDX.DirectInput;
using GTA;
namespace Benjamin94.Input
{
    /// <summary>
    /// This class is useful for letting the user configure their controller
    /// </summary>
    class ControllerWizard
    {
        private Joystick stick;

        public ControllerWizard(Joystick stick)
        {
            this.stick = stick;
        }

        /// <summary>
        /// Starts a wizard in which the user gets asked for all buttons one by one
        /// </summary>
        /// <param name="iniFile">The target INI file</param>
        /// <returns></returns>
        public bool StartConfiguration(string iniFile)
        {
            InputManager input = new InputManager(stick);

            ScriptSettings data = ScriptSettings.Load(iniFile);
            string guid = stick.Information.ProductGuid.ToString();
            data.SetValue(TwoPlayerMod.ScriptName, TwoPlayerMod.ControllerKey, guid);

            foreach (DeviceButton btn in Enum.GetValues(typeof(DeviceButton)))
            {
                Configure(btn, data, input, guid);
                UI.Notify(GetBtnText(btn) + " button configured.");
            }

            data.Save();
            return true;
        }

        /// <summary>
        /// Helper method to configure a single DeviceButton and add it the configuration
        /// </summary>
        /// <param name="btn">The target DeviceButton</param>
        /// <param name="data">The ScriptSettings object which it needs to be saved too</param>
        /// <param name="input">InputManager object to handle input</param>
        /// <param name="guid">The GUID of the controller</param>
        private void Configure(DeviceButton btn, ScriptSettings data, InputManager input, string guid)
        {
            while (input.GetPressedButton() == -1)
            {
                UI.ShowSubtitle("Press and hold the " + GetBtnText(btn) + " button on the controller for 1 second.");
                Script.Wait(100);
            }
            int button = input.GetPressedButton();
            UI.ShowSubtitle("Please hold the " + GetBtnText(btn) + " button to confirm it.");
            Script.Wait(1000);
            if (button != input.GetPressedButton())
            {
                UI.ShowSubtitle("Please and hold the " + GetBtnText(btn) + " button to confirm.");
                Script.Wait(1000);
                Configure(btn, data, input, guid);
            }
            else
            {
                data.SetValue(guid, btn.ToString(), button);
                while (input.GetPressedButton() != -1)
                {
                    UI.ShowSubtitle("Now let go the button to configure the next one.");
                    Script.Wait(100);
                }
                Script.Wait(1000);
            }
        }

        /// <summary>
        /// Helper method which shows the DeviceButon in green text
        /// </summary>
        /// <param name="btn">The specific DeviceButton</param>
        /// <returns>"~g~'Start'~w~" for example</returns>
        private string GetBtnText(DeviceButton btn)
        {
            return "~g~'" + btn + "'~w~";
        }
    }
}
