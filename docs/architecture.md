# How Isoline is put together

## The split, and why it exists

```
Isoline.App  (net8.0-windows)   WPF shell, machine communication, 3D viewport
     │
     ▼ project reference
Isoline.Core (net8.0)           G-code, height maps, Gerber, toolpaths
     ▲
     └── Isoline.Core.Tests     xUnit, runs on Linux CI
```

Upstream OpenCNCPilot was one project. `GCodeFile` imported `HelixToolkit.Wpf` so it could
build a 3D preview, and read `Properties.Settings.Default` so it could decide whether to
emit `M30`. `HeightMap` did the same. The practical consequence was that none of the
interesting code could be exercised without a window on screen, so none of it was tested.

Isoline moves everything that does not need a screen into `Isoline.Core`:

| Moved out of core | Into |
|---|---|
| `GCodeFile.GetModel(LinesVisual3D…)` | `Isoline.App/Visuals/ToolpathVisuals.cs` |
| `HeightMap.GetModel(MeshGeometryVisual3D)` | `Isoline.App/Visuals/HeightMapVisuals.cs` |
| `Vector3.ToPoint3D()` and friends | `Isoline.App/Visuals/GeometryExtensions.cs` |
| `Settings.Default.GCodeInclude*` | `GCodeOutputOptions.Current`, set at start-up |
| `Settings.Default.IgnoreAdditionalAxes` | `GCodeParserOptions.Current` |
| `Settings.Default.FirmwareType` | a parameter to `GrblCodeTranslator.Reload` |

The CI Linux job builds and tests the core. If a Windows-only dependency ever creeps back
in, that job stops compiling — the boundary is enforced by the build, not by discipline.

## Data flow

```
Gerber file ──► GerberParser ──► PathsD (copper, in mm)
                                     │
                                     ▼
                     IsolationToolpathGenerator (Clipper2 offsets)
                                     │
                                     ▼ G-code text
                              GCodeParser ──► GCodeFile (commands)
                                     │
   probe grid ──► HeightMap ─────────┤
                                     ▼
                     GCodeFile.Split → ArcsToLines → ApplyHeightMap
                                     │
                                     ▼
                            Machine (serial / telnet / WebSocket)
```

Everything above the last arrow is testable without hardware, and is.

## The pieces

### `GCode`

`GCodeParser` is a modal-state parser: it tracks distance mode, units, plane and the last
motion mode, and produces `Line`, `Arc`, `Dwell`, `Spindle` and `MCode` commands, each
remembering the source line it came from. `GCodeFile` wraps a command list and adds the
operations the sender needs: `Split` (break long moves into segments), `ArcsToLines` and
`ApplyHeightMap`.

Height compensation only works on short segments, so the order matters:
`Split(length) → ArcsToLines(length) → ApplyHeightMap(map)`. A 100 mm move wrapped onto a
curved surface without splitting would be a straight line through the middle of the board.

### `HeightMaps`

`HeightMap` is a grid of nullable heights plus the corners it spans. `InterpolateZ`
dispatches on `Interpolation`:

- **Bilinear** — the original behaviour. Exact at the probed points, but the surface has a
  crease along every grid line.
- **Bicubic** — Catmull-Rom over a 4×4 window, still exact at the probed points, but C1
  continuous. Falls back to bilinear within one cell of the border, where the support
  window does not exist, and around any un-probed hole.

`OutlierFilter` uses the median absolute deviation of each point's 8-neighbourhood rather
than mean and standard deviation, because a mean is itself dragged around by the outlier it
is meant to find. The 1.4826 factor makes the MAD a consistent estimator of σ for normal
data, so a threshold of 3.5 means what it usually means.

### `Gerber`

A reader for the RS-274X subset that CAD tools emit for copper: `%FS` coordinate formats,
`%MO` units, standard C/R/O/P apertures, `D01`/`D02`/`D03`, linear and circular
interpolation in both quadrant modes, regions (`G36`/`G37`) and polarity (`%LPD`/`%LPC`).

Everything becomes one polygon set in millimetres. Tracks are built by inflating their
centre line by half the aperture width — which is exactly what a photoplotter does when it
drags an aperture — and dark and clear exposures are composed in order with union and
difference, because a `%LPC` region only clears what was drawn before it.

Aperture macros (`%AM`) are approximated by a circle and *reported as a warning*. Silently
drawing them wrong would be worse than saying so.

### `Toolpaths`

Pass *n* is the copper outline inflated by `toolRadius + n × stepover × toolDiameter`, then
unioned (offsetting islands that sit close together produces overlapping outlines that the
tool cannot actually thread between) and simplified. Contours are ordered greedily by
nearest neighbour and each is rotated to start at the vertex closest to where the tool
already is: on a board with a few hundred pads, tracing them in Gerber order can spend more
time in rapids than in the cut.

### `Jobs`

`JobRecoveryState` is written next to the executable once a second while a job runs. It
stores the file's SHA-256, so a resume against a re-exported file is refused rather than
cutting in the wrong place. `BuildResumePreamble` restores the modal groups first, then
lifts, then travels in XY, then starts the spindle and dwells, and only then plunges.

## The application layer

- **Theme** — `Tokens.Dark.xaml` and `Tokens.Light.xaml` define the same key set with
  different values. Control templates reference them with `DynamicResource`, so
  `ThemeManager.Apply` swaps one dictionary and the whole interface re-resolves.
- **`StatusPresentation`** — maps controller state to a pill colour and decodes alarm and
  error numbers into a headline plus a remedy.
- **`JobProgress`** — estimates time remaining from the observed line rate, smoothed with
  an exponential moving average, rather than from the file's nominal feed rates, which
  ignore the buffer, the link speed and every override the operator touches.
- **`WebSocketConnection`** — presents FluidNC's WebSocket console as a `Stream`, so the
  worker loop drives WiFi and USB with the same code.

## Testing

`Isoline.Core.Tests` covers the expression evaluator, the Gerber reader, the isolation
generator, height map interpolation and filtering, and job recovery. Tests run on any
platform:

```bash
dotnet test tests/Isoline.Core.Tests/Isoline.Core.Tests.csproj
```

The WPF project can be *compiled* on Linux and macOS with
`-p:EnableWindowsTargeting=true`, which is enough to catch XAML and binding errors; the
Windows CI job is what proves it actually publishes.
