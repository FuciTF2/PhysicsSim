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
        public float Life { get; set; }
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
        public float Restitution { get; set; } = 0.4f;
        public float Friction { get; set; } = 1f;

        public RectangleF Bounds { get; set; }

        private const int MaxParticles = 100;
        private const float CollisionStiffness = 600f;

        private Random _rng = new Random();
        private HashSet<Spring> _alreadyBroken = new();

        public PhysicsWorld(RectangleF bounds)
        {
            Bounds = bounds;
        }

        public void Step(float dt)
        {
            RemoveDestroyedBodies();
            UpdateParticles(dt);

            // Collect all nodes for inter-body collision
            var allNodes = new List<(Node node, SoftBody body)>();
            foreach (var body in Bodies)
                foreach (var node in body.Nodes)
                    allNodes.Add((node, body));

            foreach (var body in Bodies)
            {
                // Gravity on non-grabbed nodes
                foreach (var node in body.Nodes)
                {
                    if (node.IsPinned)
                        node.Force = PointF.Empty;
                    else
                        node.Force = new PointF(0, body.Mass / body.Nodes.Count * Gravity);
                }

                // Spring forces
                foreach (var spring in body.Springs)
                {
                    if (spring.IsBroken) continue;
                    var fa = spring.ComputeForceOn(spring.A);
                    var fb = spring.ComputeForceOn(spring.B);
                    spring.A.Force = new PointF(spring.A.Force.X + fa.X, spring.A.Force.Y + fa.Y);
                    spring.B.Force = new PointF(spring.B.Force.X + fb.X, spring.B.Force.Y + fb.Y);
                }

                // Debris for newly broken springs
                foreach (var spring in body.Springs)
                {
                    if (spring.IsBroken && !_alreadyBroken.Contains(spring))
                    {
                        _alreadyBroken.Add(spring);
                        if (Particles.Count < MaxParticles && _rng.NextDouble() < 0.15f)
                            SpawnDebris(spring.A.Position, body.Color);
                    }
                }

                float nodeMass = body.Mass / body.Nodes.Count;

                foreach (var node in body.Nodes)
                {
                    if (node.IsPinned) continue;

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

            ResolveBodyCollisions(allNodes);
        }

        private void ResolveBodyCollisions(List<(Node node, SoftBody body)> allNodes)
        {
            int count = allNodes.Count;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    var (nodeA, bodyA) = allNodes[i];
                    var (nodeB, bodyB) = allNodes[j];

                    if (bodyA == bodyB) continue;

                    float dx = nodeB.Position.X - nodeA.Position.X;
                    float dy = nodeB.Position.Y - nodeA.Position.Y;
                    float distSq = dx * dx + dy * dy;

                    // Use cell size as collision diameter — nodes repel within one cell size
                    float radius = 18f;
                    if (distSq >= radius * radius || distSq < 0.001f) continue;

                    float dist = MathF.Sqrt(distSq);
                    float overlap = radius - dist;
                    float nx = dx / dist;
                    float ny = dy / dist;

                    float massA = bodyA.Mass / bodyA.Nodes.Count;
                    float massB = bodyB.Mass / bodyB.Nodes.Count;
                    float totalMass = massA + massB;

                    // Velocity impulse
                    float impulse = overlap * CollisionStiffness * 0.016f;
                    float pushA = impulse * (massB / totalMass);
                    float pushB = impulse * (massA / totalMass);

                    if (!nodeA.IsPinned)
                        nodeA.Velocity = new PointF(nodeA.Velocity.X - nx * pushA,
                                                    nodeA.Velocity.Y - ny * pushA);
                    if (!nodeB.IsPinned)
                        nodeB.Velocity = new PointF(nodeB.Velocity.X + nx * pushB,
                                                    nodeB.Velocity.Y + ny * pushB);

                    // Positional correction — prevent sinking
                    float corrA = overlap * (massB / totalMass) * 0.4f;
                    float corrB = overlap * (massA / totalMass) * 0.4f;

                    if (!nodeA.IsPinned)
                        nodeA.Position = new PointF(nodeA.Position.X - nx * corrA,
                                                    nodeA.Position.Y - ny * corrA);
                    if (!nodeB.IsPinned)
                        nodeB.Position = new PointF(nodeB.Position.X + nx * corrB,
                                                    nodeB.Position.Y + ny * corrB);
                }
            }
        }

        private void CollideBounds(Node node)
        {
            float x = node.Position.X;
            float y = node.Position.Y;
            float vx = node.Velocity.X;
            float vy = node.Velocity.Y;

            if (x < Bounds.Left)   { x = Bounds.Left;   vx =  MathF.Abs(vx) * Restitution; }
            if (x > Bounds.Right)  { x = Bounds.Right;  vx = -MathF.Abs(vx) * Restitution; }
            if (y < Bounds.Top)    { y = Bounds.Top;     vy =  MathF.Abs(vy) * Restitution; }
            if (y > Bounds.Bottom) { y = Bounds.Bottom;  vy = -MathF.Abs(vy) * Restitution;
                                     vx *= (0.5f + Restitution * 0.5f); }

            x = Math.Clamp(x, Bounds.Left, Bounds.Right);
            y = Math.Clamp(y, Bounds.Top, Bounds.Bottom);

            node.Position = new PointF(x, y);
            node.Velocity = new PointF(vx, vy);
        }

        private void SpawnDebris(PointF pos, Color color)
        {
            for (int i = 0; i < 2; i++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                float speed = (float)(_rng.NextDouble() * 120 + 40);
                var vel = new PointF(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
                Particles.Add(new Particle(pos, vel, (float)(_rng.NextDouble() * 0.5f + 0.2f), color));
            }
        }

        private void UpdateParticles(float dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];
                p.Velocity = new PointF(p.Velocity.X, p.Velocity.Y + Gravity * dt);
                p.Position = new PointF(p.Position.X + p.Velocity.X * dt, p.Position.Y + p.Velocity.Y * dt);
                p.Life -= dt;
                if (!p.IsAlive) Particles.RemoveAt(i);
            }
        }

        private void RemoveDestroyedBodies()
        {
            Bodies.RemoveAll(b => b.IsDestroyed);
        }

        // Find the node closest to a point within a given body
        public Node? GetClosestNode(SoftBody body, PointF point)
        {
            Node? closest = null;
            float bestDist = float.MaxValue;
            foreach (var node in body.Nodes)
            {
                float dx = node.Position.X - point.X;
                float dy = node.Position.Y - point.Y;
                float d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; closest = node; }
            }
            return closest;
        }

        public SoftBody? GetBodyAt(PointF point)
        {
            return Bodies.FirstOrDefault(b => b.Contains(point));
        }

        public void AddBody(SoftBody body) => Bodies.Add(body);
    }
}