using System;
using SharpDX.DirectInput;
using GTA;
using System.Collections.Generic;

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
            DirectInputManager input = new DirectInputManager(stick);

            ScriptSettings data = ScriptSettings.Load(iniFile);
            string guid = stick.Information.ProductGuid.ToString();
            data.SetValue(TwoPlayerMod.ScriptName, TwoPlayerMod.ControllerKey, guid);

            DpadType dpadType = DetermineDpadType(input);
            if (dpadType == (DpadType)3)
            {
                return false;
            }

            if (dpadType == DpadType.Unknown)
            {
                UI.Notify("Unknown Dpad type, controller configuration stopped.");
                return false;
            }
            data.SetValue(guid, DirectInputManager.DpadTypeKey, dpadType.ToString());

            while (input.GetDpadValue() != -1)
            {
                UI.ShowSubtitle("Please let go the Dpad button.");
                Script.Wait(100);
            }

            Script.Wait(1000);

            UI.ShowSubtitle("Determined Dpad type: " + dpadType, 2500);

            Script.Wait(2500);

            foreach (DeviceButton btn in Enum.GetValues(typeof(DeviceButton)))
            {
                if (btn.ToString().ToLower().Contains("dpad") && dpadType == DpadType.DigitalDpad)
                {
                    bool result = ConfigureDigitalDpadButton(btn, data, input, guid);
                    if (!result)
                    {
                        return false;
                    }
                }
                else
                {
                    bool result = Configure(btn, data, input, guid);
                    if (!result)
                    {
                        return false;
                    }
                }

                UI.Notify(GetBtnText(btn) + " button configured.");
            }

            data.Save();
            return true;
        }

        /// <summary>
        /// Small wizard to determine what kind of Dpad the controller belonging to the InputManager has
        /// </summary>
        /// <param name="input">InputManager which to determine the Dpad of</param>
        /// <returns>DpadType enum</returns>
        private DpadType DetermineDpadType(DirectInputManager input)
        {
            while (input.GetPressedButton() == -1 && input.GetDpadValue() == -1)
            {
                if (Game.IsKeyPressed(System.Windows.Forms.Keys.Escape)) return (DpadType)3;
                UI.ShowSubtitle("Press and hold at least one Dpad button for 1 second. Press the Esc key to cancel.", 120);
                Script.Wait(100);
            }

            UI.ShowSubtitle("Now keep holding that Dpad button.");
            Script.Wait(1000);

            int button = input.GetPressedButton();
            int digitalDpadvalue = input.GetDpadValue();

            if (digitalDpadvalue != -1)
            {
                return DpadType.DigitalDpad;
            }
            else if (button != -1)
            {
                return DpadType.ButtonsDpad;
            }
            return DpadType.Unknown;
        }

        /// <summary>
        /// Helper method to configure a single DeviceButton and add it the configuration
        /// </summary>
        /// <param name="btn">The target DeviceButton</param>
        /// <param name="data">The ScriptSettings object which it needs to be saved too</param>
        /// <param name="input">InputManager object to handle input</param>
        /// <param name="guid">The GUID of the controller</param>
        private bool Configure(DeviceButton btn, ScriptSettings data, DirectInputManager input, string guid)
        {
            while (input.GetPressedButton() == -1)
            {
                if (Game.IsKeyPressed(System.Windows.Forms.Keys.Escape)) return false;
                UI.ShowSubtitle("Press and hold the " + GetBtnText(btn) + " button on the controller for 1 second. Press the Esc key to cancel.", 120);
                Script.Wait(100);
            }

            int button = input.GetPressedButton();
            UI.ShowSubtitle("Please hold the " + GetBtnText(btn) + " button to confirm it.");
            Script.Wait(1000);

            if (button != input.GetPressedButton())
            {
                UI.ShowSubtitle("Now hold the " + GetBtnText(btn) + " button to confirm.");
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

            return true;
        }

        /// <summary>
        /// Helper method to configure a single DeviceButton and add it the configuration
        /// </summary>
        /// <param name="btn">The target DeviceButton</param>
        /// <param name="data">The ScriptSettings object which it needs to be saved too</param>
        /// <param name="input">InputManager object to handle input</param>
        /// <param name="guid">The GUID of the controller</param>
        private bool ConfigureDigitalDpadButton(DeviceButton btn, ScriptSettings data, DirectInputManager input, string guid)
        {
            while (input.GetDpadValue() == -1)
            {
                if (Game.IsKeyPressed(System.Windows.Forms.Keys.Escape)) return false;
                UI.ShowSubtitle("Press and hold the " + GetBtnText(btn) + " button on the controller for 1 second. Press the Esc key to cancel.", 120);
                Script.Wait(100);
            }

            int dpadValue = input.GetDpadValue();
            UI.ShowSubtitle("Please hold the " + GetBtnText(btn) + " button to confirm it.");
            Script.Wait(1000);

            if (dpadValue == -1)
            {
                UI.ShowSubtitle("Now hold the " + GetBtnText(btn) + " button to confirm.");
                Script.Wait(1000);
                ConfigureDigitalDpadButton(btn, data, input, guid);
            }
            else
            {
                data.SetValue(guid, btn.ToString(), dpadValue);
                while (input.GetDpadValue() != -1)
                {
                    UI.ShowSubtitle("Now let go the button to configure the next one.");
                    Script.Wait(100);
                }
                Script.Wait(1000);
            }

            return true;
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
