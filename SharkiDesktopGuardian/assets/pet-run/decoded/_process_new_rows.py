"""One-off tool: convert the 4 raw Gemini grid sheets into atlas-ready
192x208 horizontal strips (8 columns, unused trailing cells transparent),
matching the existing spritesheet-extended row convention.
"""
from PIL import Image

CELL_W, CELL_H = 192, 208
ROW_W, ROW_H = CELL_W * 8, CELL_H
TARGET_CONTENT_HEIGHT_RATIO = 0.90  # fraction of CELL_H the character should fill

JOBS = [
    # name, source file, cols, rows, explicit_cells (list of (col,row)) or None to sample evenly, frame_count if sampling
    ("carrying-box", "raw-carrying-box.jpg", 4, 3, None, 8),
    ("stuffing-box", "raw-stuffing-box.jpg", 3, 2, None, None),
    ("wiping-sweat", "raw-wiping-sweat.jpg", 3, 2, None, None),
    # on-fire is a 3x3 grid; only these cells actually show flames (col,row), 0-indexed
    ("on-fire", "raw-on-fire.jpg", 3, 3, [(0, 1), (1, 1), (2, 1), (1, 2), (2, 2)], None),
]


def chroma_key(cell_rgb):
    cell_rgb = cell_rgb.convert("RGB")
    w, h = cell_rgb.size
    px = cell_rgb.load()
    out = Image.new("RGBA", (w, h))
    opx = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            greenness = g - max(r, b)
            if greenness >= 55:
                alpha = 0
            elif greenness <= 12:
                alpha = 255
            else:
                alpha = int(255 * (55 - greenness) / (55 - 12))
            if alpha > 0 and g > max(r, b):
                g = max(r, b)
            opx[x, y] = (r, g, b, alpha)
    return out


def extract_cell(im, col, row, cols, rows, inset=0.02):
    w, h = im.size
    cw, ch = w / cols, h / rows
    # small inward inset to avoid grid divider lines/seams bleeding into the crop
    ix, iy = cw * inset, ch * inset
    box = (
        round(col * cw + ix), round(row * ch + iy),
        round((col + 1) * cw - ix), round((row + 1) * ch - iy),
    )
    return im.crop(box)


def content_bbox(rgba):
    alpha = rgba.split()[3]
    bbox = alpha.getbbox()
    return bbox


def fit_into_cell(cell_rgba):
    bbox = content_bbox(cell_rgba)
    if bbox is None:
        return Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    content = cell_rgba.crop(bbox)
    cw, ch = content.size
    target_h = CELL_H * TARGET_CONTENT_HEIGHT_RATIO
    scale = min(target_h / ch, (CELL_W * 0.92) / cw)
    new_w, new_h = max(1, round(cw * scale)), max(1, round(ch * scale))
    resized = content.resize((new_w, new_h), Image.LANCZOS)
    canvas = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    x = (CELL_W - new_w) // 2
    y = CELL_H - new_h - 8  # bottom-anchored, small foot margin
    canvas.paste(resized, (x, y), resized)
    return canvas


def sample_indices(total, count):
    if count is None or count >= total:
        return list(range(total))
    return sorted({round(i * (total - 1) / (count - 1)) for i in range(count)})[:count]


for name, filename, cols, rows, explicit_cells, frame_count in JOBS:
    src = Image.open(filename)
    if explicit_cells is not None:
        chosen = explicit_cells
    else:
        total = cols * rows
        all_cells = [(c, r) for r in range(rows) for c in range(cols)]
        indices = sample_indices(total, frame_count)
        chosen = [all_cells[i] for i in indices]

    strip = Image.new("RGBA", (ROW_W, ROW_H), (0, 0, 0, 0))
    used_flags = []
    for slot in range(8):
        if slot < len(chosen):
            col, row = chosen[slot]
            raw_cell = extract_cell(src, col, row, cols, rows)
            keyed = chroma_key(raw_cell)
            fitted = fit_into_cell(keyed)
            strip.paste(fitted, (slot * CELL_W, 0), fitted)
            used_flags.append(True)
        else:
            used_flags.append(False)

    out_path = f"row-{name}.png"
    strip.save(out_path)
    print(f"{name}: {len(chosen)} frames used, saved {out_path}, used flags={used_flags}")

print("done")
