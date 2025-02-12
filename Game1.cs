using System;
using EvolutionSim.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Color = Microsoft.Xna.Framework.Color;

namespace EvolutionSim;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly SimulationParameters _parameters;
    private readonly Random _random;
    private Texture2D _filledCircleTexture;

    private Texture2D _plantTexture;
    private MouseState _previousMouseState;
    private Texture2D _reproductionTexture;
    private Creature _selectedCreature;

    private Simulation _simulation;
    private SpriteBatch _spriteBatch;
    private SpriteFont _spriteFont;

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
        _reproductionTexture = CreateCircleTexture(_parameters.Render.CreatureRenderRadius, Color.White);
        _filledCircleTexture = CreateCircleTexture(16, Color.White);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // Handle mouse click to select a creature.
        var currentMouseState = Mouse.GetState();
        if (currentMouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            // Get the mouse click position.
            var mousePos = new Vector2(currentMouseState.X, currentMouseState.Y);

            // Find a creature whose position is close to the click (within 10 pixels).
            foreach (var creature in _simulation.Creatures.Values)
                if (Vector2.Distance(mousePos, creature.Position) < 10f)
                {
                    _selectedCreature = creature;
                    break;
                }
        }

        _previousMouseState = currentMouseState;

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _simulation.Update(dt);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();


        foreach (var creature in _simulation.Creatures.Values)
        {
            var color = Color.Yellow;

            switch (creature.BodyShape)
            {
                case BodyShape.Rod:
                {
                    // Draw the rod as a capsule (a rectangular center with semicircular end caps).
                    // Let W (width) = creature.Size/3 and H (height/length) = creature.Size.
                    var W = creature.Size / 3f;
                    var H = creature.Size;
                    // For a capsule, choose the cap diameter to equal the rod's width.
                    var radius = W / 2f;

                    // Setup transformation: rotate by creature.Heading and translate to creature.Position.
                    var transform = Matrix.CreateRotationZ(creature.Heading + MathHelper.PiOver2) *
                                    Matrix.CreateTranslation(creature.Position.X, creature.Position.Y, 0);

                    _spriteBatch.End();
                    _spriteBatch.Begin(transformMatrix: transform);

                    // Draw the central rectangle (spanning the middle of the capsule).
                    // Its horizontal span is the full width and its vertical span is H - W.
                    _spriteBatch.FillRectangle(
                        new RectangleF(-W / 2f, -H / 2f + radius, W, H - W),
                        color);

                    // Compute scaling for the filled circle texture so that its drawn diameter equals W.
                    var circleScale = W / _filledCircleTexture.Width;
                    var circleOrigin = new Vector2(_filledCircleTexture.Width / 2f, _filledCircleTexture.Height / 2f);

                    // Draw the top cap.
                    _spriteBatch.Draw(_filledCircleTexture,
                        new Vector2(0, -H / 2f + radius),
                        null, color, 0f, circleOrigin, circleScale, SpriteEffects.None, 0f);
                    // Draw the bottom cap.
                    _spriteBatch.Draw(_filledCircleTexture,
                        new Vector2(0, H / 2f - radius),
                        null, color, 0f, circleOrigin, circleScale, SpriteEffects.None, 0f);

                    _spriteBatch.End();
                    _spriteBatch.Begin();
                }
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
                        $"  Hunger: {_selectedCreature.LastSensors.Hunger:F2}\n" +
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