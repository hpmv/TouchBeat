using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace SynGame
{
    public struct VertexPositionColorNormal : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector3 Normal;

        public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(sizeof(float) * 3, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(sizeof(float) * 3 + 4, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0)
        );


        VertexDeclaration IVertexType.VertexDeclaration
        {
            get { return VertexDeclaration; }
        }
    }

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        private Fingers fingers;

        private BasicEffect basicEffect;
        private DateTime startingTime;
        private double time;
        private float fallingSpeed = 1.0f;
        private GameSession session;


        private List<FingerTrace> userTraces = new List<FingerTrace>();
        private List<FingerTrace> songTraces = new List<FingerTrace>(); 
        private FingerTrace[] currentUserTraces = new FingerTrace[5];
        private NoteChart chart;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            fingers = new Fingers();
            session = new GameSession();
            chart = new NoteChart();
            chart.parse(new Scanner(File.ReadAllText("test.txt")));
            session.Initialize(chart);
            Song song = Content.Load<Song>("qby");
            MediaPlayer.Play(song);
            startingTime = DateTime.Now;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize() {
            basicEffect = new BasicEffect(graphics.GraphicsDevice);
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = true;
            basicEffect.TextureEnabled = false;
            basicEffect.DirectionalLight0.DiffuseColor = new Vector3(1, 1, 1); // a red light
            basicEffect.DirectionalLight0.Direction = new Vector3(0, 0, -1);  // coming along the x-axis
            basicEffect.DirectionalLight0.SpecularColor = new Vector3(0, 1, 0); // with green highlights

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }


        private float ForceToThickness(float force) {
            if (force < 20) {
                return 0.02f;
            } else if (force < 520) {
                return 0.02f + 0.0001f*(force - 20);
            }
            else {
                return 0.07f + 0.00001f*(force - 520);
            }
        }

        private Color blend(Color a, Color b, float t) {
            return new Color(a.ToVector3() * (1-t) + b.ToVector3() * t);
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime) {
            if (IsActive) {
                int cx = Window.ClientBounds.Width/2;
                int cy = Window.ClientBounds.Height/2;
                Mouse.SetPosition(cx, cy);
            }
            time = (DateTime.Now - startingTime).TotalMinutes;
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            Finger[] f = fingers.getFingers();
            List<float> fingerXs = new List<float>();
            for (int i = 0; i < f.Length; i++) {
                if (f[i].Exists) {
                    if (currentUserTraces[i] == null) {
                        currentUserTraces[i] = new FingerTrace();
                        userTraces.Add(currentUserTraces[i]);
                        if (userTraces.Count > 5) {
                            userTraces.RemoveAt(0);
                        }
                    }
                    float thickness = ForceToThickness(f[i].Force);
                    currentUserTraces[i].points.Add(new CorePoint(new Vector3(f[i].X, (float) (time * chart.bpm), 0),
                        -Vector3.UnitY, Vector3.UnitZ, thickness, new Color(1.0f, 0.0f, 0.0f, 0.3f)));
                    fingerXs.Add(f[i].X);
                }
                else {
                    currentUserTraces[i] = null;
                }
            }

            songTraces =
                GetTraces(session.FeedInstant(fingerXs.ToArray(), time, time + 100/chart.bpm, gameTime.ElapsedGameTime));

            base.Update(gameTime);
        }

        struct CorePoint {
            public Vector3 pos;
            public Vector3 orient;
            public Vector3 up;
            public float radius;
            public Color color;
            public CorePoint(Vector3 pos, Vector3 orient, Vector3 up, float radius, Color color) {
                this.pos = pos;
                this.orient = orient;
                this.up = up;
                this.radius = radius;
                this.color = color;
            }
        }

        class FingerTrace {
            public List<CorePoint> points = new List<CorePoint>();
        }


        private List<FingerTrace> GetTraces(CurrentNoteRendering r) {
            List<FingerTrace> traces = new List<FingerTrace>();
            foreach (var trace in r.missed) {
                FingerTrace ft = new FingerTrace();
                foreach (var p in trace.points) {
                    CorePoint cp = new CorePoint(new Vector3(p.x, fallingSpeed * chart.bpm * (float)(p.time), 0), Vector3.UnitY, Vector3.UnitZ, p.force, new Color(0.5f, 0.5f, 0.5f, 0.7f));
                    ft.points.Add(cp);
                }
                traces.Add(ft);
            }
            foreach (var trace in r.inProgress)
            {
                FingerTrace ft = new FingerTrace();
                foreach (var p in trace.points)
                {
                    CorePoint cp = new CorePoint(new Vector3(p.x, fallingSpeed * chart.bpm * (float)(p.time), 0), Vector3.UnitY, Vector3.UnitZ, p.force, Color.Yellow);
                    ft.points.Add(cp);
                }
                traces.Add(ft);
            }
            foreach (var trace in r.future)
            {
                FingerTrace ft = new FingerTrace();
                foreach (var p in trace.points)
                {
                    CorePoint cp = new CorePoint(new Vector3(p.x, fallingSpeed * chart.bpm * (float)(p.time), 0), Vector3.UnitY, Vector3.UnitZ, p.force, Color.LightBlue);
                    ft.points.Add(cp);
                }
                traces.Add(ft);
            }
            return traces;
        }

        private Tuple<VertexPositionColorNormal[], int[]> getCylinder(CorePoint[] points, int steps, bool reverse) {
            VertexPositionColorNormal[] vertices = new VertexPositionColorNormal[points.Length * steps];
            int[] indices = new int[2*steps+2];
            for (int i = 0; i < points.Length; i++) {
                CorePoint p = points[i];
                for (int j = 0; j < steps; j++) {
                    Matrix fromAxisAngle = Matrix.CreateFromAxisAngle(p.orient, (float) ((reverse?-1:1)*j * Math.PI * 2 / steps));
                    vertices[i*steps + j].Position = Vector3.Transform(p.up, fromAxisAngle)*p.radius + p.pos;
                    vertices[i*steps + j].Normal = Vector3.TransformNormal(p.up, fromAxisAngle);
                    vertices[i*steps + j].Color = p.color;
                }
            }
            for (int j = 0; j < steps; j++) {
                indices[2*j] = j;
                indices[2 * j + 1] = steps + j;
            }
            indices[2 * steps] = 0;
            indices[2 * steps + 1] = steps;
            return new Tuple<VertexPositionColorNormal[], int[]>(vertices, indices);
        }

        private Tuple<VertexPositionColorNormal[], int[]> getCylinderCap(CorePoint point, int steps, bool reverse, bool backwards)
        {
            VertexPositionColorNormal[] vertices = new VertexPositionColorNormal[steps * steps];
            int[] indices = new int[2 * steps + 2];
            for (int i = 0; i < steps; i++)
            {
                for (int j = 0; j < steps; j++) {
                    float t = (float) j/(steps - 1);
                    Vector3 newp = point.pos - point.radius* point.orient*(backwards ? 1 : -1)*t;
                    float newr = point.radius*(float)Math.Sqrt(1-(1-t)*(1-t));
                    Vector3 newn = newr*point.up + newp - point.pos;
                    newn.Normalize();
                    Matrix rotationMatrix = Matrix.CreateFromAxisAngle(point.orient, (float)((reverse ? -1 : 1) * j * Math.PI * 2 / steps));
                    vertices[i * steps + j].Position = Vector3.Transform(point.up, rotationMatrix) * newr + newp;
                    vertices[i * steps + j].Normal = Vector3.TransformNormal(newn, rotationMatrix);
                    vertices[i * steps + j].Color = point.color;
                }
            }
            for (int j = 0; j < steps; j++)
            {
                indices[2 * j] = j;
                indices[2 * j + 1] = steps + j;
            }
            indices[2 * steps] = 0;
            indices[2 * steps + 1] = steps;
            return new Tuple<VertexPositionColorNormal[], int[]>(vertices, indices);
        }


        private VertexPositionColorNormal[] getPlane(Vector3 from, Vector3 to, Vector3 along) {

            Vector3 normal = Vector3.Cross(along, to - from);
            Vector3 p1 = from, p2 = to, p3 = from + along, p4 = to + along;
            Color c1 = Color.White, c2 = Color.Black, c3 = Color.White, c4 = Color.Black;
            return new[] {
                             new VertexPositionColorNormal {Color = c1, Normal = normal, Position = p1},
                             new VertexPositionColorNormal {Color = c2, Normal = normal, Position = p2},
                             new VertexPositionColorNormal {Color = c3, Normal = normal, Position = p3},
                             new VertexPositionColorNormal {Color = c4, Normal = normal, Position = p4}
                         };

        }



        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime) {
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.Clear(Color.Black);
            spriteBatch.Begin();

            var font = Content.Load<SpriteFont>("Arial");
            spriteBatch.DrawString(font, "Score: " + Math.Floor(session.score), new Vector2(50, 100), Color.Red);
            spriteBatch.End();

            float noteOffset = (float) (time * fallingSpeed * chart.bpm);
            basicEffect.Projection = Matrix.CreatePerspective(0.1f, 0.2f, 0.1f, 50);
            basicEffect.View = Matrix.CreateLookAt(new Vector3(0.5f, -0.5f, 2f), new Vector3(0.5f, 1f, 0), Vector3.Normalize(new Vector3(0, 1, 0.5f)));
            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes) {
                pass.Apply();


                basicEffect.World = Matrix.CreateTranslation(0, -noteOffset, 0);
                VertexPositionColorNormal[] plane = getPlane(new Vector3(0, noteOffset, 0), new Vector3(0, noteOffset+10, 0),
                    new Vector3(1, 0, 0));
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, plane, 0, 2);

                foreach (var fingerTrace in songTraces) {
                    CorePoint[] points = fingerTrace.points.ToArray();
                    var cylinder = getCylinder(points, 10, false);
                    var cap1 = getCylinderCap(points[0], 10, false, false);
                    var cap2 = getCylinderCap(points[points.Length - 1], 10, false, true);
                    VertexBuffer buffer = new VertexBuffer(GraphicsDevice, VertexPositionColorNormal.VertexDeclaration,
                        cylinder.Item1.Length, BufferUsage.WriteOnly);
                    IndexBuffer indBuf = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits,
                        cylinder.Item2.Length,
                        BufferUsage.WriteOnly);
                    buffer.SetData(cylinder.Item1);
                    indBuf.SetData(cylinder.Item2);
                    GraphicsDevice.SetVertexBuffer(buffer);
                    GraphicsDevice.Indices = indBuf;
                    for (int i = 0; i < points.Length - 1; i++) {
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 10*i, 0,
                            cylinder.Item1.Length - 10*i, 0, 20);
                    }
                    VertexBuffer cap1Buffer = new VertexBuffer(GraphicsDevice, VertexPositionColorNormal.VertexDeclaration, cap1.Item1.Length, BufferUsage.WriteOnly);
                    VertexBuffer cap2Buffer = new VertexBuffer(GraphicsDevice, VertexPositionColorNormal.VertexDeclaration, cap2.Item1.Length, BufferUsage.WriteOnly);
                    cap1Buffer.SetData(cap1.Item1);
                    GraphicsDevice.SetVertexBuffer(cap1Buffer);

                    for (int i = 0; i < 9; i++)
                    {
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 10 * i, 0,
                            cap1.Item1.Length - 10 * i, 0, 20);
                    }
                    cap2Buffer.SetData(cap2.Item1);
                    GraphicsDevice.SetVertexBuffer(cap2Buffer);

                    for (int i = 0; i < 9; i++)
                    {
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 10 * i, 0,
                            cap2.Item1.Length - 10 * i, 0, 20);
                    }
                }

                foreach (var fingerTrace in userTraces)
                {
                    CorePoint[] points = fingerTrace.points.ToArray();
                    var cylinder = getCylinder(points, 10, true);
                    var cap1 = getCylinderCap(points[0], 10, true, false);
                    var cap2 = getCylinderCap(points[points.Length -1], 10, true, true);
                    VertexBuffer buffer = new VertexBuffer(GraphicsDevice, VertexPositionColorNormal.VertexDeclaration,
                        cylinder.Item1.Length, BufferUsage.WriteOnly);
                    IndexBuffer indBuf = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits,
                        cylinder.Item2.Length,
                        BufferUsage.WriteOnly);
                    buffer.SetData(cylinder.Item1);
                    indBuf.SetData(cylinder.Item2);
                    GraphicsDevice.SetVertexBuffer(buffer);
                    GraphicsDevice.Indices = indBuf;
                    for (int i = 0; i < points.Length - 1; i++)
                    {
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 10 * i, 0,
                            cylinder.Item1.Length - 10 * i, 0, 20);
                    }
                    VertexBuffer cap1Buffer = new VertexBuffer(GraphicsDevice, VertexPositionColorNormal.VertexDeclaration, cap1.Item1.Length, BufferUsage.WriteOnly);
                    VertexBuffer cap2Buffer = new VertexBuffer(GraphicsDevice, VertexPositionColorNormal.VertexDeclaration, cap2.Item1.Length, BufferUsage.WriteOnly);
                    cap1Buffer.SetData(cap1.Item1);
                    GraphicsDevice.SetVertexBuffer(cap1Buffer);

                    for (int i = 0; i < 9; i++)
                    {
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 10 * i, 0,
                            cap1.Item1.Length - 10 * i, 0, 20);
                    }
                    cap2Buffer.SetData(cap2.Item1);
                    GraphicsDevice.SetVertexBuffer(cap2Buffer);

                    for (int i = 0; i < 9; i++)
                    {
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 10 * i, 0,
                            cap2.Item1.Length - 10 * i, 0, 20);
                    }
                }

            }
            base.Draw(gameTime);
        }
    }
}
