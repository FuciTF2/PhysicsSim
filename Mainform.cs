using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhysicsSim
{
    public partial class MainForm : Form
    {
        private PhysicsWorld _world;
        private System.Windows.Forms.Timer _gameTimer;

        // Mouse state
        private SoftBody? _grabbed = null;
        private PointF _lastMousePos;
        private PointF _throwVelocity;

        // Spawn settings
        private int _spawnCols = 5;
        private int _spawnRows = 5;
        private float _cellSize = 20f;
        private float _stiffness = 80f;
        private float _yieldStrength = 0.2f;
        private float _breakStrength = 0.6f;

        private readonly Color[] _palette = {
            Color.FromArgb(100, 180, 255),
            Color.FromArgb(100, 255, 150),
            Color.FromArgb(255, 160, 80),
            Color.FromArgb(220, 100, 220),
            Color.FromArgb(255, 220, 80),
        };
        private int _colorIndex = 0;
        private Random _rng = new();

        private Label? _infoLabel;
        private Panel? _canvas;

        public MainForm()
        {
            Text = "Physics Sim — Soft Body Crusher";
            ClientSize = new Size(1000, 700);
            BackColor = Color.FromArgb(20, 20, 30);
            DoubleBuffered = true;

            BuildUI();

            _world = new PhysicsWorld(new RectangleF(0, 0, _canvas!.Width, _canvas!.Height - 40));;

            _gameTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
            _gameTimer.Tick += GameLoop;
            _gameTimer.Start();
        }

        private void BuildUI()
        {
            // Sidebar
            var sidebar = new Panel
            {
                Dock = DockStyle.Right,
                Width = 200,
                BackColor = Color.FromArgb(30, 30, 45),
                Padding = new Padding(10)
            };

            int y = 10;

            void AddLabel(string text, bool header = false)
            {
                var lbl = new Label
                {
                    Text = text,
                    ForeColor = header ? Color.FromArgb(150, 200, 255) : Color.FromArgb(180, 180, 200),
                    Font = header ? new Font("Segoe UI", 9f, FontStyle.Bold) : new Font("Segoe UI", 8.5f),
                    AutoSize = true,
                    Location = new Point(10, y)
                };
                sidebar.Controls.Add(lbl);
                y += header ? 20 : 16;
            }

            TrackBar AddSlider(string label, int min, int max, int value, Action<int> onChange)
            {
                AddLabel(label);
                var tb = new TrackBar
                {
                    Minimum = min, Maximum = max, Value = value,
                    Location = new Point(5, y), Width = 185, Height = 35,
                    TickFrequency = (max - min) / 5,
                    BackColor = Color.FromArgb(30, 30, 45)
                };
                tb.ValueChanged += (s, e) => onChange(tb.Value);
                sidebar.Controls.Add(tb);
                y += 40;
                return tb;
            }

            AddLabel("SPAWN SETTINGS", true);
            AddSlider("Grid Cols", 2, 10, _spawnCols, v => _spawnCols = v);
            AddSlider("Grid Rows", 2, 10, _spawnRows, v => _spawnRows = v);
            AddSlider("Cell Size", 10, 40, (int)_cellSize, v => _cellSize = v);

            y += 5;
            AddLabel("MATERIAL", true);
            AddSlider("Stiffness", 50, 800, (int)_stiffness, v => _stiffness = v);
            AddSlider("Yield (0=rigid)", 5, 60, (int)(_yieldStrength * 100), v => _yieldStrength = v / 100f);
            AddSlider("Break point", 20, 95, (int)(_breakStrength * 100), v => _breakStrength = v / 100f);

            y += 5;
            AddLabel("WORLD", true);
            AddSlider("Gravity", 0, 2000, 800, v => _world.Gravity = v);
            AddSlider("Bounciness", 0, 100, 40, v => _world.Restitution = v / 100f);

            y += 5;
            var clearBtn = new Button
            {
                Text = "Clear All",
                Location = new Point(10, y),
                Size = new Size(175, 30),
                BackColor = Color.FromArgb(180, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            clearBtn.Click += (s, e) => _world.Bodies.Clear();
            sidebar.Controls.Add(clearBtn);

            Controls.Add(sidebar);

            // Info label
            _infoLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                ForeColor = Color.FromArgb(120, 160, 200),
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Color.FromArgb(20, 20, 30),
                Text = "Left click: spawn   |   Drag: grab & throw   |   Right click: delete   |   Sliders adjust spawn properties"
            };
            Controls.Add(_infoLabel);

            // Canvas
            _canvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25)
            };
            _canvas.Paint += OnCanvasPaint;
            _canvas.MouseDown += OnMouseDown;
            _canvas.MouseMove += OnMouseMove;
            _canvas.MouseUp += OnMouseUp;
            Controls.Add(_canvas);
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            float dt = 0.016f / 8f; // smaller substep
            for (int i = 0; i < 8; i++)
            {
                if (_grabbed != null)
                {
                    _grabbed.Position = _lastMousePos;
                    foreach (var node in _grabbed.Nodes)
                        node.Velocity = PointF.Empty;
            }
            _world.Step(dt);
        }

            _world.Bounds = new RectangleF(0, 0, _canvas!.Width, _canvas!.Height - 40);
            _canvas!.Invalidate();
        }

        private void OnCanvasPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw floor
            using var floorBrush = new SolidBrush(Color.FromArgb(50, 80, 120));
            using var floorPen = new Pen(Color.FromArgb(80, 130, 190), 2f);
            int floorY = _canvas!.Height - 40;
            g.FillRectangle(floorBrush, 0, floorY, _canvas.Width, 40);
            g.DrawLine(floorPen, 0, floorY, _canvas.Width, floorY);
            // Draw particles
            foreach (var p in _world.Particles)
            {
                float alpha = p.Life / p.MaxLife;
                using var brush = new SolidBrush(Color.FromArgb((int)(alpha * 200), p.Color));
                g.FillEllipse(brush, p.Position.X - p.Size / 2, p.Position.Y - p.Size / 2, p.Size, p.Size);
            }

            // Draw bodies
            foreach (var body in _world.Bodies)
                body.Draw(g);

            // HUD
            using var hudFont = new Font("Segoe UI", 9f);
            using var hudBrush = new SolidBrush(Color.FromArgb(80, 150, 220));
            g.DrawString($"Bodies: {_world.Bodies.Count}   Particles: {_world.Particles.Count}", hudFont, hudBrush, 8, 8);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            var pos = new PointF(e.X, e.Y);

            if (e.Button == MouseButtons.Left)
            {
                var hit = _world.GetBodyAt(pos);
                if (hit != null)
                {
                    _grabbed = hit;
                    _grabbed.IsGrabbed = true;
                }
                else
                {
                    SpawnBody(pos);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                var hit = _world.GetBodyAt(pos);
                if (hit != null) _world.Bodies.Remove(hit);
            }

            _lastMousePos = pos;
            _throwVelocity = PointF.Empty;
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            var pos = new PointF(e.X, e.Y);

            if (_grabbed != null)
            {
                _throwVelocity = new PointF(
                    (pos.X - _lastMousePos.X) / 0.016f,
                    (pos.Y - _lastMousePos.Y) / 0.016f
                );
                _lastMousePos = pos;
            }
            else
            {
                _lastMousePos = pos;
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (_grabbed != null)
            {
                _grabbed.IsGrabbed = false;
                // Apply throw velocity
                foreach (var node in _grabbed.Nodes)
                    node.Velocity = _throwVelocity;
                _grabbed = null;
            }
        }

        private void SpawnBody(PointF center)
        {
            var color = _palette[_colorIndex % _palette.Length];
            _colorIndex++;

            float w = _spawnCols * _cellSize;
            float h = _spawnRows * _cellSize;
            var topLeft = new PointF(center.X - w / 2, center.Y - h / 2);

            var body = new SoftBody(
                topLeft, _spawnCols, _spawnRows, _cellSize,
                mass: 5f,
                color: color,
                stiffness: _stiffness,
                damping: 15f,
                yieldStrength: _yieldStrength,
                breakStrength: _breakStrength
            );

            _world.AddBody(body);
        }
    }
}