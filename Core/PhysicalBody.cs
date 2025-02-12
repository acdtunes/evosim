using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class PhysicalBody
{
    private readonly float _angularDragCoefficient;

    private readonly float _forceScaling;
    private readonly float _jetCooldown;
    private readonly float _linearDragCoefficient;
    private readonly Random _random;

    private readonly float _size;
    private float _backJetCooldownTimer;
    private float _bottomLeftJetCooldownTimer;
    private float _bottomRightJetCooldownTimer;

    private float _frontJetCooldownTimer;

    private float _inputThrust;
    private float _inputTorque;
    private float _topLeftJetCooldownTimer;
    private float _topRightJetCooldownTimer;

    public PhysicalBody(Vector2 position, float heading, float mass, float size, BodyShape shape, Random random,
        SimulationParameters parameters)
    {
        Position = position;
        Heading = heading;
        Mass = mass;
        _size = size;
        _random = random;
        Shape = shape;
        WorldWidth = parameters.World.WorldWidth;
        WorldHeight = parameters.World.WorldHeight;

        _forceScaling = parameters.Physics.ForceScaling;
        _linearDragCoefficient = parameters.Physics.LinearDragCoefficient;
        _angularDragCoefficient = parameters.Physics.AngularDragCoefficient;
        _jetCooldown = parameters.Physics.JetCooldown;

        _frontJetCooldownTimer = (float)_random.NextDouble() * _jetCooldown;
        _backJetCooldownTimer = (float)_random.NextDouble() * _jetCooldown;
        _topRightJetCooldownTimer = (float)_random.NextDouble() * _jetCooldown;
        _topLeftJetCooldownTimer = (float)_random.NextDouble() * _jetCooldown;
        _bottomRightJetCooldownTimer = (float)_random.NextDouble() * _jetCooldown;
        _bottomLeftJetCooldownTimer = (float)_random.NextDouble() * _jetCooldown;
    }

    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; } = Vector2.Zero;
    public float Heading { get; private set; }
    public float AngularVelocity { get; private set; }
    public float Mass { get; }
    public BodyShape Shape { get; }

    private float WorldWidth { get; }
    private float WorldHeight { get; }

    public void Update(float dt)
    {
        _frontJetCooldownTimer = Math.Max(0, _frontJetCooldownTimer - dt);
        _backJetCooldownTimer = Math.Max(0, _backJetCooldownTimer - dt);
        _topRightJetCooldownTimer = Math.Max(0, _topRightJetCooldownTimer - dt);
        _topLeftJetCooldownTimer = Math.Max(0, _topLeftJetCooldownTimer - dt);
        _bottomRightJetCooldownTimer = Math.Max(0, _bottomRightJetCooldownTimer - dt);
        _bottomLeftJetCooldownTimer = Math.Max(0, _bottomLeftJetCooldownTimer - dt);

        var (newPos, newVel, newHeading, newAngularVel) =
            Rk4Integrate(Position, Velocity, Heading, AngularVelocity, dt);

        newPos.X = (newPos.X % WorldWidth + WorldWidth) % WorldWidth;
        newPos.Y = (newPos.Y % WorldHeight + WorldHeight) % WorldHeight;

        Position = newPos;
        Velocity = newVel;
        Heading = newHeading % MathHelper.TwoPi;
        AngularVelocity = newAngularVel;

        _inputThrust = 0;
        _inputTorque = 0;
    }

    public void ApplyJetForces(JetForces forces)
    {
        const float minActivation = 0.01f;

        var backThrust = forces.Back >= minActivation && _backJetCooldownTimer <= 0f
            ? forces.Back * _forceScaling
            : 0f;
        if (backThrust > 0f)
            _backJetCooldownTimer = _jetCooldown;

        var frontThrust = forces.Front >= minActivation && _frontJetCooldownTimer <= 0f
            ? forces.Front * _forceScaling
            : 0f;
        if (frontThrust > 0f)
            _frontJetCooldownTimer = _jetCooldown;

        var netThrust = backThrust - frontThrust;

        var torqueScaling = _forceScaling / 2;
        var topRightForce = forces.TopRight >= minActivation && _topRightJetCooldownTimer <= 0f
            ? forces.TopRight * torqueScaling
            : 0f;
        if (topRightForce > 0f)
            _topRightJetCooldownTimer = _jetCooldown;

        var topLeftForce = forces.TopLeft >= minActivation && _topLeftJetCooldownTimer <= 0f
            ? forces.TopLeft * torqueScaling
            : 0f;
        if (topLeftForce > 0f)
            _topLeftJetCooldownTimer = _jetCooldown;

        var bottomRightForce = forces.BottomRight >= minActivation && _bottomRightJetCooldownTimer <= 0f
            ? forces.BottomRight * torqueScaling
            : 0f;
        if (bottomRightForce > 0f)
            _bottomRightJetCooldownTimer = _jetCooldown;

        var bottomLeftForce = forces.BottomLeft >= minActivation && _bottomLeftJetCooldownTimer <= 0f
            ? forces.BottomLeft * torqueScaling
            : 0f;
        if (bottomLeftForce > 0f)
            _bottomLeftJetCooldownTimer = _jetCooldown;

        var halfWidth = _size / 2f;
        var halfHeight = _size / 2f;

        var topRightPos = new Vector2(halfWidth, -halfHeight);
        var topLeftPos = new Vector2(-halfWidth, -halfHeight);
        var bottomRightPos = new Vector2(halfWidth, halfHeight);
        var bottomLeftPos = new Vector2(-halfWidth, halfHeight);

        var forceTopRight = GetPerpendicularForce(topRightPos, topRightForce);
        var forceTopLeft = GetPerpendicularForce(topLeftPos, topLeftForce);
        var forceBottomRight = GetPerpendicularForce(bottomRightPos, bottomRightForce);
        var forceBottomLeft = GetPerpendicularForce(bottomLeftPos, bottomLeftForce);

        var torqueTopRight = topRightPos.Cross(forceTopRight);
        var torqueTopLeft = -topLeftPos.Cross(forceTopLeft);
        var torqueBottomRight = -bottomRightPos.Cross(forceBottomRight);
        var torqueBottomLeft = bottomLeftPos.Cross(forceBottomLeft);

        var netTorque = torqueTopRight + torqueTopLeft + torqueBottomRight + torqueBottomLeft;

        _inputTorque += netTorque;
        _inputThrust += netThrust;
    }

    private Vector2 GetPerpendicularForce(Vector2 offset, float magnitude)
    {
        var perpendicular = new Vector2(-offset.Y, offset.X);
        if (perpendicular != Vector2.Zero)
            perpendicular.Normalize();
        return perpendicular * magnitude;
    }

    private (Vector2, Vector2, float, float) Rk4Integrate(Vector2 pos, Vector2 vel, float heading, float angularVel,
        float dt)
    {
        var (k1Pos, k1Vel, k1Heading, k1Angular) = Derivatives(pos, vel, heading, angularVel);
        var (k2Pos, k2Vel, k2Heading, k2Angular) = Derivatives(
            pos + 0.5f * dt * k1Pos,
            vel + 0.5f * dt * k1Vel,
            heading + 0.5f * dt * k1Heading,
            angularVel + 0.5f * dt * k1Angular
        );
        var (k3Pos, k3Vel, k3Heading, k3Angular) = Derivatives(
            pos + 0.5f * dt * k2Pos,
            vel + 0.5f * dt * k2Vel,
            heading + 0.5f * dt * k2Heading,
            angularVel + 0.5f * dt * k2Angular
        );
        var (k4Pos, k4Vel, k4Heading, k4Angular) = Derivatives(
            pos + dt * k3Pos,
            vel + dt * k3Vel,
            heading + dt * k3Heading,
            angularVel + dt * k3Angular
        );

        var newPos = pos + dt / 6f * (k1Pos + 2f * k2Pos + 2f * k3Pos + k4Pos);
        var newVel = vel + dt / 6f * (k1Vel + 2f * k2Vel + 2f * k3Vel + k4Vel);
        var newHeading = heading + dt / 6f * (k1Heading + 2f * k2Heading + 2f * k3Heading + k4Heading);
        var newAngular = angularVel + dt / 6f * (k1Angular + 2f * k2Angular + 2f * k3Angular + k4Angular);

        return (newPos, newVel, newHeading, newAngular);
    }

    private (Vector2, Vector2, float, float) Derivatives(Vector2 pos, Vector2 vel, float heading, float angularVel)
    {
        var posDerivative = vel;
        var thrustDirection = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading));
        var thrust = thrustDirection * _inputThrust;

        var linearDrag = -_linearDragCoefficient * vel * vel.Length();
        var angularDrag = -Math.Sign(angularVel) * _angularDragCoefficient * angularVel * angularVel;

        var acceleration = (thrust + linearDrag) / Mass;
        var angularAcceleration = (_inputTorque + angularDrag) / GetMomentOfInertia();

        var headingDerivative = angularVel;
        return (posDerivative, acceleration, headingDerivative, angularAcceleration);
    }

    private float GetMomentOfInertia()
    {
        return Shape switch
        {
            BodyShape.Cylinder => 0.5f * Mass * _size * _size,
            BodyShape.Sphere => 0.4f * Mass * _size * _size,
            BodyShape.Rod => 1f / 12f * Mass * _size * _size,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}