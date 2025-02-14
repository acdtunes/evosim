using System;
using EvolutionSim;

var simParams = SimulationConfigParser.Parse("parameters.yaml");

new Game1(simParams, new Random()).Run();