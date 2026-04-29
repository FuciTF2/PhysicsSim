using System.Drawing;

namespace PhysicsSim
{
    public abstract class PhysicsObject
    {
        public virtual PointF Position { get; set; }
        public virtual PointF Velocity { get; set; }
        public float Mass { get; set; }
        public bool IsGrabbed { get; set; }
        public Color Color { get; set; }

        protected PhysicsObject(PointF position, float mass, Color color)
        {
            Position = position;
            Mass = mass;
            Color = color;
            Velocity = PointF.Empty;
        }

        public abstract void Draw(Graphics g);
        public abstract RectangleF GetBounds();
        public abstract bool Contains(PointF point);
    }
}