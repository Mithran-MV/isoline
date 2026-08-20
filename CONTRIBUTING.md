# Contributing

## Building

```bash
dotnet build Isoline.sln -c Release
dotnet test tests/Isoline.Core.Tests/Isoline.Core.Tests.csproj
```

On Linux or macOS the core and its tests work as-is. The WPF project needs
`-p:EnableWindowsTargeting=true` to compile and cannot be run.

## Where code goes

Anything that does not need a window belongs in `Isoline.Core`, and should arrive with
tests. If you find yourself adding `using System.Windows` to a file in `Isoline.Core`, that
is the signal it belongs in `Isoline.App` instead — see
[docs/architecture.md](docs/architecture.md).

## Style

Tabs, Allman braces, and the naming the existing code uses. Comments explain *why*
something is the way it is; the code already says what it does.

## Anything that moves the machine

Isoline drives a machine with a spinning cutter in it. Changes to jogging, probing,
streaming or the resume path need a note in the pull request saying how you tested them,
and what happens when the assumption fails — a lost connection mid-move, an alarm during
probing, a file that changed under a resume.
