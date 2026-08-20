---
name: I need help getting it running
about: New to Isoline and stuck on setup
---

Before opening this, the quick checks that solve most cases:

- The controller is running **Grbl 1.1f or newer** (or grblHAL, FluidNC, uCNC). Grbl 0.8,
  0.9 and 1.0 will not work, and there is no workaround.
- You have the **.NET 8 Desktop Runtime** installed, or you downloaded the standalone build
  which does not need it.
- The serial port and baud rate match the controller. Isoline asks for these on first run;
  they are under Settings afterwards.
- Nothing else is holding the port open - the Arduino IDE serial monitor is the usual
  culprit.

**What are you trying to do, and where does it stop?**


**Setup**
- Isoline version:
- Controller and firmware:
- Windows version:
