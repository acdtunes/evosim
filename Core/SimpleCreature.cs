using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class SimpleCreature(Vector2 position, float size, float mass, Random random, Simulation simulation)
    : Creature(position, size, mass, random, simulation);