using Benjamin94.Input;
using GTA;
using GTA.Math;
using GTA.Native;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
//using System.Timers;

namespace Benjamin94
{
    /// <summary>
    /// This class will wrap around a normal Ped and will have properties like WeaponIndex, TargetIndex, InputManager 
    /// </summary>
    class PlayerPed
    {
        // static properties
        //private static WeaponHash[] weapons = (WeaponHash[])Enum.GetValues(typeof(WeaponHash));
        private static WeaponHash[] meleeWeapons = new WeaponHash[] { WeaponHash.PetrolCan, WeaponHash.Knife, WeaponHash.Nightstick, WeaponHash.Hammer, WeaponHash.Bat, WeaponHash.GolfClub, WeaponHash.Crowbar, WeaponHash.Bottle, WeaponHash.SwitchBlade, WeaponHash.Dagger, WeaponHash.Hatchet, WeaponHash.Unarmed, WeaponHash.KnuckleDuster, WeaponHash.Machete, WeaponHash.Flashlight };
        private static WeaponHash[] throwables = new WeaponHash[] { WeaponHash.StickyBomb, WeaponHash.Snowball, WeaponHash.SmokeGrenade, WeaponHash.ProximityMine, WeaponHash.Molotov, WeaponHash.Grenade, WeaponHash.Flare, WeaponHash.BZGas, WeaponHash.Ball };
        private static WeaponHash[] weaponset = new WeaponHash[] { WeaponHash.Unarmed, WeaponHash.MicroSMG, WeaponHash.SpecialCarbine, WeaponHash.SniperRifle, WeaponHash.HomingLauncher, WeaponHash.Minigun, WeaponHash.StickyBomb };
        private static WeaponHash[] weaponchain = new WeaponHash[] { WeaponHash.MicroSMG, WeaponHash.SpecialCarbine, WeaponHash.CombatMG, WeaponHash.HomingLauncher, WeaponHash.RPG };
        private static WeaponHash[] unavailable = { WeaponHash.Grenade, WeaponHash.Molotov, WeaponHash.SmokeGrenade, WeaponHash.ProximityMine, WeaponHash.StickyBomb, WeaponHash.Bat, WeaponHash.Crowbar, WeaponHash.GolfClub, WeaponHash.Hammer, WeaponHash.Hatchet, WeaponHash.Knife, WeaponHash.Dagger, WeaponHash.Nightstick, WeaponHash.PetrolCan, WeaponHash.Snowball };

        // PlayerPed properties
        public Ped Ped { get; internal set; }
        // private Ped ped = null;
        private readonly Ped Player1;
        public readonly UserIndex UserIndex = UserIndex.Any;
        private VehicleAction LastVehicleAction = VehicleAction.Brake;
        private Ped[] Targets = null;

        private int MaxHealth = 100;
        //private float normalize;

        private WeaponHash[] currentset = weaponset;
        private int weaponIndex = 0;
        private Ped[] instTarget = null;
        //private int weaponIndex = Array.IndexOf(weaponset, WeaponHash.Unarmed);

        
        private float speed = 0;
        private int drivecounter = 0;
        //private int brakecounter = 0;
        private int followcounter = 0;
        private Vector3 cardest = Vector3.Zero;
        private Vector3 oldcardest = Vector3.Zero;
        



        private Vector2 prevState = Vector2.Zero;
        private Vector3 prevDest = Vector3.Zero;
        private Vector3 dest = Vector3.Zero;
        private Vector2 prevAim = Vector2.Zero;
        private Vector3 aimoffset = Vector3.Zero;
        private Vector3 aim = Vector3.Zero;
        private Vector3 lasersight = Vector3.Zero;
        private Vector3 instaim = Vector3.Zero;

        private DateTime _drive = new DateTime();
        private DateTime carflip = new DateTime();


        private int WeaponIndex
        {
            get
            {
                if (weaponIndex < 0)
                {
                    weaponIndex = currentset.Length - 1;
                }
                if (weaponIndex >= currentset.Length)
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
        //public string Info
        //{
        //    get
        //    {
        //        string info = "";

        //        info += "Player " + UserIndex + " Weapon: ~n~" + currentset[WeaponIndex];

        //        return info;
        //    }
        //}

        private Dictionary<PlayerPedAction, int> lastActions = new Dictionary<PlayerPedAction, int>();
        public readonly InputManager Input;
        private readonly PedHash CharacterHash;
        private readonly Color MarkerColor;
        private readonly BlipColor blipcolor;

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
            blipcolor = blipColor;

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
            //Function.Call(Hash.CLONE_PED, Game.Player.Character, Game.Player.Character.Heading);
            //Ped = World.GetNearbyPeds(Player1, 10)[0];
            
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
            Ped.IsInvincible = true;
            Ped.CanFlyThroughWindscreen = true;
            Ped.Armor = 50;

            // dont let the playerped decide what to do when there is combat etc.
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, Ped, true);
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, Ped, 0, 0);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, Ped, 46, true);

            //Function.Call(Hash.SET_AI_WEAPON_DAMAGE_MODIFIER, 100);

            foreach (WeaponHash hash in Enum.GetValues(typeof(WeaponHash)))
            {
                try
                {
                    Weapon weapon = Ped.Weapons.Give(hash, int.MaxValue, true, true);
                    weapon.InfiniteAmmo = true;
                    weapon.InfiniteAmmoClip = true;
                    if (hash == WeaponHash.SniperRifle) weapon.SetComponent(WeaponComponent.AtArSupp02, true);
                }
                catch (Exception)
                {
                }
            }
            // add silencer
           //Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, Ped, 100416529, 0xA73D4664);

            SelectWeapon(Ped, currentset[WeaponIndex]);
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
        //private bool IsValid(Ped target)
        //{
        //    return target != null;
        //}

        /// <summary>
        /// Method to select another weapon for a Ped
        /// </summary>
        /// <param name="p">target Ped</param>
        /// <param name="weaponHash">target WeaponHash</param>
        private void SelectWeapon(Ped p, WeaponHash weaponHash)
        {
            WeaponIndex = Array.IndexOf(currentset, weaponHash);

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

        //public void drivingTimer(int delay, Vehicle v)
        //{
        //    Timer timer = new Timer();
        //    timer.Interval = delay;
        //    timer.Elapsed += (sender, args) => updatedrive(sender, v);
        //}

        private void updatedrive(Vehicle v)
        {

            //drivecounter += 1;
            //if (drivecounter < 8) return;
            if (DateTime.Now < _drive) return;
            _drive = DateTime.Now + TimeSpan.FromMilliseconds(250);

            //float steering = v.SteeringAngle;
            
            Vector2 direction = Input.GetState().LeftThumbStick;
            //get speed
            //joystick limit calibration
            // allow max throttle if only y

            //drag
            if ((float)Math.Abs(direction.Y) > 0.995f)
                speed = (float)Math.Abs(direction.Y * 100.0f);
            //fast 30
            else if ((float)Math.Abs(direction.Y) > 0.9f)
                speed = (float)Math.Abs(direction.Y * 30.0f);
            //normal 30
            else if ((float)Math.Abs(direction.Y) > 0.6f)
                speed = (float)Math.Abs(direction.Y * 30.0f);
            else if ((float)Math.Abs(direction.Y) > 0.5f)
                speed = (float)Math.Abs(direction.Y * 15.0f);
            //else if ((float)Math.Abs(direction.Y) < 0.05f)
            //    speed = 0f;
            else if ((float)Math.Abs(direction.Y) < 0.33f)
                speed = 10f;
            // slow cruise
            else if ((float)Math.Abs(direction.Y) < 0.1f)
                speed = 5f;
            
                //else
                //{
                //    speed = 10.0f;
                //}


                if (direction == Vector2.Zero) prevState = direction;
            
            if (direction != Vector2.Zero)
            {

                // handbrake attempt
                //if (direction.X >= 0.995f && (float)Math.Abs(direction.Y) <= 0.2)
                //{
                //    Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, v, 5, -1);
                //    cardest = v.GetOffsetInWorldCoords(new Vector3(0, 200, 0));
                //    return;
                //}
                //else if (direction.X <= -0.995f && (float)Math.Abs(direction.Y) <= 0.2)
                //{
                //    Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, v, 4, -1);
                //    cardest = v.GetOffsetInWorldCoords(new Vector3(0, 200, 0));
                //    return;
                //}

                //check if want to turn
                if ((float)Math.Abs(direction.X) < 0.05)
                {
                    cardest = v.GetOffsetInWorldCoords(new Vector3(0, 200, 0));
                }
                    

                //joystick in same position
                //else if ((float)Math.Abs(direction.X - prevState.X) < 0.02f && (float)Math.Abs(direction.Y - prevState.Y) < 0.02f)
                //{
                //    prevDest = v.Rotation;
                //    v.Rotation = NC_Get_Cam_Rotation();

                //    cardest = v.GetOffsetInWorldCoords(new Vector3(0, 1000, 0));
                //    v.Rotation = prevDest;
                //    prevState = direction;
                //}

                // get new dest
                else
                {
                    // limit how much you can turn
                    // need more settings
                    // joystick limit calibration 0.55 was .6
                    if (direction.X > 0.6f) direction.X = 0.4f;
                    else if (direction.X > 0.3) direction.X = 0.1f; // dne
                    else if (direction.X > 0.1f) direction.X = 0.05f; // was .1
                    else if (direction.X < -0.6f) direction.X = -0.4f;
                    else if (direction.X < -0.3f) direction.X = -0.1f;
                    else if (direction.X < -0.1f) direction.X = -0.05f;
                    

                    //cardest = Car.GetOffsetInWorldCoords(new Vector3(direction.X, direction.Y, 0));
                    //  steerAngle = (float) Math.Atan(cardest.X / cardest.Y);
                    prevDest = v.Rotation;
                    v.Rotation = new Vector3(NC_Get_Cam_Rotation().X, NC_Get_Cam_Rotation().Y, prevDest.Z);

                    // optimizae!!!!!

                    //UI.ShowSubtitle($"X ROT {NC_Get_Cam_Rotation().X} Y Rot {NC_Get_Cam_Rotation().Y} Z { NC_Get_Cam_Rotation().Z}");
                    cardest = v.GetOffsetInWorldCoords(new Vector3(direction.X * 200.0f, direction.Y * 200.0f, 0));
                    v.Rotation = prevDest;
                    prevState = direction;
                    //steerAngle = Car.SteeringAngle;

                    // cardest = Game.Player.Character.Position;
                    //oldcardest = cardest;


                    //Function.Call(Hash.TASK_VEHICLE_GOTO_NAVMESH, Ped, Car, dest.X, dest.Y, dest.Z, 10.0f, 4194304, 3f);
                    // Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, Car, 23, Math.Atan(cardest.X/cardest.Y));

                }
            }
            //if (Input.isPressed(DeviceButton.RightTrigger)) Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, Ped, v, cardest.X, cardest.Y, 0, 20f, 1f, v.Model.Hash, 16777216, 1f, true);

            // allow correction after interval
            UI.ShowSubtitle($" X {direction.X} Ang {v.SteeringAngle} AC {v.Acceleration} ................................ RPM {v.Speed} Gear {v.CurrentGear}");
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, Ped, v, cardest.X, cardest.Y, 0, speed, 1f, v.Model.Hash, 16777216, 1f, true);
            


            //UI.ShowSubtitle($"Input {direction.X} {direction.Y}  Car dest {cardest.X} {cardest.Y} {cardest.Z} {speed} ");
            //World.DrawMarker(MarkerType.UpsideDownCone, target.GetBoneCoord(Bone.SKEL_Head) + new Vector3(0, 0, 1), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(1, 1, 1), Color.OrangeRed)
            //World.DrawMarker(MarkerType.UpsideDownCone, new Vector3(cardest.X, cardest.Y, Ped.Position.Z), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(1, 1, 1), Color.Blue);
            //brakecounter = 0;
            //drivecounter = 0;
        }

        /// <summary>
        /// This method will get called by the Script so this PlayerPed can update its state
        /// </summary>
        public void Tick()
        {

            if (Ped.IsInVehicle())
            {
                if (TwoPlayerMod.customCamera)
                {
                    UpdateCombat(() => { return Input.isPressed(DeviceButton.LeftShoulder); }, () => { return Input.isPressed(DeviceButton.RightShoulder); });
                }
                //else
                //{
                //    World.DrawMarker(MarkerType.UpsideDownCone, new Vector3(cardest.X, cardest.Y, Ped.Position.Z), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(4, 4, 4), Color.Blue);
                //    UpdateCombat(() => { return Input.isPressed(DeviceButton.LeftShoulder); }, () => { return Input.isPressed(DeviceButton.RightShoulder); });
                //}

                Vehicle v = Ped.CurrentVehicle;

                if (v.GetPedOnSeat(VehicleSeat.Driver) == Ped)
                {
                    //if (TwoPlayerMod.customCamera)
                    //{
                    //VehicleAction action = TwoPlayerMod.customCamera ? GetVehicleAction(v) : drivingTimer(1000, v);
                    VehicleAction action = GetVehicleAction(v);
                    if (action != LastVehicleAction && TwoPlayerMod.customCamera)
                    {
                        LastVehicleAction = action;

                        PerformVehicleAction(Ped, v, action);
                    }
                    else if (!TwoPlayerMod.customCamera && (Input.isPressed(DeviceButton.RightShoulder)) ) updatedrive(v);

                    //straight override
                    if (Input.isPressed(DeviceButton.A) && !TwoPlayerMod.customCamera)
                    {
                        Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, v, 32,-1);
                    }
                    // brake
                    if (Input.isPressed(DeviceButton.LeftShoulder) && !TwoPlayerMod.customCamera)
                    {
                        Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, v, 1, -1);
                    }
                    if (Input.isPressed(DeviceButton.LeftTrigger) && !TwoPlayerMod.customCamera)
                    {
                        Direction dir = Input.GetDirection(DeviceButton.LeftStick);
                        if (Input.IsDirectionLeft(dir))
                        {
                            PerformVehicleAction(Ped, v, VehicleAction.ReverseLeft);
                        }
                        else if (Input.IsDirectionRight(dir))
                        {
                            PerformVehicleAction(Ped, v, VehicleAction.ReverseRight);
                        }
                        else
                        {
                            PerformVehicleAction(Ped, v, VehicleAction.BrakeReverseFast);
                        }
                        // brakecounter += 1;
                        //Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, v, 28, -1);
                        //PerformVehicleAction(Ped, v, VehicleAction.ReverseStraight);
                        //if (brakecounter > 10)
                        //{
                        //    brakecounter = 0;
                        //    PerformVehicleAction(Ped, v, VehicleAction.ReverseStraight);
                        //}
                        //else if (brakecounter > 5)
                        //{ 
                        //    Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, v, 1, -1);
                        //}
                        //brakecounter += 1;
                    }
                    if (Input.isPressed(DeviceButton.LeftStick))
                    {
                        //brakecounter = 0;
                        if (followcounter > 5)
                        {
                            speed = (Input.GetState().LeftThumbStick.Y * 40.0f)+20.0f;
                            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, Ped, v, Game.Player.Character.Position.X, Game.Player.Character.Position.Y, 0, speed, 1f, v.Model.Hash, 16777216, 1f, true);
                            followcounter = 0;
                        }
                        else followcounter += 1;
                    }
                    //}
                    //else
                    //{
                    //    DriveCar(v);
                    //    if (action != LastVehicleAction)
                    //    {
                    //        LastVehicleAction = action;

                    //        PerformVehicleAction(Ped, v, action);
                    //    }
                    //}

                }
                else
                {
                    World.DrawMarker(MarkerType.UpsideDownCone, new Vector3(cardest.X, cardest.Y, Ped.Position.Z), GameplayCamera.Direction, GameplayCamera.Rotation, new Vector3(4, 4, 4), Color.Blue);
                    UpdateCombat(() => { return Input.isPressed(DeviceButton.LeftShoulder); }, () => { return Input.isPressed(DeviceButton.RightShoulder); });
                }

                //if (Input.isPressed(DeviceButton.X))
                //{
                //    if (CanDoAction(PlayerPedAction.SelectWeapon, 150))
                //    {
                //        if (UpdateWeaponIndex())
                //        {
                //            SelectWeapon(Ped, currentset[WeaponIndex]);
                //        }
                //    }
                //    NotifyWeapon();
                //}
                if (Input.isPressed(DeviceButton.X))
                {
                    if (DateTime.Now < carflip) return;
                    carflip = DateTime.Now + TimeSpan.FromMilliseconds(1000);
                    v.Rotation = new Vector3(v.Rotation.X, 180, v.Rotation.Z);
                    //v.Rotation = new Vector3(v.Rotation.X, v.Rotation.Y, 180); //pitch
                }
            }
            else
            {
                //drivecounter = 0;
                followcounter = 0;
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

            // reset aim coordinate
            if (Input.isPressed(DeviceButton.RightStick)) aim = Ped.GetOffsetInWorldCoords(new Vector3(0, 1000.0f, 0));

            if (Input.isPressed(DeviceButton.Start)) World.CurrentDayTime = new TimeSpan(8, 0, 0);

            if (Ped.IsDead)
            {
                UI.ShowSubtitle("Player " + "~g~" + UserIndex + "~w~ press ~g~ Select ~w~ to respawn~w~.",1000);   
            }
            if (!Ped.IsInVehicle() && Input.isPressed(DeviceButton.Back))
            {
                Respawn();
            }

            //if (!Ped.IsNearEntity(Player1, new Vector3(20, 20, 20)))
            //{
            //    UI.Notify("Player " + "~g~" + UserIndex + "~w~ press ~g~" + DeviceButton.Back + "~w~ to go back to player ~g~" + UserIndex.One + "~w~.");
            //    if (Input.isPressed(DeviceButton.Back))
            //    {
            //        Ped.Position = Player1.Position.Around(5);
            //    }
            //}
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
            UpdateBlip(BlipSprite.Standard, blipcolor);
            weaponIndex = 0;
            //UI.Notify("Player " + "~g~" + UserIndex + "~g~ respawned.");
        }
        //private VehicleAction DriveCar(Vehicle Car)
        //{
            //Vector2 direction = Input.GetState().LeftThumbStick;
            //if (direction == Vector2.Zero) cardest = Vector3.Zero;
            //if (direction != Vector2.Zero)
            //{
            //    if ((float) Math.Abs(direction.X - prevState.X) < 0.02f && (float) Math.Abs(direction.Y - prevState.Y) < 0.02f)
            //    {

            //    }
            //    else
            //    {
            //        //cardest = Car.GetOffsetInWorldCoords(new Vector3(direction.X, direction.Y, 0));
            //        //  steerAngle = (float) Math.Atan(cardest.X / cardest.Y);
            //        prevDest = Car.Rotation;
            //        Car.Rotation = NC_Get_Cam_Rotation();
            //        cardest = Car.GetOffsetInWorldCoords(new Vector3(direction.X * 1000.0f, direction.Y * 1000.0f, 0));
            //        Car.Rotation = prevDest;
            //        //steerAngle = Car.SteeringAngle;

            //        // cardest = Game.Player.Character.Position;
            //        //oldcardest = cardest;

                    
            //        //Function.Call(Hash.TASK_VEHICLE_GOTO_NAVMESH, Ped, Car, dest.X, dest.Y, dest.Z, 10.0f, 4194304, 3f);
            //       // Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, Car, 23, Math.Atan(cardest.X/cardest.Y));

            //    }
            //}
            //if  (Input.isPressed(DeviceButton.RightTrigger)) Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, Ped, Car, cardest.X, cardest.Y, 0, 10f, 1f, Car.Model.Hash, 16777216, 1f, true);
            ////else Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, Ped, Car, 1, -1);
            //UI.ShowSubtitle($"Car dest {cardest.X} {cardest.Y} {cardest.Z} {Car.SteeringAngle} ");
            //return VehicleAction.Brake;

        //}


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
            UpdateCombat(() => { return Input.isPressed(DeviceButton.LeftShoulder); }, () => { return Input.isPressed(DeviceButton.RightShoulder); });
            Vector2 leftThumb = Input.GetState().LeftThumbStick;

            if (Input.isPressed(DeviceButton.LeftStick))
            {
                prevState = leftThumb;
                dest = Game.Player.Character.Position;
                if (Input.isPressed(DeviceButton.A))
                {
                    //UI.ShowSubtitle($"{Ped.CanFlyThroughWindscreen}");
                    //Function.Call(Hash._WORLD3D_TO_SCREEN2D);
                    //World.
                     
                }                
                else
                {
                    Function.Call(Hash.SET_NEXT_DESIRED_MOVE_STATE, 4);
                    Ped.Task.RunTo(dest, true, -1);
                }
                resetWalking = true;
            }
            else if (leftThumb != Vector2.Zero)
            {
                //if (Input.isPressed(DeviceButton.A))
                //{
                //    // needed for running
                //    leftThumb *= 10;
                //}
                //Vector3 dest = Vector3.Zero;
                if (TwoPlayerMod.customCamera)
                {
                    dest = Ped.Position - new Vector3(leftThumb.X*10, leftThumb.Y*10, 0);
                }
                else
                {
                    // check if joystick in same position
                    if ((float)Math.Abs(leftThumb.X - prevState.X) < 0.02f && (float)Math.Abs(leftThumb.Y - prevState.Y) < 0.02f)
                    {
                        //dest = Ped.GetOffsetInWorldCoords(new Vector3(0,0.2f,0));
                        //UI.ShowSubtitle("no update");
                        // UI.ShowSubtitle($"Auto Input {leftThumb.X} {leftThumb.Y} Dest {dest.X} {dest.Y} {dest.Z} ");
                    }
                    else
                    {
                        // make minor correction
                        //if (resetWalking)
                        //{
                        //    dest = Ped.GetOffsetInWorldCoords(new Vector3(leftThumb.X - prevState.X, leftThumb.Y - prevState.Y, 0));
                        //    prevDest += dest;
                        //    dest = prevDest;
                        //    prevState = leftThumb;
                        //}
                        //// new movement
                        //else
                        //{
                        //Ped.Task.ClearAllImmediately();
                        //dest = Ped.GetOffsetInWorldCoords(new Vector3(leftThumb.X*1000.0f, leftThumb.Y*1000.0f, 0));

                        // faces character forward relative to camera
                        //dest = NC_Get_Cam_Position() + new Vector3(RotToDir(NC_Get_Cam_Rotation()).X*1000.0f*(leftThumb.X*5.0f), RotToDir(NC_Get_Cam_Rotation()).Y*1000.0f*(leftThumb.Y*5.0f), RotToDir(NC_Get_Cam_Rotation()).Z*1000.0f);
                        //dest = NC_Get_Cam_Position() + new Vector3(RotToDir(NC_Get_Cam_Rotation(), leftThumb.X).X * 1000.0f, RotToDir(NC_Get_Cam_Rotation(), leftThumb.Y).Y * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Z * 1000.0f);
                        //dest = NC_Get_Cam_Position() + new Vector3(RotToDir(NC_Get_Cam_Rotation()).X * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Y * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Z * 1000.0f);
                        prevDest = Ped.Rotation;
                        Ped.Rotation = NC_Get_Cam_Rotation();
                        dest = Ped.GetOffsetInWorldCoords(new Vector3(leftThumb.X * 25.0f, leftThumb.Y * 25.0f, 0));
                        Ped.Rotation = prevDest;

                        //dest = Ped.GetOffsetInWorldCoords(dest);
                        //normalize = (float) Math.Sqrt(dest.X*dest.X + dest.Y * dest.Y + dest.Z * dest.Z);
                        //dest.X /= normalize;
                        //dest.Y /= normalize;
                        //dest.Z /= normalize;


                        //Ped.Task.TurnTo(dest);
                        //prevDest = dest;
                        prevState = leftThumb;
                        //UI.ShowSubtitle($"zInput {leftThumb.X} {leftThumb.Y} Dest {dest.X} {dest.Y} {dest.Z} ");
                        //UI.ShowSubtitle($"cam {NC_Get_Cam_Rotation().X} {NC_Get_Cam_Rotation().Y} {NC_Get_Cam_Rotation().Z} ");

                        //}
                    }
                    //leftThumb = Input.GetState().LeftThumbStick;
                    //if (leftThumb == Vector2.Zero)
                    //{
                    //    Ped.Task.ClearAll();
                    //    resetWalking = false;
                    //    prevState = Vector2.Zero;
                    //    return;
                    //}

                    //dest = Ped.GetOffsetInWorldCoords(new Vector3(leftThumb.X, leftThumb.Y, 0));
                    //dest = Ped.GetOffsetInWorldCoords(new Vector3(0, 10, 0));

                    //Direction dir = Input.GetDirection(DeviceButton.LeftStick);

                    //if (Input.IsDirectionLeft(dir))
                    //{
                    //    dest = Ped.GetOffsetInWorldCoords(new Vector3(-10, 0, 0));
                    //    Ped.Task.TurnTo(dest);
                    //}
                    //if (Input.IsDirectionRight(dir))
                    //{
                    //    dest = Ped.GetOffsetInWorldCoords(new Vector3(10, 0, 0));
                    //}

                }
                //Ped.Task.RunTo(dest, true, -1);
                if (Input.isPressed(DeviceButton.A))
                {
                    Ped.Task.RunTo(dest, true, -1);
                }
                else if (Input.isPressed(DeviceButton.LeftShoulder))
                {
                    Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Ped.Handle, dest.X, dest.Y,
                                dest.Z, aim.X, aim.Y, aim.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0, (uint)FiringPattern.FullAuto);
                }
                else
                {
                    Ped.Task.GoTo(dest, true, -1);
                }
                resetWalking = true;
            }
            else if (resetWalking)
            {
                Ped.Task.ClearAll();
                resetWalking = false;
                prevState = Vector2.Zero;
            }

            //if (Input.isPressed(DeviceButton.X) && CanDoAction(PlayerPedAction.Jump, 850))
            //{
            //    Ped.Task.Climb();
            //    UpdateLastAction(PlayerPedAction.Jump);
            //}
            if (Input.isPressed(DeviceButton.X))
            {
                Ped.Task.Jump();
                //Function.Call(Hash.CLONE_PED, Game.Player.Character, Game.Player.Character.Heading);
            }
            if (Input.isPressed(DeviceButton.B))
            {
                if (CanDoAction(PlayerPedAction.SelectWeapon, 300))
                {
                    if (UpdateWeaponIndex())
                    {
                        SelectWeapon(Ped, currentset[WeaponIndex]);
                    }
                    NotifyWeapon();
                }
            }
            //if (Input.isPressed(DeviceButton.LeftShoulder))
            //{
            //    if (CanDoAction(PlayerPedAction.SelectWeapon, 500))
            //    {
            //        if (UpdateWeaponIndex())
            //        {
            //            SelectWeapon(Ped, weaponset[WeaponIndex]);
            //        }
            //    }
            //    NotifyWeapon();
            //}
        }

        /// <summary>
        /// Helper method to show the current weapon and show the user what to do to change the weapon
        /// </summary>
        private void NotifyWeapon()
        {
            //UI.ShowSubtitle("Player 2 weapon: ~g~" + weaponset[WeaponIndex] + "~w~. Use ~g~" + (Ped.IsInVehicle() ? DeviceButton.LeftStick : DeviceButton.RightStick) + "~w~ to select weapons.");
            UI.ShowSubtitle("Player weapon: ~g~" + currentset[WeaponIndex]);
        }

        /// <summary>
        /// This will fire at the targeted ped and will handle changing targets
        /// </summary>
        /// <param name="firstButton">The first (aiming) button which needs to pressed before handling this update</param>
        /// <param name="secondButton">The second (firing) button which needs to pressed before the actual shooting</param>
        private void UpdateCombat(Func<bool> firstButton, Func<bool> secondButton)
        {
            if (Input.isPressed(DeviceButton.LeftTrigger) && !Ped.IsInVehicle())
            {
                foreach (WeaponHash projectile in throwables)
                {
                    Function.Call(Hash.EXPLODE_PROJECTILES, Ped, new InputArgument(projectile), true);
                }
            }
            // change weaponset
            if (Input.isPressed(DeviceButton.RightTrigger) && !Ped.IsInVehicle())
            {
                weaponIndex = 0;
                if (currentset == weaponset)
                {
                    currentset = weaponchain;
                    UI.ShowSubtitle("Chain set selected ", 1000);
                }
                else
                {
                    currentset = weaponset;
                    UI.ShowSubtitle("Default set selected ", 1000);
                }
            }

            if (TwoPlayerMod.customCamera)
            {
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
                            SelectWeapon(Ped, currentset[WeaponIndex]);

                            if (IsThrowable(currentset[WeaponIndex]))
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
                                else if (IsMelee(currentset[WeaponIndex]))
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
            else
            {
                if (Ped.IsInVehicle() && Ped.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) != Ped)
                {
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
                                SelectWeapon(Ped, currentset[WeaponIndex]);

                                if (IsThrowable(currentset[WeaponIndex]))
                                {
                                    if (CanDoAction(PlayerPedAction.ThrowTrowable, 1500))
                                    {
                                        Function.Call(Hash.TASK_THROW_PROJECTILE, Ped, target.Position.X, target.Position.Y, target.Position.Z);
                                        UpdateLastAction(PlayerPedAction.ThrowTrowable);
                                    }
                                }
                                else if (CanDoAction(PlayerPedAction.Shoot, 750))
                                {
                                    //if (Ped.IsInVehicle())
                                    //{
                                        Function.Call(Hash.TASK_DRIVE_BY, Ped, target, 0, 0, 0, 0, 50.0f, 100, 1, (uint)FiringPattern.FullAuto);
                                    //}
                                    

                                  //  UpdateLastAction(PlayerPedAction.Shoot);
                                }
                            }
                        }
                    }
                    else
                    {
                        TargetIndex = 0;
                    }
                }
                else
                {


                    if (firstButton.Invoke())
                    {
                        //if (unavailable.Contains(Ped.Weapons.Current.Hash)) return;
                        SelectWeapon(Ped, currentset[WeaponIndex]);

                        // Draw laser
                        //if (!IsThrowable(currentset[WeaponIndex]) && !IsMelee(currentset[WeaponIndex]))
                        //{
                        //lasersight = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Ped).GetOffsetInWorldCoords(new Vector3(1, 1, 1));
                        //lasersight = Ped.GetOffsetInWorldCoords(Ped.Position);
                        //lasersight = Ped.Position;
                        //lasersight.Z = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Ped).GetOffsetInWorldCoords(new Vector3(0, 0, 0)).Z;
                        //Entity gun = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Ped);
                        //Vector3 gunbone = gun.GetOffsetInWorldCoords(new Vector3(0, 0, 0));
                        //Vector3 orig = RotToDir(new Vector3(-gun.Rotation.Y, gun.Rotation.X, -gun.Rotation.Z - 1f));
                        //Vector3 changed = new Vector3(orig.Y, orig.X, orig.Z);
                        // lasersight = gunbone;

                        // laser draw
                        //Function.Call(Hash.DRAW_LINE, lasersight.X, lasersight.Y, lasersight.Z, aim.X, aim.Y, aim.Z, 124, 252, 0, 250);

                        //Vector3 aim = new Vector3(Ped.Position.X, Ped.Position.Y, Ped.Position.Z);
                        //UI.ShowSubtitle($"Position {lasersight.X} {lasersight.Y} {lasersight.Z} Aim {aim.X} {aim.Y} {aim.Z}  ");
                        //}
                        

                        Vector2 rightThumb = Input.GetState().RightThumbStick;
                        //if (rightThumb == Vector2.Zero) aim = Ped.GetOffsetInWorldCoords(new Vector3(0, 10, 0f));
                        //if (Input.isPressed(DeviceButton.RightStick)) aim = Ped.GetOffsetInWorldCoords(new Vector3(0, 10, 0));

                        //dpad aiming
                        if (Input.isPressed(DeviceButton.DPadUp))
                        {
                            // compare to base vector
                            if (aim.Z > (Ped.GetOffsetInWorldCoords(new Vector3(0, 1000.0f, 0)).Z + 600)) return;
                            aim.Z += 5f;
                            Ped.Task.AimAt(aim, 1000);
                            return;
                        }
                        else if (Input.isPressed(DeviceButton.DPadDown))
                        {
                            // compare to base vector
                            if (aim.Z < (Ped.GetOffsetInWorldCoords(new Vector3(0, 1000.0f, 0)).Z - 600)) return;
                            aim.Z -= 5f;
                            Ped.Task.AimAt(aim, 1000);
                            return;
                        }
                        else if (Input.isPressed(DeviceButton.DPadLeft))
                        {
                            // compare to base vector
                            //if (aim.Z < (Ped.GetOffsetInWorldCoords(new Vector3(0, 1000.0f, 0)).Z - 8)) return;
                            aim.X += 2f;
                            aim.Y += 2f;
                            Ped.Task.AimAt(aim, 1000);
                            return;
                        }
                        else if (Input.isPressed(DeviceButton.DPadRight))
                        {
                            // compare to base vector
                            // if (aim.Z < (Ped.GetOffsetInWorldCoords(new Vector3(0, 1000.0f, 0)).Z - 8)) return;
                            aim.X -= 2f;
                            aim.Y -= 2f;
                            Ped.Task.AimAt(aim, 1000);
                            return;
                        }

                        if (rightThumb != Vector2.Zero)
                        {
                            //if ((float)Math.Abs(rightThumb.Y) > 0.99f && (float)Math.Abs(rightThumb.X) < 0.1f)  //z override
                            //{
                            //    aim.Z += rightThumb.Y;
                            //    Ped.Task.AimAt(aim,1000);
                            //    return;
                            //}


                            if ((float)Math.Abs(rightThumb.X - prevAim.X) < 0.02f && (float)Math.Abs(rightThumb.Y - prevAim.Y) < 0.02f) // same pos 0.05
                            {
                                //aim = Ped.GetOffsetInWorldCoords(new Vector3(0, 10, 0));
                            }

                            else
                            {
                                prevAim = rightThumb;
                                aimoffset = Ped.Rotation;
                                Ped.Rotation = NC_Get_Cam_Rotation();
                                aim.X = Ped.GetOffsetInWorldCoords(new Vector3(rightThumb.X * 1000.0f, rightThumb.Y * 1000.0f, 0)).X;
                                aim.Y = Ped.GetOffsetInWorldCoords(new Vector3(rightThumb.X * 1000.0f, rightThumb.Y * 1000.0f, 0)).Y;
                                Ped.Rotation = aimoffset;
                            }

                            //aim += new Vector3(rightThumb.X, rightThumb.Y, 0) * 30;
                            //aim.Z = Ped.GetOffsetInWorldCoords(new Vector3(0, 1, 0)).Z;

                            //UI.ShowSubtitle($"Input {rightThumb.X} {rightThumb.Y} Updated Position {aim.X} {aim.Y} {aim.Z} ");
                        }

                        // check if want to autolock
                        //shitty aimbot
                        if (Input.isPressed(DeviceButton.LeftTrigger))
                        {
                            // check objects
                            //RaycastResult ray = World.Raycast(lasersight, aim, IntersectOptions.Objects);
                            //if (ray.DitHitAnything)
                            //{
                            //    Ped newguy = World.CreatePed(PedHash.Business01AMM, ray.HitCoords);
                            //}
                            // check if map
                            //RaycastResult ray = World.Raycast(lasersight, RotToDir(Ped.Rotation), 10, IntersectOptions.Everything);

                            //{
                            //ray = World.Raycast(lasersight, Ped.ForwardVector, 40, IntersectOptions.Everything);
                            //if (ray.DitHitEntity)
                            //{
                            //    Ped newguy = World.CreatePed(PedHash.Business01AMM, new Vector3(ray.HitCoords.X, ray.HitCoords.Y, ray.HitCoords.Z+5));
                            //    newguy.IsVisible = false;
                            //    UI.Notify("Ped spawmed");
                            //    //Ped[] instaTarget = World.GetNearbyPeds(newguy, 5);
                            //    try
                            //    {
                            //        instTarget = World.GetNearbyPeds(newguy, 10).Where(p => IsValidTarget(p)).OrderBy(p => p.Position.DistanceTo(newguy.Position)).ToArray();

                            //        if (instTarget != null)
                            //        {
                            //            aim = instTarget[0].GetBoneCoord(Bone.SKEL_Head);
                            //            UI.Notify("Ped found");
                            //        }

                            //    }
                            //    catch
                            //    {
                            //        UI.Notify("No results");
                            //    }
                            //    UI.Notify("Ped deleted");
                            //    newguy.Delete();
                            //    //else UI.Notify("Nothing found");
                            //}
                            // else
                            //{
                            Vector3 rayoffset = Ped.GetOffsetInWorldCoords(new Vector3(0, 8, 0));

                            //Function.Call(Hash.GET_GROUND_Z_FOR_3D_COORD, rayoffset.X, rayoffset.Y, rayoffset.Z, );

                            //Ped newguy = World.CreatePed(PedHash.Business01AMM, new Vector3(rayoffset.X, rayoffset.Y, rayoffset.Z));

                            //newguy.IsVisible = false;
                            //UI.Notify("Ped spawmn guess");
                            //Ped[] instaTarget = World.GetNearbyPeds(newguy, 5);
                            // old code
                            //try
                            //{
                            //    Ped newguy = World.CreatePed(PedHash.Business01AMM, new Vector3(rayoffset.X, rayoffset.Y, rayoffset.Z));

                            //    newguy.IsVisible = false;
                            //    instTarget = World.GetNearbyPeds(newguy, 15).Where(p => IsValidTarget(p)).OrderBy(p => p.Position.DistanceTo(newguy.Position)).ToArray();
                            //    //UI.Notify("Ped deleted");
                            //    newguy.Delete();

                            //    if (instTarget != null)
                            //    {
                            //        aim = instTarget[0].GetBoneCoord(Bone.SKEL_Head);
                            //        UI.Notify("Target Acquired");
                            //    }

                            //}
                            //catch
                            //{
                            //    UI.Notify("No results");
                            //}

                            try
                            {
                                instTarget = World.GetNearbyPeds(rayoffset, 15).Where(p => IsValidTarget(p)).OrderBy(p => p.Position.DistanceTo(rayoffset)).ToArray();
                                //UI.Notify("Ped deleted");

                                if (instTarget != null)
                                {
                                    aim = instTarget[0].GetBoneCoord(Bone.SKEL_Head);
                                    UI.Notify("Target Acquired");
                                }

                            }
                            catch
                            {
                                UI.Notify("No results");
                            }
                        }

                        if (secondButton.Invoke())
                        {
                            if (IsThrowable(currentset[WeaponIndex]))
                            {
                                // bind key for quick throw bomb while weapon
                                if (CanDoAction(PlayerPedAction.ThrowTrowable, 1500))
                                {
                                    SelectWeapon(Ped, currentset[WeaponIndex]);
                                    Function.Call(Hash.TASK_THROW_PROJECTILE, Ped, Ped.Position.X, Ped.Position.Y, Ped.Position.Z);
                                    UpdateLastAction(PlayerPedAction.ThrowTrowable);
                                }
                            }
                            else
                            {
                                Ped.Task.ShootAt(aim, 500, FiringPattern.FullAuto);
                            }
                        }
                        else
                        {
                            Ped.Task.AimAt(aim, 500);
                        }

                        //RaycastResult RayCast;
                        //RayCast = this.Cam_Raycast_Forward();

                        //Vector2 rightThumb = Input.GetState().RightThumbStick;
                        //if (rightThumb != Vector2.Zero)
                        //{
                        //    Vector3 aim = RayCast.HitCoords;
                        //    Ped.Task.ShootAt(aim, 750, FiringPattern.FullAuto);
                        //    //aim = new Vector3(rightThumb.X, rightThumb.Y, 0)
                        //}

                        // Draw laser
                        if (!IsThrowable(currentset[WeaponIndex]) && !IsMelee(currentset[WeaponIndex]))
                        {
                            Entity gun = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Ped);
                            Vector3 lasersight = gun.GetOffsetInWorldCoords(new Vector3(0, 0, 0));
                            // laser draw
                            Function.Call(Hash.DRAW_LINE, lasersight.X, lasersight.Y, lasersight.Z, aim.X, aim.Y, aim.Z, 124, 252, 0, 250);
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
                    else if (secondButton.Invoke()) // dont aim, just shoot
                    {
                        instaim.X = Ped.GetOffsetInWorldCoords(new Vector3(0, 8, -0.2f)).X;
                        instaim.Y = Ped.GetOffsetInWorldCoords(new Vector3(0, 8, -0.2f)).Y;

                        //dpad aiming
                        if (Input.isPressed(DeviceButton.DPadUp) && instaim.Z < Ped.GetOffsetInWorldCoords(new Vector3(0, 8, -0.2f)).Z + 5)
                        {
                            instaim.Z += 1f;
                        }
                        else if (Input.isPressed(DeviceButton.DPadDown) && instaim.Z > Ped.GetOffsetInWorldCoords(new Vector3(0, 8, -0.2f)).Z + -5)
                        {
                            instaim.Z -= 1f;
                        }
                        Ped.Task.ShootAt(instaim, 500, FiringPattern.FullAuto);
                    }
                    else if (!secondButton.Invoke()) instaim.Z = Ped.GetOffsetInWorldCoords(new Vector3(0, 8, -0.2f)).Z;
                }
                //else
                //{
                //    TargetIndex = 0;
                //}
            }
        }

        /// <summary>
        /// Updates the selection of weapon for Player 2
        /// </summary>
        /// <returns>true if weaponIndex changed, false otherwise</returns>
        private bool UpdateWeaponIndex()
        {
            WeaponIndex++;
            return true;

            //Direction dir = Input.GetDirection(Ped.IsInVehicle() ? DeviceButton.LeftStick : DeviceButton.RightStick);

            //if (Input.IsDirectionLeft(dir))
            //{
            //    WeaponIndex--;
            //    return true;
            //}
            //if (Input.IsDirectionRight(dir))
            //{
            //    WeaponIndex++;
            //    return true;
            //}
            //return false;
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

        /// <summary>
        /// Helper functions for raycast
        /// </summary>
        //public Vector3 RotToDir(Vector3 Rot)
        //{
        //    try
        //    {
        //        float z = Rot.Z;
        //        float retz = z * 0.0174532924F;//degree to radian
        //        float x = Rot.X;
        //        float retx = x * 0.0174532924F;
        //        float absx = (float)System.Math.Abs(System.Math.Cos(retx));
        //        return new Vector3((float)-System.Math.Sin(retz) * absx, (float)System.Math.Cos(retz) * absx, (float)System.Math.Sin(retx));
        //    }
        //    catch
        //    {
        //        return new Vector3(0, 0, 0);
        //    }

        //}
        // overload
        //public Vector3 RotToDir(Vector3 Rot, float left)
        //{
        //    try
        //    {
        //        float modifier = left * 5.0f;
        //        float z = Rot.Z;
        //        float retz = z * 0.0174532924F;//degree to radian
        //        float x = Rot.X;
        //        float retx = x * 0.0174532924F;
        //        float absx = (float)System.Math.Abs(System.Math.Cos(retx));
        //        Vector3 temp = new Vector3((float)-System.Math.Sin(retz) * absx, (float)System.Math.Cos(retz) * absx, (float)System.Math.Sin(retx));
        //        //if (temp.X < 0) temp.X *= -1f;
        //        //if (temp.Y < 0) temp.Y *= -1f;
        //        temp.X *= modifier;
        //        temp.Y *= modifier;
        //        return temp;
        //    }
        //    catch
        //    {
        //        return new Vector3(0, 0, 0);
        //    }
        //}

        //public Vector3 RotToDirEZ(Vector3 Rotation)
        //{
        //    double rotZ = (Math.PI / 180) * Rotation.Z;
        //    double rotX = (Math.PI / 180) * Rotation.X;
        //    double multiXY = Math.Abs(Convert.ToDouble(Math.Cos(rotX)));
        //    Vector3 res = new Vector3();
        //    res.X = (float)(Convert.ToDouble(-Math.Sin(rotZ)) * multiXY);
        //    res.Y = (float)(Convert.ToDouble(Math.Cos(rotZ)) * multiXY);
        //    res.Z = (float)(Convert.ToDouble(Math.Sin(rotX)));

        //    return res;
        //}

        //public RaycastResult Cam_Raycast_Forward()
        //{
        //    try
        //    {
        //        Vector3 multiplied = new Vector3(RotToDir(NC_Get_Cam_Rotation()).X * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Y * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Z * 1000.0f);
        //        RaycastResult ray = World.Raycast(NC_Get_Cam_Position(), NC_Get_Cam_Position() + multiplied, IntersectOptions.Everything);
        //        //RaycastResult ray = World.Raycast(Ped.ForwardVector, )
        //        return ray;
        //    }
        //    catch
        //    {
        //        return new RaycastResult();
        //    }
        //}

        //public RaycastResult Gun_Raycast_Idle(Entity gun)
        //{
        //    try
        //    {
        //        Vector3 gunbone = gun.GetOffsetInWorldCoords(new Vector3(0, -0.5f, -0.17f));//Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, gun, 0);
        //                                                                                    //Vector3 front = gun.GetOffsetInWorldCoords(new Vector3(0, 1f, -0.17f));
        //                                                                                    //Vector3 back = gun.GetOffsetInWorldCoords(new Vector3(0, 1f, -0.17f));

        //        //Vector3 multiplied = new Vector3(RotToDir(NC_Get_Cam_Rotation()).X * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Y * 1000.0f, RotToDir(NC_Get_Cam_Rotation()).Z * 1000.0f);
        //        //RaycastResult ray = World.Raycast(back, (front - back) * 1000.0f, IntersectOptions.Everything);
        //        //Vector3 orig = RotToDir(new Vector3(-gun.Rotation.Y + 8f, gun.Rotation.X, -gun.Rotation.Z + 14.5f));this is with +0.5f, -0.17
        //        Vector3 orig = RotToDir(new Vector3(-gun.Rotation.Y + 5.5f, gun.Rotation.X, -gun.Rotation.Z - 15f));
        //        Vector3 changed = new Vector3(orig.Y, orig.X, orig.Z);
        //        RaycastResult ray = World.Raycast(gunbone, gunbone + changed * 1000, IntersectOptions.Everything);
        //        return ray;
        //    }
        //    catch
        //    {
        //        return new RaycastResult();
        //    }
        //}

        //public RaycastResult Gun_Raycast_Reloading(Entity gun)
        //{
        //    try
        //    {
        //        Vector3 gunbone = gun.GetOffsetInWorldCoords(new Vector3(0, -0.5f, -0.17f));
        //        Vector3 orig = RotToDir(new Vector3(-gun.Rotation.Y, gun.Rotation.X, -gun.Rotation.Z - 1f));//was -4
        //        Vector3 changed = new Vector3(orig.Y, orig.X, orig.Z);
        //        RaycastResult ray = World.Raycast(gunbone, gunbone + changed * 1000, IntersectOptions.Everything);
        //        return ray;
        //    }
        //    catch
        //    {
        //        return new RaycastResult();
        //    }
        //}

        //public Vector3 NC_Get_Cam_Position()
        //{
        //    try
        //    {
        //        return Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_COORD);
        //    }
        //    catch
        //    {

        //    }
        //    return new Vector3(0, 0, 0);
        //}

        public Vector3 NC_Get_Cam_Rotation()
        {
            try
            {
                return Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_ROT, 0);
            }
            catch
            {

            }
            return new Vector3(0, 0, 0);
        }
    }
}