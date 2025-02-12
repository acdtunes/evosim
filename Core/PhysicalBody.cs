using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class PhysicalBody
{
    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; } = Vector2.Zero;
    public float Heading { get; private set; }
    public float AngularVelocity { get; private set; }
    public float Mass { get; }
    public BodyShape Shape { get; }

    // World boundaries from parameters
    private float WorldWidth { get; }
    private float WorldHeight { get; }

    // Creature size (used for torque calculation)
    private readonly float _size;

    // Accumulated forces (reset every update)
    private float _inputThrust;
    private float _inputTorque;

    // Hardcoded scaling constants
    private const float FORCE_SCALING = 100000f;

    // Cooldown constants for burst-like movement
    private const float JET_COOLDOWN = 3f; // seconds
    private static readonly Random
        _globalRandom = new Random();

    // New code: each jet starts with a random cooldown timer to ensure desynchronization.
    private float _frontJetCooldownTimer = 0f;
    private float _backJetCooldownTimer = 0f;
    private float _topRightJetCooldownTimer = 0f;
    private float _topLeftJetCooldownTimer = 0f;
    private float _bottomRightJetCooldownTimer = 0f;
    private float _bottomLeftJetCooldownTimer = 0f;

    // Constructor (simplified)
    public PhysicalBody(Vector2 position, float heading, float mass, float size, BodyShape shape, SimulationParameters parameters)
    {
        Position = position;
        Heading = heading;
        Mass = mass;
        this._size = size;
        Shape = shape;
        WorldWidth = parameters.World.WorldWidth;
        WorldHeight = parameters.World.WorldHeight;
        // Initialize jet cooldown timers with random offsets for desynchronization.
        _frontJetCooldownTimer = (float)_globalRandom.NextDouble() * JET_COOLDOWN;
        _backJetCooldownTimer = (float)_globalRandom.NextDouble() * JET_COOLDOWN;
        _topRightJetCooldownTimer = (float)_globalRandom.NextDouble() * JET_COOLDOWN;
        _topLeftJetCooldownTimer = (float)_globalRandom.NextDouble() * JET_COOLDOWN;
        _bottomRightJetCooldownTimer = (float)_globalRandom.NextDouble() * JET_COOLDOWN;
        _bottomLeftJetCooldownTimer = (float)_globalRandom.NextDouble() * JET_COOLDOWN;
    }

    public void Update(float dt)
    {
        // Decrease individual cooldown timers over time.
        _frontJetCooldownTimer = Math.Max(0, _frontJetCooldownTimer - dt);
        _backJetCooldownTimer = Math.Max(0, _backJetCooldownTimer - dt);
        _topRightJetCooldownTimer = Math.Max(0, _topRightJetCooldownTimer - dt);
        _topLeftJetCooldownTimer = Math.Max(0, _topLeftJetCooldownTimer - dt);
        _bottomRightJetCooldownTimer = Math.Max(0, _bottomRightJetCooldownTimer - dt);
        _bottomLeftJetCooldownTimer = Math.Max(0, _bottomLeftJetCooldownTimer - dt);

        // Integrate the current state using RK4.
        var (newPos, newVel, newHeading, newAngularVel) = RK4Integrate(Position, Velocity, Heading, AngularVelocity, dt);

        // Apply world boundaries (wrap around)
        newPos.X = (newPos.X % WorldWidth + WorldWidth) % WorldWidth;
        newPos.Y = (newPos.Y % WorldHeight + WorldHeight) % WorldHeight;

        Position = newPos;
        Velocity = newVel;
        Heading = newHeading % MathHelper.TwoPi;
        AngularVelocity = newAngularVel;

        // Reset applied forces after integration.
        _inputThrust = 0;
        _inputTorque = 0;
    }

    /// <summary>
    /// Applies jet forces only if the cooldown timer has expired.
    /// The jets are only allowed to fire if their activation exceeds a minimal threshold.
    /// </summary>
    public void ApplyJetForces(JetForces forces)
    {
        // Minimal activation threshold to avoid jitter.
        const float minActivation = 0.01f;

        // --- Linear Thrust ---
        // Check individual cooldown for the back jet.
        float backThrust = (forces.Back >= minActivation && _backJetCooldownTimer <= 0f)
            ? forces.Back * FORCE_SCALING
            : 0f;
        if (backThrust > 0f)
            _backJetCooldownTimer = JET_COOLDOWN;

        // Check individual cooldown for the front jet.
        float frontThrust = (forces.Front >= minActivation && _frontJetCooldownTimer <= 0f)
            ? forces.Front * FORCE_SCALING
            : 0f;
        if (frontThrust > 0f)
            _frontJetCooldownTimer = JET_COOLDOWN;

        // The net thrust (positive means forward; negative means backward).
        float netThrust = backThrust - frontThrust;

        // --- Rotational (Torque) Forces ---
        // Check individual cooldowns for turning jets.
        
        var torqueScaling = FORCE_SCALING / 2;
        float topRightForce = (forces.TopRight >= minActivation && _topRightJetCooldownTimer <= 0f)
            ? forces.TopRight * torqueScaling
            : 0f;
        if (topRightForce > 0f)
            _topRightJetCooldownTimer = JET_COOLDOWN;

        float topLeftForce = (forces.TopLeft >= minActivation && _topLeftJetCooldownTimer <= 0f)
            ? forces.TopLeft * torqueScaling
            : 0f;
        if (topLeftForce > 0f)
            _topLeftJetCooldownTimer = JET_COOLDOWN;

        float bottomRightForce = (forces.BottomRight >= minActivation && _bottomRightJetCooldownTimer <= 0f)
            ? forces.BottomRight * torqueScaling
            : 0f;
        if (bottomRightForce > 0f)
            _bottomRightJetCooldownTimer = JET_COOLDOWN;

        float bottomLeftForce = (forces.BottomLeft >= minActivation && _bottomLeftJetCooldownTimer <= 0f)
            ? forces.BottomLeft * torqueScaling
            : 0f;
        if (bottomLeftForce > 0f)
            _bottomLeftJetCooldownTimer = JET_COOLDOWN;

        // For a rectangular body, compute half-dimensions.
        float halfWidth = _size / 2f;
        float halfHeight = _size / 2f;

        // Local positions of turning jets.
        Vector2 topRightPos = new Vector2(halfWidth, -halfHeight);
        Vector2 topLeftPos = new Vector2(-halfWidth, -halfHeight);
        Vector2 bottomRightPos = new Vector2(halfWidth, halfHeight);
        Vector2 bottomLeftPos = new Vector2(-halfWidth, halfHeight);

        // Calculate forces applied perpendicular to the offset.
        Vector2 forceTopRight = GetPerpendicularForce(topRightPos, topRightForce);
        Vector2 forceTopLeft = GetPerpendicularForce(topLeftPos, topLeftForce);
        Vector2 forceBottomRight = GetPerpendicularForce(bottomRightPos, bottomRightForce);
        Vector2 forceBottomLeft = GetPerpendicularForce(bottomLeftPos, bottomLeftForce);

        // Compute torque using the cross product (offset × force).
        float torqueTopRight = topRightPos.Cross(forceTopRight);
        float torqueTopLeft = topLeftPos.Cross(forceTopLeft);
        float torqueBottomRight = bottomRightPos.Cross(forceBottomRight);
        float torqueBottomLeft = bottomLeftPos.Cross(forceBottomLeft);

        float netTorque = torqueTopRight + torqueTopLeft + torqueBottomRight + torqueBottomLeft;

        // Accumulate the computed forces.
        _inputThrust += netThrust;
        _inputTorque += netTorque;
    }

    // Helper method to compute a perpendicular force.
    private Vector2 GetPerpendicularForce(Vector2 offset, float magnitude)
    {
        // Compute a vector perpendicular to the offset.
        Vector2 perpendicular = new Vector2(-offset.Y, offset.X);
        if (perpendicular != Vector2.Zero)
            perpendicular.Normalize();
        return perpendicular * magnitude;
    }

    // RK4 integration and derivatives methods remain the same.
    private (Vector2, Vector2, float, float) RK4Integrate(Vector2 pos, Vector2 vel, float heading, float angularVel, float dt)
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

        Vector2 newPos = pos + dt / 6f * (k1Pos + 2f * k2Pos + 2f * k3Pos + k4Pos);
        Vector2 newVel = vel + dt / 6f * (k1Vel + 2f * k2Vel + 2f * k3Vel + k4Vel);
        float newHeading = heading + dt / 6f * (k1Heading + 2f * k2Heading + 2f * k3Heading + k4Heading);
        float newAngular = angularVel + dt / 6f * (k1Angular + 2f * k2Angular + 2f * k3Angular + k4Angular);

        return (newPos, newVel, newHeading, newAngular);
    }

    private (Vector2, Vector2, float, float) Derivatives(Vector2 pos, Vector2 vel, float heading, float angularVel)
    {
        Vector2 posDerivative = vel;
        Vector2 thrustDirection = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading));
        Vector2 thrust = thrustDirection * _inputThrust;

        var linearDragCoefficient = 0.2f;
        Vector2 linearDrag = - linearDragCoefficient * vel * vel.Length();
        var angularDragCoefficient = 40;
        float angularDrag = -Math.Sign(angularVel) *  angularDragCoefficient * angularVel * angularVel;

        Vector2 acceleration = (thrust + linearDrag) / Mass;
        float angularAcceleration = (_inputTorque + angularDrag) / GetMomentOfInertia();

        float headingDerivative = angularVel;
        return (posDerivative, acceleration, headingDerivative, angularAcceleration);
    }

    private float GetMomentOfInertia()
    {
        return Shape switch
        {
            BodyShape.Cylinder => 0.5f * Mass * _size * _size,
            BodyShape.Sphere => 0.4f * Mass * _size * _size,
            BodyShape.Rod => (1f / 12f) * Mass * _size * _size,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}