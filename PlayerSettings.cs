using GTA;
using SharpDX.XInput;
using System;

namespace Benjamin94
{
    class PlayerSettings
    {
        private static ScriptSettings settings = ScriptSettings.Load("scripts//" + TwoPlayerMod.ScriptName + ".ini");

        private const string PlayerKey = "Player";

        public static void SetValue(UserIndex player, string key, string value)
        {
            settings.SetValue(PlayerKey + player, key, value);
            settings.Save();
            settings = ScriptSettings.Load("scripts//" + TwoPlayerMod.ScriptName + ".ini");
        }

        public static string GetValue(UserIndex player, string key, string defaultValue)
        {
            try
            {
                string value = settings.GetValue(PlayerKey + player, key, defaultValue);
                if (string.IsNullOrEmpty(value))
                {
                    value = defaultValue;
                }
                return value;
            }
            catch (Exception)
            {
            }
            return defaultValue;
        }

        public static TEnum GetEnumValue<TEnum>(UserIndex player, string key, string defaultValue)
        {
            try
            {
                string value = GetValue(player, key, defaultValue);
                return (TEnum)Enum.Parse(typeof(TEnum), value);
            }
            catch (Exception)
            {
                return (TEnum)Enum.Parse(typeof(TEnum), defaultValue);
            }
        }
    }
}