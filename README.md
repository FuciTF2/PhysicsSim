# Physics Sim — Soft Body Crusher

A sandbox desktop physics toy built with C# and WinForms. Spawn soft body objects, throw them around, stack them, and watch them deform and break apart under stress.

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows (WinForms is Windows-only)
- [VS Code](https://code.visualstudio.com/) + [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (optional, for editing)

---

## Running

```bash
git clone <https://github.com/FuciTF2/PhysicsSim.git>
cd PhysicsSim
dotnet run
```

Or to build without running:

```bash
dotnet build
```

---

## Controls

| Input | Action |
|---|---|
| Left click (empty space) | Spawn a soft body |
| Left click + drag | Grab and swing a body |
| Release drag | Throw the body |
| Right click | Delete a body |
| Clear All button | Remove everything |

Grab a body by a corner or edge and it will swing from that point under gravity.

---

## Sidebar Controls

### Spawn Settings
| Slider | Effect |
|---|---|
| Grid Cols / Rows | Size of the node grid per body |
| Cell Size | Spacing between nodes in pixels |

### Material
| Slider | Effect |
|---|---|
| Stiffness | How rigid the springs are — higher = stiffer body |
| Yield | How much stretch before permanent deformation begins |
| Break point | How much stretch before springs snap |

Low yield + low break = very squishy, falls apart easily. High stiffness + high yield = bouncy rigid block.

### World
| Slider | Effect |
|---|---|
| Gravity | Downward acceleration |
| Bounciness | How much velocity is preserved on wall/floor bounce |

---

## How It Works

Each object is a **soft body** — a grid of point masses (nodes) connected by springs. The physics simulation runs every frame:

1. **Gravity** pulls each node downward
2. **Spring forces** try to maintain the rest length between connected nodes
3. **Yield** — springs that are stretched beyond their yield threshold permanently deform (rest length shifts), shown by the spring turning red
4. **Breaking** — springs stretched past the break threshold snap entirely and emit debris particles
5. **Collision** — nodes from different bodies repel each other, allowing stacking
6. **Bounds** — nodes bounce off walls and the floor with configurable restitution

Simulation uses **adaptive substeps** — more substeps when fewer bodies are present for accuracy, fewer substeps as the scene gets busier to maintain framerate.

---

## Project Structure

```
PhysicsSim/
├── PhysicsSim.csproj     — project file (.NET 8, WinForms)
├── Program.cs            — entry point
├── PhysicsObject.cs      — abstract base class for physics objects
├── SoftBody.cs           — Node, Spring, and SoftBody classes
├── PhysicsWorld.cs       — simulation loop, collision, particles
├── MainForm.cs           — render loop, mouse input, UI
└── .gitignore
```

---

## Tips

- **Crush things**: spawn a large stiff body and drop it on a small soft one (low yield, low break)
- **Jelly**: low stiffness + low yield + high bounciness
- **Glass**: high stiffness + low break point — shatters on impact
- **Rubber**: high stiffness + high yield + high bounciness — deforms but springs back