using System;
using System.Linq;
using EvolutionSim.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Color = Microsoft.Xna.Framework.Color;

namespace EvolutionSim;

public class Game1 : Game
{
    private const float ClusterRegenInterval = 5f;
    private readonly GraphicsDeviceManager _graphics;
    private readonly SimulationParameters _parameters;
    private readonly Random _random;

    private float _clusterRegenTimer;
    private Texture2D _filledCircleTexture = null!;

    private Texture2D _plantTexture = null!;
    private MouseState _previousMouseState;
    private Creature? _selectedCreature;

    private Simulation _simulation = null!;
    private SpriteBatch _spriteBatch = null!;

    public Game1(SimulationParameters parameters, Random random)
    {
        _parameters = parameters;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _random = random;
    }

    protected override void Initialize()
    {
        _simulation = new Simulation(_parameters, _random);

        _graphics.PreferredBackBufferWidth = _parameters.World.WorldWidth;
        _graphics.PreferredBackBufferHeight = _parameters.World.WorldHeight;
        _graphics.ApplyChanges();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _plantTexture = CreateCircleTexture(_parameters.Render.PlantRenderRadius, Color.Green);
        _filledCircleTexture = CreateCircleTexture(16, Color.White);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        HandleMouseClick();

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _simulation.Update(dt);

        _clusterRegenTimer += dt;
        if (_clusterRegenTimer >= ClusterRegenInterval &&
            _simulation.Plants.Count < _parameters.Population.GlobalMaxPlantCount)
        {
            PlantClusterSpawner.SpawnCluster(_simulation, _random);
            _clusterRegenTimer = 0f;
        }

        base.Update(gameTime);
    }

    private void HandleMouseClick()
    {
        var currentMouseState = Mouse.GetState();
        if (currentMouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Vector2(currentMouseState.X, currentMouseState.Y);
            var nearCreatures = _simulation.GetNearbyCreatures(mousePos, 10f);
            _selectedCreature = nearCreatures.FirstOrDefault();
        }

        _previousMouseState = currentMouseState;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();

        foreach (var creature in _simulation.Creatures.Values)
        {
            var color = creature.IsParasite ? Color.Red : Color.Yellow;

            switch (creature.BodyShape)
            {
                case BodyShape.Rod:
                    DrawRodCreature(creature, color);
                    break;
                case BodyShape.Cylinder:
                case BodyShape.Sphere:
                default:
                    _spriteBatch.DrawCircle(creature.Position, creature.Size, 32, color, 2f);
                    break;
            }
        }

        foreach (var plant in _simulation.Plants.Values)
            _spriteBatch.Draw(_plantTexture, plant.Position, Color.White);

        _spriteBatch.End();

        if (_selectedCreature != null)
        {
            var stats = $"Position: {_selectedCreature.Position}\n" +
                        $"Energy: {_selectedCreature.Energy:F2}\n" +
                        $"Sensors:\n" +
                        $"  PlantNorm: {_selectedCreature.LastSensors.PlantNormalizedDistance:F2}, " +
                        $"PlantSin: {_selectedCreature.LastSensors.PlantAngleSin:F2}, " +
                        $"PlantCos: {_selectedCreature.LastSensors.PlantAngleCos:F2}\n" +
                        $"  CreatureNorm: {_selectedCreature.LastSensors.CreatureNormalizedDistance:F2}, " +
                        $"CreatureSin: {_selectedCreature.LastSensors.CreatureAngleSin:F2}, " +
                        $"CreatureCos: {_selectedCreature.LastSensors.CreatureAngleCos:F2}\n" +
                        $"  Energy: {_selectedCreature.LastSensors.Energy:F2}\n" +
                        $"JetForces:\n" +
                        $"  Front: {_selectedCreature.LastJetForces.Front:F2}, " +
                        $"Back: {_selectedCreature.LastJetForces.Back:F2}\n" +
                        $"  TopRight: {_selectedCreature.LastJetForces.TopRight:F2}, " +
                        $"TopLeft: {_selectedCreature.LastJetForces.TopLeft:F2}\n" +
                        $"  BottomRight: {_selectedCreature.LastJetForces.BottomRight:F2}, " +
                        $"BottomLeft: {_selectedCreature.LastJetForces.BottomLeft:F2}";

            Console.Clear();
            Console.WriteLine(stats);
        }

        base.Draw(gameTime);
    }

    private void DrawRodCreature(Creature creature, Color color)
    {
        var width = creature.Size / 3f;
        var height = creature.Size;
        var radius = width / 2f;
        var transform = Matrix.CreateRotationZ(creature.Heading + MathHelper.PiOver2) *
                        Matrix.CreateTranslation(creature.Position.X, creature.Position.Y, 0);

        _spriteBatch.End();
        _spriteBatch.Begin(transformMatrix: transform);

        _spriteBatch.FillRectangle(
            new RectangleF(-width / 2f, -height / 2f + radius, width, height - width),
            color);

        var circleScale = width / _filledCircleTexture.Width;
        var circleOrigin = new Vector2(_filledCircleTexture.Width / 2f, _filledCircleTexture.Height / 2f);

        _spriteBatch.Draw(_filledCircleTexture,
            new Vector2(0, -height / 2f + radius),
            null, color, 0f, circleOrigin, circleScale, SpriteEffects.None, 0f);

        _spriteBatch.Draw(_filledCircleTexture,
            new Vector2(0, height / 2f - radius),
            null, color, 0f, circleOrigin, circleScale, SpriteEffects.None, 0f);

        _spriteBatch.End();
        _spriteBatch.Begin();
    }

    private Texture2D CreateCircleTexture(int radius, Color color)
    {
        var diameter = radius * 2;
        var texture = new Texture2D(GraphicsDevice, diameter, diameter);
        var colorData = new Color[diameter * diameter];
        float radiusSquared = radius * radius;

        for (var x = 0; x < diameter; x++)
        for (var y = 0; y < diameter; y++)
        {
            var index = x + y * diameter;
            var pos = new Vector2(x - radius, y - radius);
            colorData[index] = pos.LengthSquared() <= radiusSquared ? color : Color.Transparent;
        }

        texture.SetData(colorData);
        return texture;
    }
}