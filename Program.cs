using System;
using EvolutionSim;
using EvolutionSim.Configuration;
using EvolutionSim.UI;

var simParams = SimulationConfigParser.Parse("Configuration/parameters.yaml");

new Game1(simParams, new Random()).Run();