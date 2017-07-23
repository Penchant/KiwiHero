# KiwiHero
Port of [4607's Kiwi Code](https://github.com/FRC4607/KiwiSwag) to C# for the CTRE's [HERO board](http://www.ctr-electronics.com/control-system/hro.html)  

Supports "field oriented" mode where regardless of which direction the robot is facing forward on the joystick will have the robot go in the same direction, compared to normal mode where forward is forward relative to the front of the robot. The direction considered forward is 90 degrees counter-clockwise to yaw reading from the gyro. Also includes the ability to reset the yaw to 0 to then adjust what is forward for "field oriented" mode.  

Depends on the Pigeon IMU from CTRE for a gyro to be able to use field oriented.

Currently does not support using the FRC Driverstation with it, but does include buttons to disable and enable the robot, where disabling it sets the motor speed values to 0 and if the motor controllers are not already in brake mode, then it puts them in brake mode. Enabling allows use of the motors again and resets neutral behavior to the original settings.

Because a Kiwi drive has no clear front and back, supports shifting what is presently considered the front by 120 degrees in either direction as well as flipping the orientation 180 degrees.
