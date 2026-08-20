# Changelog

## 2.0.0

First release of Isoline, forked from OpenCNCPilot 1.5.13.

### Added

- **Gerber import.** Reads RS-274X copper layers and generates multi-pass isolation
  toolpaths in-process, with a live preview of what the current settings will cut. V-bit
  cut width is derived from the included angle and the depth of cut.
- **Job recovery.** The file position is checkpointed while a job runs; an interrupted job
  can be resumed after verifying the file still hashes the same. The resume preamble
  restores modal state, lifts, travels, spins up and only then plunges.
- **Machine calibration.** The controller's `$n` settings are read into a live cache, the
  panel populates itself, values are range checked before being written, and a wizard
  computes steps/mm from a commanded move and a measured one.
- **Bicubic height map interpolation** alongside the original bilinear.
- **Probe outlier rejection** using the median absolute deviation of each point's
  neighbourhood.
- **grblHAL and FluidNC support**, including FluidNC's WebSocket console.
- **First-run connection wizard.**
- **Alarm and error banner** that decodes the firmware code and says what to do about it.
- **Job progress** with elapsed time, estimated time remaining and taskbar progress.
- **Light theme**, and a "follow Windows" option.
- **Unit test suite** for the whole computation core, running on Linux CI.
- **GitHub Actions** CI and release workflows.

### Changed

- Migrated from .NET Framework 4.6 to **.NET 8**.
- Split into `Isoline.Core` (no UI, no Windows) and `Isoline.App`.
- Replaced the `martin2250.Calculator` dependency with an expression evaluator written for
  this fork; the package only shipped .NET Framework binaries and blocked the migration.
- The height map surface uses a perceptually uniform viridis ramp instead of a rainbow.
- Colour tokens replace 129 hard-coded hex literals across the XAML; accent text uses a
  lighter variant that meets WCAG AA on the dark surface.
- Controller buffer size is taken from the firmware profile by default.
- The digital readout uses tabular monospaced digits so numbers do not jitter while
  the machine moves.

### Fixed

- `HeightMap` sized its grid before normalising the corners, so a map entered from its far
  corner failed with a misleading "must have at least 4 points" — precisely the case
  upstream's README warned users about.
- The height map surface used `height × span` as its texture coordinate instead of
  `height ÷ span`, so the colouring depended on how warped the board was rather than on
  where each point sat within that warp.
- The parser warned about `G94`, which nearly every real file contains.
- The status label was painted black on a dark background when the machine was idle.
- Disconnecting an Ethernet connection did not dispose the stream.
