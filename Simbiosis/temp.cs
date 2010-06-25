using System;

/// <summary>
/// Summary description for Class1
/// </summary>
public class Class1
{

    Pose _startPoseForDriveDistance;
    double _distanceToTravel;
    /// <summary>
    /// Applies constant power to both wheels, driving the motor base for a fixed distance, in the current direction
    /// </summary>
    /// <param name="distance">Distance to travel, in meters</param>
    /// <param name="power">Normalized power (torque) value for both wheels</param>
    public void DriveDistance(float distance, float power)
    {
        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException("distance");
        }
        _startPoseForDriveDistance = State.Pose;
        _distanceToTravel = distance;
        SetAxleVelocity(power * _motorTorqueScaling, power * _motorTorqueScaling);
    }
    /// <summary>
    /// target rotation around Y axis, in radians
    /// </summary>
    double _targetAngle = double.MaxValue;
    /// <summary>
    /// Applies constant power to each wheel (but of inverse polarity), rotating the motor base until the given rotation
    /// </summary>
    /// <param name="degrees">Rotation around Y axis, in degrees. Range is -180 to 180</param>
    /// <param name="power">Normalized power (torque) value for both wheels</param>
    public void RotateDegrees(float degrees, float power)
    {
        degrees = degrees % 360;
        if (degrees >= 180)
        {
            degrees = 180 - degrees;
        }
        xna.Vector3 euler = UIMath.QuaternionToEuler(State.Pose.Orientation);
        // target angle is current euler angle plus degrees specified
        float target = euler.Y + degrees;
        _targetAngle = xna.MathHelper.ToRadians(target);
        if (degrees < 0)
        {
            SetAxleVelocity(power * _motorTorqueScaling, -power * _motorTorqueScaling);
        }
        else
        {
            SetAxleVelocity(-power * _motorTorqueScaling, power * _motorTorqueScaling);
        }
    }
    const float SPEED_DELTA = 0.5f;
    /// <summary>
    /// Current heading, in radians, of robot base
    /// </summary>
    public float CurrentHeading
    {
        get
        {
            // return the axis angle of the quaternion
            xna.Vector3 euler = UIMath.QuaternionToEuler(State.Pose.Orientation);
            return xna.MathHelper.ToRadians(euler.Y); // heading is the rotation about the Y axis.
        }
    }
    /// <summary>
    /// When a direct update to motor torque or wheel velocity occurs
    /// we abandond any current DriveDistance or RotateDegrees commands
    /// </summary>
    void ResetRotationDistance()
    {
        _distanceToTravel = 0;
        _targetAngle = double.MaxValue;
    }
    /// <summary>
    /// Sets motor torque on the active wheels
    /// </summary>
    /// <param name="leftWheel"></param>
    /// <param name="rightWheel"></param>
    public void SetMotorTorque(float leftWheel, float rightWheel)
    {
        ResetRotationDistance();
        SetAxleVelocity(leftWheel * _motorTorqueScaling, rightWheel * _motorTorqueScaling);
    }
    float _leftTargetVelocity;
    float _rightTargetVelocity;
    /// <summary>
    /// Sets angular velocity (radians/sec) on both wheels
    /// </summary>
    /// <param name="value"></param>
    public void SetVelocity(float value)
    {
        ResetRotationDistance();
        SetVelocity(value, value);
    }
    /// <summary>
    /// Sets angular velocity on the wheels
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    public void SetVelocity(float left, float right)
    {
        ResetRotationDistance();
        if (_leftWheel == null || _rightWheel == null)
            return;
        left = ValidateWheelVelocity(left);
        right = ValidateWheelVelocity(right);
        // v is in m/sec - convert to an axle speed
        // 2Pi(V/2PiR) = V/R
        SetAxleVelocity(left / _leftWheel.Wheel.State.Radius,
        right / _rightWheel.Wheel.State.Radius);
    }
    private void SetAxleVelocity(float left, float right)
    {
        _leftTargetVelocity = left;
        _rightTargetVelocity = right;
    }
    const float MAX_VELOCITY = 20.0f;
    const float MIN_VELOCITY = -MAX_VELOCITY;
    float ValidateWheelVelocity(float value)
    {
        if (value > MAX_VELOCITY)
            return MAX_VELOCITY;
        if (value < MIN_VELOCITY)
            return MIN_VELOCITY;
        return value;
    }













}
