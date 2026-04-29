using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PhysicsSim
{
    public class Particle
    {
        public PointF Position { get; set; }
        public PointF Velocity { get; set; }
        public float Life { get; set; }       // 0..1, fades to 0
        public float MaxLife { get; }
        public Color Color { get; }
        public float Size { get; }

        public Particle(PointF pos, PointF vel, float life, Color color, float size = 3f)
        {
            Position = pos;
            Velocity = vel;
            Life = life;
            MaxLife = life;
            Color = color;
            Size = size;
        }

        public bool IsAlive => Life > 0;
    }

    public class PhysicsWorld
    {
        public List<SoftBody> Bodies { get; } = new();
        public List<Particle> Particles { get; } = new();

        public float Gravity { get; set; } = 800f;
        public float Restitution { get; set; } = 0.4f;   // wall bounce
        public float Friction { get; set; } = 0.98f;     // velocity damping per frame

        public RectangleF Bounds { get; set; }

        private Random _rng = new Random();

        public PhysicsWorld(RectangleF bounds)
        {
            Bounds = bounds;
        }

        public void Step(float dt)
        {
            RemoveDestroyedBodies();
            UpdateParticles(dt);

            foreach (var body in Bodies)
            {
                if (body.IsGrabbed) continue;

                // Accumulate forces on each node
                foreach (var node in body.Nodes)
                    node.Force = new PointF(0, body.Mass / body.Nodes.Count * Gravity);

                // Spring forces
                foreach (var spring in body.Springs)
                {
                    if (spring.IsBroken) continue;

                    var fa = spring.ComputeForceOn(spring.A);
                    var fb = spring.ComputeForceOn(spring.B);

                    spring.A.Force = new PointF(spring.A.Force.X + fa.X, spring.A.Force.Y + fa.Y);
                    spring.B.Force = new PointF(spring.B.Force.X + fb.X, spring.B.Force.Y + fb.Y);
                }

                // Detect newly broken springs and spawn debris
                foreach (var spring in body.Springs.Where(s => s.IsBroken))
                {
                    if (_rng.NextDouble() < 0.3f)
                        SpawnDebris(spring.A.Position, body.Color);
                }

                float nodeMass = body.Mass / body.Nodes.Count;

                // Integrate
                foreach (var node in body.Nodes)
                {
                    float ax = node.Force.X / nodeMass;
                    float ay = node.Force.Y / nodeMass;

                    node.Velocity = new PointF(
                        (node.Velocity.X + ax * dt) * Friction,
                        (node.Velocity.Y + ay * dt) * Friction
                    );

                    node.Position = new PointF(
                        node.Position.X + node.Velocity.X * dt,
                        node.Position.Y + node.Velocity.Y * dt
                    );

                    CollideBounds(node);
                }
            }
        }

        private void CollideBounds(Node node)
        {
            float x = node.Position.X;
            float y = node.Position.Y;
            float vx = node.Velocity.X;
            float vy = node.Velocity.Y;

            if (x < Bounds.Left)   { x = Bounds.Left;   vx = MathF.Abs(vx) * Restitution; }
            if (x > Bounds.Right)  { x = Bounds.Right;  vx = -MathF.Abs(vx) * Restitution; }
            if (y < Bounds.Top)    { y = Bounds.Top;     vy = MathF.Abs(vy) * Restitution; }
            if (y > Bounds.Bottom) { y = Bounds.Bottom;  vy = -MathF.Abs(vy) * Restitution;
                                     vx *= 0.85f; } // floor friction

            node.Position = new PointF(x, y);
            node.Velocity = new PointF(vx, vy);
        }

        private void SpawnDebris(PointF pos, Color color)
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                float speed = (float)(_rng.NextDouble() * 150 + 50);
                var vel = new PointF(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
                Particles.Add(new Particle(pos, vel, (float)(_rng.NextDouble() * 0.8f + 0.3f), color));
            }
        }

        private void UpdateParticles(float dt)
        {
            foreach (var p in Particles)
            {
                if (!p.IsAlive) continue;
                p.Velocity = new PointF(p.Velocity.X, p.Velocity.Y + Gravity * dt);
                p.Position = new PointF(p.Position.X + p.Velocity.X * dt, p.Position.Y + p.Velocity.Y * dt);
                p.Life -= dt;
            }
            Particles.RemoveAll(p => !p.IsAlive);
        }

        private void RemoveDestroyedBodies()
        {
            Bodies.RemoveAll(b => b.IsDestroyed);
        }

        public SoftBody? GetBodyAt(PointF point)
        {
            return Bodies.FirstOrDefault(b => b.Contains(point));
        }

        public void AddBody(SoftBody body) => Bodies.Add(body);
    }
}