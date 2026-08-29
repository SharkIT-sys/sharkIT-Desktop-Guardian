Create one horizontal animation strip for Codex pet `sharki-desktop`, state `on-fire`.

Use the attached canonical base for identity. Use the attached layout guide only for slot count, spacing, centering, and padding; do not draw the guide.

Output exactly 8 full-body frames in one left-to-right row on flat pure user-selected #00FF00. Treat the row as 8 invisible equal-width slots: one centered complete pose per slot, evenly spaced, with no overlap, clipping, empty slots, labels, or borders.

Identity: same pet in every frame: Humanoide tecnológico compacto con capucha y armadura rojo carmesí, ribetes dorados, visor o rostro oscuro, ojos estrechos luminosos azul cian y guanteletes con núcleos cian. Silueta amistosa, firme y legible; conservar exactamente estos rasgos identificadores.. Preserve silhouette, face, proportions, markings, palette, material, style, and props. Note: the pet's own eye color switches to red for this critical state in-app via a separate overlay layer, so keep the base eyes as drawn here; do not recolor them yourself.
Style: Pet-safe sprite: compact full-body mascot, readable in a 192x208 cell, clear silhouette, simple face, stable palette/materials, and crisp edges for chroma-key extraction. Style `3d-toy`: Stylized 3D toy mascot with smooth rounded forms, simple materials, clear silhouette, and no photoreal complexity. User style notes: Figura 3D estilizada de escritorio, cuerpo completo compacto, proporciones ligeramente chibi sin aspecto infantil, materiales metálicos limpios, alto contraste y detalles grandes legibles a 192x208..
Animation continuity: keep apparent pet scale and baseline stable within the row; this is an urgent in-place loop, feet planted, flames flicker frame to frame.

State action: Critical temperature alert loop (`PetState.ThermalAlert`): the pet stands alarmed, fully surrounded by small stylized flames rising from around its feet/body.

State requirements:
- Small stylized flame shapes (orange/yellow, toy-style, not photoreal) rising around the pet's feet and lower body, overlapping the silhouette, never floating separate from it.
- Alarmed but still readable pose: arms slightly raised or braced, upright stance, not falling over.
- Flame shapes should flicker/vary in size and position between frames to read as animated fire.
- No crates, no sweat drops, no other props besides the attached flames.

Clean extraction: crisp opaque edges, safe padding, no scenery, text, guide marks, checkerboard, shadows unrelated to the flames, motion blur, speed lines, dust, or chroma-key colors inside the pet.
