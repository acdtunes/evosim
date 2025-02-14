using System;

namespace EvolutionSim.Core;

public class Jet(Random random, float jetCooldown, float costMultiplier)
{
    private float _jetTimer = (float)random.NextDouble() * jetCooldown;

    public float LastForce { get; private set; }

    public void Update(float dt, float force)
    {
        _jetTimer -= dt;
        if (_jetTimer <= 0f)
        {
            _jetTimer = jetCooldown;
            LastForce = force;
        }
    }

    public float CalculateEnergyCost(float dt, float energyCostFactor)
    {
        return LastForce * costMultiplier * energyCostFactor * dt;
    }
}