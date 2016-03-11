using GTA;

namespace Benjamin94
{
    public static class PedExtensions
    {

        /// <summary>
        /// Helper method to check if the given Ped is a PlayerPed
        /// </summary>
        /// <param name="ped">Target Ped to check</param>
        /// <returns>true or false whether the Ped is a PlayerPed</returns>
        public static bool IsPlayerPed(this Ped ped)
        {
            if (TwoPlayerMod.player1 == ped)
            {
                return true;
            }
            foreach (PlayerPed playerPed in TwoPlayerMod.playerPeds)
            {
                if (playerPed.Ped == ped)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
