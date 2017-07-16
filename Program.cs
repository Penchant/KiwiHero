namespace Kiwi_HERO
{
    using System;
    using System.Threading;
    using Microsoft.SPOT;
    using System.Text;
    using CTRE;

    /// <summary>
    /// Program to drive Kiwi in regular mode or can do field oriented if Pidgeon IMU is attached 
    /// </summary>
    public class Program
    {
        /* create a talon */
        static CTRE.TalonSrx motorA = new CTRE.TalonSrx(1);
        static CTRE.TalonSrx motorB = new CTRE.TalonSrx(2);
        static CTRE.TalonSrx motorC = new CTRE.TalonSrx(3);
        static CTRE.TalonSrx.NeutralMode motorAInitialNeutralMode = GetActiveNeutralMode(motorA);
        static CTRE.TalonSrx.NeutralMode motorBInitialNeutralMode = GetActiveNeutralMode(motorB);
        static CTRE.TalonSrx.NeutralMode motorCInitialNeutralMode = GetActiveNeutralMode(motorC);

        static StringBuilder stringBuilder = new StringBuilder();
        static CTRE.Gamepad _gamepad = null;
        struct ButtonPress { public bool now; public bool last; public bool WasPressed() { return now && !last; } }

        public static float Orientation { get; set; } = 0;
        public static bool EStop { get; set; } = false;
        public static PigeonImu Pidgy { get; protected set; } = new CTRE.PigeonImu(0);
        private static ButtonPress[] Buttons { get; set; } = new ButtonPress[20];
        public static bool FieldOriented1 { get; set; } = false;

        public static void Main()
        {
            CTRE.UsbHostDevice.GetInstance().SetSelectableXInputFilter(CTRE.UsbHostDevice.SelectableXInputFilter.BothDInputAndXInput);
            /* loop forever */
            while (true)
            {
                /* get buttons */
                for (uint i = 1; i < 20; ++i)
                {
                    Buttons[i].last = Buttons[i].now;
                    Buttons[i].now = _gamepad.GetButton(i);
                }

                /* if button one was pressed, toggles field oriented*/
                FieldOriented1 = Buttons[0].WasPressed() ? !FieldOriented1 : FieldOriented1;

                /* kills drivetrain if button 2 is pressed or held */
                if (Buttons[1].now)
                {
                    motorA.Set(0);
                    motorB.Set(0);
                    motorC.Set(0);
                    motorA.ConfigNeutralMode(CTRE.TalonSrx.NeutralMode.Brake);
                    motorB.ConfigNeutralMode(CTRE.TalonSrx.NeutralMode.Brake);
                    motorC.ConfigNeutralMode(CTRE.TalonSrx.NeutralMode.Brake);
                    EStop = true;
                }

                /* turns off estop mode if button 10 was pressed*/
                if (Buttons[9].WasPressed())
                {
                    EStop = false;
                    motorA.ConfigNeutralMode(motorAInitialNeutralMode);
                    motorB.ConfigNeutralMode(motorBInitialNeutralMode);
                    motorC.ConfigNeutralMode(motorCInitialNeutralMode);
                    
                }

                /* resets yaw if button six was pressed*/
                if (Buttons[5].WasPressed())
                {
                    try
                    {
                        Pidgy.SetYaw(0);
                    }
                    catch (Exception) { }
                }

                /* adjust orientation by 120 degrees if button three was pressed*/
                Orientation += Buttons[2].WasPressed() ? (float)System.Math.PI * 2 / 3 : 0;

                /*adjust orientation back by 120 degrees if button four was pressed*/
                Orientation -= Buttons[3].WasPressed() ? (float)System.Math.PI * 2 / 3 : 0;

                /* adjust orientation by 180 degrees if button five was pressed*/
                Orientation += Buttons[4].WasPressed() ? (float)System.Math.PI : 0;

                /* drive robot using gamepad */
                Drive();
                /* print whatever is in our string builder */
                Debug.Print(stringBuilder.ToString());
                stringBuilder.Clear();
                /* feed watchdog to keep Talon's enabled */
                CTRE.Watchdog.Feed();
                /* run this task every 20ms */
                Thread.Sleep(20);
            }
        }
        /**
         * If value is within 10% of center, clear it.
         * @param value [out] floating point value to deadband.
         */
        static void Deadband(ref float value, float deadband)
        {
            if (Abs(value) > deadband)
            {
                /* outside of deadband */
            }
            else
            {
                /* within 10% so zero it */
                value = 0;
            }
        }

        /// <summary>
        /// Drives Kiwi around 
        /// </summary>
        static void Drive()
        {
            if (EStop) { return; }

            float aSpeed, bSpeed, cSpeed;
            if (null == _gamepad)
                _gamepad = new CTRE.Gamepad(CTRE.UsbHostDevice.GetInstance());

            float x = _gamepad.GetAxis(0);
            float y = -1 * _gamepad.GetAxis(1);
            float twist = _gamepad.GetAxis(2);

            float adjustmentAngle = 0;

            if (FieldOriented1)
            {
                TryFieldOriented(out adjustmentAngle);
            }
            else
            {
                adjustmentAngle = Orientation;
            }

            //Always using radial coordinates allows for easy orientation adjustment outside of field oriented
            float magnitude = (float)System.Math.Sqrt(x * x + y * y),
                  directionRadians = (float)System.Math.Atan(x / y);

            x = magnitude * (float)System.Math.Cos(directionRadians - adjustmentAngle);
            y = magnitude * (float)System.Math.Sin(directionRadians - adjustmentAngle);

            Deadband(ref x, .05f);
            Deadband(ref y, .05f);
            Deadband(ref twist, .05f);

            aSpeed = x / 2 - (float)System.Math.Sqrt(3) / 2 * y + twist / (2);
            bSpeed = x / 2 + (float)System.Math.Sqrt(3) / 2 * y + twist / (2);
            cSpeed = x * -1 + twist / 2;

            Deadband(ref aSpeed, .07f);
            Deadband(ref bSpeed, .07f);
            Deadband(ref cSpeed, .07f);

            motorA.Set(aSpeed);
            motorB.Set(bSpeed);
            motorC.Set(cSpeed);

            stringBuilder.Append("\t");
            stringBuilder.Append(x);
            stringBuilder.Append("\t");
            stringBuilder.Append(y);
            stringBuilder.Append("\t");
            stringBuilder.Append(twist);

        }

        /// <summary>
        /// Modifies joystick inputs for field oriented driving
        /// </summary>
        /// <param name="gyroAngle">Current angle of gyro</param>
        static float FieldOriented(float gyroAngle)
        {
            return gyroAngle * 3.14f / 180 + 3.14159f / 2;
        }

        /// <summary>
        /// Attempts to call the FieldOriented function, fails if Pidgeon is not connected
        /// </summary>
        /// <param name="x">X axis</param>
        /// <param name="y">Y axis</param>
        /// <returns>True if succeeded, else false</returns>
        static bool TryFieldOriented(out float adjustmentAngle)
        {

            float[] ypr = new float[3];
            try
            {
                Pidgy.GetYawPitchRoll(ypr);
            }
            catch (Exception)
            {
                adjustmentAngle = 0;
                return false;
            }
          
            adjustmentAngle = FieldOriented( ypr[0]);
            return true;
        }

        /// <summary>
        /// Normalizes speeds of motors if they were going to be larger than 1 in either direction
        /// </summary>
        /// <param name="aSpeed">Speed for motorA</param>
        /// <param name="bSpeed">Speed for motorB</param>
        /// <param name="cSpeed">Speed for motorC</param>
        static void Normalize(ref float aSpeed, ref float bSpeed, ref float cSpeed)
        {
            float max = (float)System.Math.Max(Abs(aSpeed), System.Math.Max(Abs(bSpeed), Abs(cSpeed)));
            if (max > 1)
            {
                aSpeed = aSpeed / max;
                bSpeed = bSpeed / max;
                cSpeed = cSpeed / max;
            }
        }

        /// <summary>
        /// Gets the active neutral mode of a motor
        /// </summary>
        /// <param name="motor">Motor to be checked</param>
        /// <returns>The active neutral mode of a motor</returns>
        static CTRE.TalonSrx.NeutralMode GetActiveNeutralMode(CTRE.TalonSrx motor)
        {
            return motor.GetBrakeEnableDuringNeutral() ? CTRE.TalonSrx.NeutralMode.Brake : CTRE.TalonSrx.NeutralMode.Coast;
        }

        /// <summary>
        /// Wrapper of System.Math.Abs that returns a float instead of a double
        /// </summary>
        /// <param name="num">Number</param>
        /// <returns>The absolute value of the number</returns>
        public static float Abs(double num)
        {
            return (float)System.Math.Abs(num);
        }
    }
}