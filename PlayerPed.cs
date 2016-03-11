using Benjamin94.Input;
using GTA;
using GTA.Math;
using GTA.Native;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Benjamin94
{
    /// <summary>
    /// This class will wrap around a normal Ped and will have properties like WeaponIndex, TargetIndex, InputManager 
    /// </summary>
    class PlayerPed
    {
        // static properties
        private static WeaponHash[] weapons = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
        private static WeaponHash[] meleeWeapons = new WeaponHash[] { WeaponHash.PetrolCan, WeaponHash.Knife, WeaponHash.Nightstick, WeaponHash.Hammer, WeaponHash.Bat, WeaponHash.GolfClub, WeaponHash.Crowbar, WeaponHash.Bottle, WeaponHash.SwitchBlade, WeaponHash.Dagger, WeaponHash.Hatchet, WeaponHash.Unarmed, WeaponHash.KnuckleDuster, WeaponHash.Machete, WeaponHash.Flashlight };
        private static WeaponHash[] throwables = new WeaponHash[] { WeaponHash.StickyBomb, WeaponHash.Snowball, WeaponHash.SmokeGrenade, WeaponHash.ProximityMine, WeaponHash.Molotov, WeaponHash.Grenade, WeaponHash.Flare, WeaponHash.BZGas, WeaponHash.Ball };

        // PlayerPed properties
        public Ped Ped { get; internal set; }
        // private Ped ped = null;
        private readonly Ped Player1;
        public readonly UserIndex UserIndex = UserIndex.Any;
        private VehicleAction LastVehicleAction = VehicleAction.Brake;
        private Ped[] Targets = null;

        private int MaxHealth = 100;

        private int weaponIndex = Array.IndexOf(weapons, WeaponHash.Unarmed);

        private int WeaponIndex
        {
            get
            {
                if (weaponIndex < 0)
                {
                    weaponIndex = weapons.Length - 1;
                }
                if (weaponIndex >= weapons.Length)
                {
                    weaponIndex = 0;
                }
                return weaponIndex;
            }
            set
            {
                weaponIndex = value;
            }
        }

        private int targetIndex = 0;
        private int TargetIndex
        {
            get
            {
                if (targetIndex < 0)
                {
                    targetIndex = Targets.Length - 1;
                }
                if (targetIndex >= Targets.Length)
                {
                    targetIndex = 0;
                }
                return targetIndex;
            }
            set
            {
                targetIndex = value;
            }
        }

        /// <summary>
        /// This proprty will return information like health, current weapon...
        /// </summary>
        public string Info
        {
            get
            {
                string info = "";

                info += "Player " + UserIndex + " Weapon: ~n~" + weapons[WeaponIndex];

                return info;
            }
        }

        private Dictionary<PlayerPedAction, int> lastActions = new Dictionary<PlayerPedAction, int>();
        public readonly InputManager Input;
        private readonly PedHash CharacterHash;
        private readonly Color MarkerColor;

        /// <summary>
        /// Constructs a new PlayerPed and sets all required properties
        /// </summary>
        /// <param name="userIndex">The UserIndex of this PlayerPed</param>
        /// <param name="characterHash">The PedHash off this PlayerPed</param>
        /// <param name="blipSprite">Any BlipSprite</param>
        /// <param name="blipColor">Any BlipColor</param>
        /// <param name="player1">The Player 1 Ped</param>
        /// <param name="input">InputManager instance</param>
        public PlayerPed(UserIndex userIndex, PedHash characterHash, BlipSprite blipSprite, BlipColor blipColor, Ped player1, InputManager input)
        {
            UserIndex = userIndex;
            CharacterHash = characterHash;
            Player1 = player1;
            Input = input;

            SetupPed();

            foreach (PlayerPedAction action in Enum.GetValues(typeof(PlayerPedAction)))
            {
                lastActions[action] = Game.GameTime;
            }

            switch (blipColor)
            {
                case BlipColor.White:
                    MarkerColor = Color.White;
                    break;
                case BlipColor.Red:
                    MarkerColor = Color.Red;
                    break;
                case BlipColor.Green:
                    MarkerColor = Color.Green;
                    break;
                case BlipColor.Blue:
                    MarkerColor = Color.Blue;
                    break;
                case BlipColor.Yellow:
                    MarkerColor = Color.Yellow;
                    break;
                default:
                    MarkerColor = Color.OrangeRed;
                    break;
            }

            UpdateBlip(blipSprite, blipColor);
        }

        /// <summary>
        /// This will setup the Ped and set all required properties
        /// </summary>
        private void SetupPed()
        {
            Ped = World.CreatePed(CharacterHash, Player1.Position.Around(5));
            MaxHealth = Ped.Health;

            while (!Ped.Exists())
            {
                Script.Wait(100);
            }

            Ped.Task.ClearAllImmediately();

            Ped.AlwaysKeepTask = true;
            Ped.NeverLeavesGroup = true;
            Ped.CanBeTargetted = true;
            Ped.RelationshipGroup = Player1.RelationshipGroup;
            Ped.CanRagdoll = false;
            Ped.IsEnemy = false;
            Ped.DrownsInWater = false;
            Ped.DiesInstantlyInWater = false;
            Ped.DropsWeaponsOnDeath = false;

            // dont let the playerped decide what to do when there is combat etc.
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, Ped, true);
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, Ped, 0, 0);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, Ped, 46, true);

            foreach (WeaponHash hash in Enum.GetValues(typeof(WeaponHash)))
            {
                try
                {
                    Weapon weapon = Ped.Weapons.Give(hash, int.MaxValue, true, true);
                    weapon.InfiniteAmmo = true;
                    weapon.InfiniteAmmoClip = true;
                }
                catch (Exception)
                {
                }
            }

            SelectWeapon(Ped, weapons[WeaponIndex]);
        }

        /// <summary>
        /// Gets targets for the given player in combat situations
        /// </summary>
        /// <returns>An array of Peds</returns>
        private Ped[] GetTargets()
        {
            return World.GetNearbyPeds(Ped, 50).Where(p => IsValidTarget(p)).OrderBy(p => p.Position.DistanceTo(Ped.Position)).ToArray();
        }

        /// <summary>
        /// Helper method to check if a Ped is a valid target for player 2
        /// </summary>
        /// <param name="target">The target Ped to check</param>
        private bool IsValidTarget(Ped target)
        {
            return target != null && !target.IsPlayerPed() && target != Player1 && target.IsAlive && target.IsOnScreen;
        }

        /// <summary>
        /// Method to select another weapon for a Ped
        /// </summary>
        /// <param name="p">target Ped</param>
        /// <param name="weaponHash">target WeaponHash</param>
        private void SelectWeapon(Ped p, WeaponHash weaponHash)
        {
            WeaponIndex = Array.IndexOf(weapons, weaponHash);

            Function.Call(Hash.SET_CURRENT_PED_WEAPON, p, new InputArgument(weaponHash), true);
            UpdateLastAction(PlayerPedAction.SelectWeapon);
        }

        /// <summary>
        ///  This method will update this PlayerPed blip sprite and color
        /// </summary>
        /// <param name="sprite">new BlipSprite</param>
        /// <param name="color">new BlipColor</param>
        public void UpdateBlip(BlipSprite sprite, BlipColor color)
        {
            Ped.CurrentBlip.Remove();
            Ped.AddBlip().Sprite = sprite;
            Ped.CurrentBlip.Color = color;
        }

        /// <summary>
        /// This method will get called by the Script so this PlayerPed can update its state
        /// </summary>
        public void Tick()
        {
            if (Ped.IsInVehicle())
            {
                UpdateCombat(() => { return Input.isPressed(DeviceButton.LeftShoulder); }, () => { return Input.isPressed(DeviceButton.RightShoulder); });

                Vehicle v = Ped.CurrentVehicle;

                if (v.GetPedOnSeat(VehicleSeat.Driver) == Ped)
                {
                    VehicleAction action = GetVehicleAction(v);
                    if (action != LastVehicleAction)
                    {
                        LastVehicleAction = action;

                        PerformVehicleAction(Ped, v, action);
                    }
                }

                if (Input.isPressed(DeviceButton.X))
                {
                    if (CanDoAction(PlayerPedAction.SelectWeapon, 500))
                    {
                        if (UpdateWeaponIndex())
                        {
                            SelectWeapon(Ped, weapons[WeaponIndex]);
                        }
                    }
                    NotifyWeapon();
                }
            }
            else
            {
                UpdateFoot();
            }

            // for entering / leaving a vehicle
            if (Input.isPressed(DeviceButton.Y))
            {
                if (Ped.IsInVehicle())
                {
                    Vehicle v = Ped.CurrentVehicle;
                    if (v.Speed > 7f)
                    {
                        //4160 = ped is throwing himself out, even when the vehicle is still (that's what the speed check is for)
                        Function.Call(Hash.TASK_LEAVE_VEHICLE, Ped, v, 4160);
                    }
                    else
                    {
                        Ped.Task.LeaveVehicle();
                    }
                }
                else
                {
                    TwoPlayerMod.HandleEnterVehicle(Ped);
                }
            }

            if (Ped.IsDead)
            {
                UI.Notify("Player " + "~g~" + UserIndex + "~w~ press ~g~" + DeviceButton.Back + "~w~ to respawn~w~.");
                if (Input.isPressed(DeviceButton.Back))
                {
                    Respawn();
                }
            }

            if (!Ped.IsNearEntity(Player1, new Vector3(20, 20, 20)))
            {
                UI.Notify("Player " + "~g~" + UserIndex + "~w~ press ~g~" + DeviceButton.Back + "~w~ to go back to player ~g~" + UserIndex.One + "~w~.");
                if (Input.isPressed(DeviceButton.Back))
                {
                    Ped.Position = Player1.Position.Around(5);
                }
            }
        }

        /// <summary>
        /// This method will respawn this PlayerPed
        /// </summary>
        public void Respawn()
        {
            if (Ped != null)
            {
                Ped.Delete();
            }
            SetupPed();
        }

        /// <summary>
        /// Determines the needed action corresponding to current controller input, e.g. VehicleAction.RevEngine
        /// </summary>
        /// <returns>A VehicleAction enum</returns>
        private VehicleAction GetVehicleAction(Vehicle v)
        {
            Direction dir = Input.GetDirection(DeviceButton.LeftStick);
            if (Input.isPressed(DeviceButton.A))
            {
                if (Input.IsDirectionLeft(dir))
                {
                    return VehicleAction.HandBrakeLeft;
                }
                else if (Input.IsDirectionRight(dir))
                {
                    return VehicleAction.HandBrakeRight;
                }
                else
                {
                    return VehicleAction.HandBrakeStraight;
                }
            }

            if (Input.isPressed(DeviceButton.RightTrigger))
            {
                if (Input.IsDirectionLeft(dir))
                {
                    return VehicleAction.GoForwardLeft;
                }
                else if (Input.IsDirectionRight(dir))
                {
                    return VehicleAction.GoForwardRight;
                }
                else
                {
                    return VehicleAction.GoForwardStraightFast;
                }
            }

            if (Input.isPressed(DeviceButton.LeftTrigger))
            {
                if (Input.IsDirectionLeft(dir))
                {
                    return VehicleAction.ReverseLeft;
                }
                else if (Input.IsDirectionRight(dir))
                {
                    return VehicleAction.ReverseRight;
                }
                else
                {
                    return VehicleAction.ReverseStraight;
                }
            }

            if (Input.IsDirectionLeft(dir))
            {
                return VehicleAction.SwerveLeft;
            }
            else if (Input.IsDirectionRight(dir))
            {
                return VehicleAction.SwerveRight;
            }

            return VehicleAction.RevEngine;
        }

        /// <summary>
        /// Helper method to check if a WeaponHash is a throwable like Grenade or Molotov
        /// </summary>
        /// <param name="hash">WeaponHash to check</param>
        /// <returns>true if the given WeaponHash  is throwable, false otherwise</returns>
        private bool IsThrowable(WeaponHash hash)
        {
            return throwables.Contains(hash);
        }

        /// <summary>
        /// Helper method to check if a WeaponHash is a melee weapon
        /// </summary>
        /// <param name="hash">WeaponHash to check</param>
        /// <returns>true if the given WeaponHash  is melee, false otherwise</returns>
        private bool IsMelee(WeaponHash hash)
        {
            return meleeWeapons.Contains(hash);
        }

        /// <summary>
        /// Cleans up this PlayerPed
        /// </summary>
        public void Clean()
        {
            if (Ped != null)
            {
                Ped.Delete();
                Ped = null;
            }
            if (Input != null)
            {
                Input.Cleanup();
            }
        }

        /// <summary>
        /// Calls the TASK_VEHICLE_TEMP_ACTION native in order to control a Vehicle
        /// </summary>
        /// <param name="ped">The driver</param>
        /// <param name="vehicle">The target Vehicle</param>
        /// <param name="action">Any of enum VehicleAction</param>
        private void PerformVehicleAction(Ped ped, Vehicle vehicle, VehicleAction action)
        {
            Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, ped, vehicle, (int)action, -1);
        }

        private bool resetWalking = false;

        /// <summary>
        /// Updates all kind of on foot actions like walking entering exiting vehicles
        /// </summary>
        private void UpdateFoot()
        {
            UpdateCombat(() => { return Input.isPressed(DeviceButton.LeftTrigger); }, () => { return Input.isPressed(DeviceButton.RightTrigger); });

            Vector2 leftThumb = Input.GetState().LeftThumbStick;

            if (leftThumb != Vector2.Zero)
            {
                if (Input.isPressed(DeviceButton.A))
                {
                    // needed for running
                    leftThumb *= 10;
                }
                Vector3 dest = Vector3.Zero;
                if (TwoPlayerMod.customCamera)
                {
                    dest = Ped.Position - new Vector3(leftThumb.X, leftThumb.Y, 0);
                }
                else
                {
                    dest = Ped.GetOffsetInWorldCoords(new Vector3(leftThumb.X, leftThumb.Y, 0));
                }

                Ped.Task.RunTo(dest, true, -1);
                resetWalking = true;
            }
            else if (resetWalking)
            {
                Ped.Task.ClearAll();
                resetWalking = false;
            }

            if (Input.isPressed(DeviceButton.X) && CanDoAction(PlayerPedAction.Jump, 850))
            {
                Ped.Task.Climb();
                UpdateLastAction(PlayerPedAction.Jump);
            }

            if (Input.isPressed(DeviceButton.LeftShoulder))
            {
                if (CanDoAction(PlayerPedAction.SelectWeapon, 500))
                {
                    if (UpdateWeaponIndex())
                    {
                        SelectWeapon(Ped, weapons[WeaponIndex]);
                    }
                }
                NotifyWeapon();
            }
        }

        /// <summary>
        /// Helper method to show the current weapon and show the user what to do to change the weapon
        /// </summary>
        private void NotifyWeapon()
        {
            UI.ShowSubtitle("Player 2 weapon: ~g~" + weapons[WeaponIndex] + "~w~. Use ~g~" + (Ped.IsInVehicle() ? DeviceButton.LeftStick : DeviceButton.RightStick) + "~w~ to select weapons.");
        }

        /// <summary>
        /// This will fire at the targeted ped and will handle changing targets
        /// </summary>
        /// <param name="firstButton">The first (aiming) button which needs to pressed before handling this update</param>
        /// <param name="secondButton">The second (firing) button which needs to pressed before the actual shooting</param>
        private void UpdateCombat(Func<bool> firstButton, Func<bool> secondButton)
        {
            if (Input.isPressed(DeviceButton.DPadLeft))
            {
                foreach (WeaponHash projectile in throwables)
                {
                    Function.Call(Hash.EXPLODE_PROJECTILES, Ped, new InputArgument(projectile), true);
                }
            }
            if (firstButton.Invoke())
            {
                if (!secondButton.Invoke())
                {
                    Targets = GetTargets();
                }
                if (CanDoAction(PlayerPedAction.SelectTarget, 500))
                {
                    Direction dir = Input.GetDirection(DeviceButton.RightStick);
                    if (Input.IsDirectionLeft(dir))
                    {
                        TargetIndex--;
                        UpdateLastAction(PlayerPedAction.SelectTarget);
                    }
                    if (Input.IsDirectionRight(dir))
                    {
                        TargetIndex++;
                        UpdateLastAction(PlayerPedAction.SelectTarget);
                    }
                }

                Ped target = Targets.ElementAtOrDefault(TargetIndex);

                if (target == null)
                {
                    return;
                }

                if (!target.IsAlive)
                {
                    Targets = GetTargets();
                }

                if (target != null)
                {
                    World.DrawMarker(MarkerType.UpsideDownCone, target.GetBoneCoord(Bone.SKEL_Head) + new Vector3(0, 0, 1), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(1, 1, 1), Color.OrangeRed);

                    if (secondButton.Invoke())
                    {
                        SelectWeapon(Ped, weapons[WeaponIndex]);

                        if (IsThrowable(weapons[WeaponIndex]))
                        {
                            if (CanDoAction(PlayerPedAction.ThrowTrowable, 1500))
                            {
                                Function.Call(Hash.TASK_THROW_PROJECTILE, Ped, target.Position.X, target.Position.Y, target.Position.Z);
                                UpdateLastAction(PlayerPedAction.ThrowTrowable);
                            }
                        }
                        else if (CanDoAction(PlayerPedAction.Shoot, 750))
                        {
                            if (Ped.IsInVehicle())
                            {
                                Function.Call(Hash.TASK_DRIVE_BY, Ped, target, 0, 0, 0, 0, 50.0f, 100, 1, (uint)FiringPattern.FullAuto);
                            }
                            else if (IsMelee(weapons[WeaponIndex]))
                            {
                                // Ped.Task.ShootAt(target, 750, FiringPattern.FullAuto);
                                // UI.ShowSubtitle("Melee weapons are not supported yet.");
                                Ped.Task.FightAgainst(target, -1);

                            }
                            else
                            {
                                Ped.Task.ShootAt(target, 750, FiringPattern.FullAuto);
                            }

                            UpdateLastAction(PlayerPedAction.Shoot);
                        }
                    }
                    else
                    {
                        Ped.Task.AimAt(target, 100);
                    }
                }

                if (Ped.IsOnScreen)
                {
                    Vector3 headPos = Ped.GetBoneCoord(Bone.SKEL_Head);
                    headPos.Z += 0.3f;
                    headPos.X += 0.1f;

                    UIRectangle rect = new UIRectangle(UI.WorldToScreen(headPos), new Size(MaxHealth / 2, 5), Color.LimeGreen);
                    rect.Draw();
                    rect.Size = new Size(Ped.Health / 2, 5);
                    rect.Color = Color.IndianRed;
                    rect.Draw();
                }
            }
            else
            {
                TargetIndex = 0;
            }
        }

        /// <summary>
        /// Updates the selection of weapon for Player 2
        /// </summary>
        /// <returns>true if weaponIndex changed, false otherwise</returns>
        private bool UpdateWeaponIndex()
        {
            Direction dir = Input.GetDirection(Ped.IsInVehicle() ? DeviceButton.LeftStick : DeviceButton.RightStick);

            if (Input.IsDirectionLeft(dir))
            {
                WeaponIndex--;
                return true;
            }
            if (Input.IsDirectionRight(dir))
            {
                WeaponIndex++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper method to determine if player 2 is allowed to do the given PlayerPedAction
        /// </summary>
        /// <param name="action">PlayerPedAction to check</param>
        /// <param name="time">minimal time that has to be past before true</param>
        /// <returns>true if time since last PlayerPedAction is more than given time, false otherwise</returns>
        private bool CanDoAction(PlayerPedAction action, int time)
        {
            return Game.GameTime - lastActions[action] >= time;
        }

        /// <summary>
        /// Updates the time of the given PlayerPedAction
        /// </summary>
        /// <param name="action">Target PlayerPedAction</param>
        private void UpdateLastAction(PlayerPedAction action)
        {
            lastActions[action] = Game.GameTime;
        }
    }
}