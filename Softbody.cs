using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PhysicsSim
{
    public class Node
    {
        public PointF Position { get; set; }
        public PointF Velocity { get; set; }
        public PointF Force { get; set; }
        public bool IsPinned { get; set; }

        public Node(PointF position)
        {
            Position = position;
            Velocity = PointF.Empty;
            Force = PointF.Empty;
        }
    }

    public class Spring
    {
        public Node A { get; }
        public Node B { get; }
        public float RestLength { get; private set; }
        public float Stiffness { get; set; }
        public float Damping { get; set; }

        // Deformation tracking
        public float PlasticOffset { get; private set; } = 0f; // permanent deformation
        public bool IsBroken { get; private set; } = false;

        // Tunable material thresholds
        public float YieldStrength { get; set; }   // stretch ratio before permanent deform
        public float BreakStrength { get; set; }   // stretch ratio before snapping

        public Spring(Node a, Node b, float stiffness, float damping, float yieldStrength = 0.25f, float breakStrength = 0.7f)
        {
            A = a;
            B = b;
            Stiffness = stiffness;
            Damping = damping;
            YieldStrength = yieldStrength;
            BreakStrength = breakStrength;

            float dx = b.Position.X - a.Position.X;
            float dy = b.Position.Y - a.Position.Y;
            RestLength = MathF.Sqrt(dx * dx + dy * dy);
        }

        public PointF ComputeForceOn(Node node)
        {
            if (IsBroken) return PointF.Empty;

            Node other = (node == A) ? B : A;

            float dx = other.Position.X - node.Position.X;
            float dy = other.Position.Y - node.Position.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) return PointF.Empty;

            float effectiveRest = RestLength + PlasticOffset;
            float stretch = (dist - effectiveRest) / effectiveRest;

            // Yield: plastically deform rest length
            if (MathF.Abs(stretch) > YieldStrength)
            {
                float excess = stretch - MathF.Sign(stretch) * YieldStrength;
                PlasticOffset += excess * effectiveRest * 0.05f; // flow rate
            }

            // Break
            if (MathF.Abs(stretch) > BreakStrength)
            {
                IsBroken = true;
                return PointF.Empty;
            }

            float springForce = Stiffness * (dist - effectiveRest);

            // Damping along spring axis
            float dvx = other.Velocity.X - node.Velocity.X;
            float dvy = other.Velocity.Y - node.Velocity.Y;
            float relVel = (dvx * dx + dvy * dy) / dist;
            float dampForce = Damping * relVel;

            float total = springForce + dampForce;
            return new PointF(total * dx / dist, total * dy / dist);
        }
    }

    public class SoftBody : PhysicsObject
    {
        public List<Node> Nodes { get; } = new();
        public List<Spring> Springs { get; } = new();

        private int _cols;
        private int _rows;

        public SoftBody(PointF topLeft, int cols, int rows, float cellSize, float mass, Color color,
                        float stiffness = 300f, float damping = 5f,
                        float yieldStrength = 0.2f, float breakStrength = 0.6f)
            : base(topLeft, mass, color)
        {
            _cols = cols;
            _rows = rows;

            float nodeMass = mass / (cols * rows);

            // Create grid of nodes
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var pos = new PointF(topLeft.X + c * cellSize, topLeft.Y + r * cellSize);
                    Nodes.Add(new Node(pos));
                }
            }

            // Connect with springs: structural + shear + bend
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i = r * cols + c;

                    // Structural (right + down)
                    if (c + 1 < cols) AddSpring(i, r * cols + (c + 1), stiffness, damping, yieldStrength, breakStrength);
                    if (r + 1 < rows) AddSpring(i, (r + 1) * cols + c, stiffness, damping, yieldStrength, breakStrength);

                    // Shear (diagonals)
                    if (c + 1 < cols && r + 1 < rows)
                    {
                        AddSpring(i, (r + 1) * cols + (c + 1), stiffness * 0.7f, damping, yieldStrength, breakStrength);
                        AddSpring(r * cols + (c + 1), (r + 1) * cols + c, stiffness * 0.7f, damping, yieldStrength, breakStrength);
                    }

                    // Bend (skip-one connections resist bending)
                    if (c + 2 < cols) AddSpring(i, r * cols + (c + 2), stiffness * 0.3f, damping, yieldStrength, breakStrength);
                    if (r + 2 < rows) AddSpring(i, (r + 2) * cols + c, stiffness * 0.3f, damping, yieldStrength, breakStrength);
                }
            }
        }

        private void AddSpring(int a, int b, float stiffness, float damping, float yield, float breakStr)
        {
            Springs.Add(new Spring(Nodes[a], Nodes[b], stiffness, damping, yield, breakStr));
        }

        // Centroid position used for grabbing / world position
        public override PointF Position
        {
            get
            {
                if (Nodes.Count == 0) return base.Position;
                float x = Nodes.Average(n => n.Position.X);
                float y = Nodes.Average(n => n.Position.Y);
                return new PointF(x, y);
            }
            set
            {
                PointF current = Position;
                float dx = value.X - current.X;
                float dy = value.Y - current.Y;
                foreach (var node in Nodes)
                    node.Position = new PointF(node.Position.X + dx, node.Position.Y + dy);
            }
        }

        public override PointF Velocity
        {
            get
            {
                if (Nodes.Count == 0) return PointF.Empty;
                float vx = Nodes.Average(n => n.Velocity.X);
                float vy = Nodes.Average(n => n.Velocity.Y);
                return new PointF(vx, vy);
            }
            set
            {
                foreach (var node in Nodes)
                    node.Velocity = value;
            }
        }

        public override RectangleF GetBounds()
        {
            if (Nodes.Count == 0) return RectangleF.Empty;
            float minX = Nodes.Min(n => n.Position.X);
            float minY = Nodes.Min(n => n.Position.Y);
            float maxX = Nodes.Max(n => n.Position.X);
            float maxY = Nodes.Max(n => n.Position.Y);
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public override bool Contains(PointF point)
        {
            return GetBounds().Contains(point);
        }

        public override void Draw(Graphics g)
        {
            // Draw springs
            foreach (var spring in Springs)
            {
                if (spring.IsBroken) continue;

                // Color shift based on plastic deformation
                float deform = MathF.Min(MathF.Abs(spring.PlasticOffset) / (spring.RestLength * spring.YieldStrength), 1f);
                Color springColor = InterpolateColor(Color, Color.Red, deform);
                using var pen = new Pen(springColor, 1.5f);
                g.DrawLine(pen, spring.A.Position, spring.B.Position);
            }

            // Draw nodes
            using var nodeBrush = new SolidBrush(Color.FromArgb(200, Color));
            foreach (var node in Nodes)
            {
                g.FillEllipse(nodeBrush, node.Position.X - 3, node.Position.Y - 3, 6, 6);
            }
        }

        private static Color InterpolateColor(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t)
            );
        }

        public bool IsDestroyed => Springs.All(s => s.IsBroken);
    }
}