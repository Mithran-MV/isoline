#!/usr/bin/env python3
"""
Generates the Isoline application icon.

The mark is literally what the program does: a copper pad-and-trace, wrapped in the
isolation contours a cutter would follow around it. Each contour is the copper shape
offset outwards - the same operation IsolationToolpathGenerator performs - which is why
the rings are drawn as the difference of two filled offsets rather than as strokes.

Usage:  python3 tools/generate_icon.py
Writes: src/Isoline.App/Resources/Isoline.png (512px) and Isoline.ico (multi-size)
"""

import os
from PIL import Image, ImageChops, ImageDraw

SS = 4                      # supersampling factor for antialiasing
SIZE = 512
S = SIZE * SS

BACKGROUND = (18, 18, 22, 255)
COPPER = (200, 120, 58, 255)

# viridis samples, dark to light, matching Colormap.cs in the application
CONTOUR_COLOURS = [
    (42, 120, 142, 255),
    (34, 168, 132, 255),
    (122, 209, 81, 255),
]

CONTOUR_OFFSETS = [30, 62, 94]      # millimetre-ish, in icon units before supersampling
CONTOUR_WIDTH = 11

PAD_RADIUS = 52
TRACE_RADIUS = 20
PAD_A = (-104, -46)
PAD_B = (104, 46)


def _circle(draw, centre, radius, fill):
    x, y = centre
    draw.ellipse(
        [(x - radius) * SS + S // 2, (y - radius) * SS + S // 2,
         (x + radius) * SS + S // 2, (y + radius) * SS + S // 2],
        fill=fill)


def _capsule(draw, a, b, radius, fill):
    """A thick line with round caps: the swept disc of radius `radius` from a to b."""
    draw.line(
        [a[0] * SS + S // 2, a[1] * SS + S // 2, b[0] * SS + S // 2, b[1] * SS + S // 2],
        fill=fill, width=int(radius * 2 * SS), joint="curve")
    _circle(draw, a, radius, fill)
    _circle(draw, b, radius, fill)


def copper_mask(offset=0):
    """The copper geometry, grown outwards by `offset`. Returns an L-mode mask."""
    mask = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(mask)

    _capsule(draw, PAD_A, PAD_B, TRACE_RADIUS + offset, 255)
    _circle(draw, PAD_A, PAD_RADIUS + offset, 255)
    _circle(draw, PAD_B, PAD_RADIUS + offset, 255)

    return mask


def build():
    icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(icon)

    # rounded-square plate
    draw.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=BACKGROUND)

    plate = Image.new("L", (S, S), 0)
    ImageDraw.Draw(plate).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)

    # isolation contours: ring = filled offset outer, minus filled offset inner
    for offset, colour in zip(CONTOUR_OFFSETS, CONTOUR_COLOURS):
        outer = copper_mask(offset + CONTOUR_WIDTH / 2)
        inner = copper_mask(offset - CONTOUR_WIDTH / 2)
        ring = ImageChops.subtract(outer, inner)
        ring = ImageChops.multiply(ring, plate)     # clip to the plate

        icon.paste(Image.new("RGBA", (S, S), colour), (0, 0), ring)

    # the copper itself, on top
    icon.paste(Image.new("RGBA", (S, S), COPPER), (0, 0), copper_mask(0))

    return icon.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(here, "src", "Isoline.App", "Resources")
    os.makedirs(out, exist_ok=True)

    icon = build()
    icon.save(os.path.join(out, "Isoline.png"))
    icon.save(os.path.join(out, "Isoline.ico"),
              sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

    # a flat version for the README header
    docs = os.path.join(here, "docs")
    os.makedirs(docs, exist_ok=True)
    icon.resize((256, 256), Image.LANCZOS).save(os.path.join(docs, "logo.png"))

    print("wrote", out)


if __name__ == "__main__":
    main()
