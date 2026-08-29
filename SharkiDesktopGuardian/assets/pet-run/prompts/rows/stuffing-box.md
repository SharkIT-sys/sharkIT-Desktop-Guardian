Create one horizontal animation strip for Codex pet `sharki-desktop`, state `stuffing-box`.

Use the attached canonical base for identity. Use the attached layout guide only for slot count, spacing, centering, and padding; do not draw the guide.

Output exactly 6 full-body frames in one left-to-right row on flat pure user-selected #00FF00. Treat the row as 6 invisible equal-width slots: one centered complete pose per slot, evenly spaced, with no overlap, clipping, empty slots, labels, or borders.

Identity: same pet in every frame: Humanoide tecnológico compacto con capucha y armadura rojo carmesí, ribetes dorados, visor o rostro oscuro, ojos estrechos luminosos azul cian y guanteletes con núcleos cian. Silueta amistosa, firme y legible; conservar exactamente estos rasgos identificadores.. Preserve silhouette, face, proportions, markings, palette, material, style, and props.
Style: Pet-safe sprite: compact full-body mascot, readable in a 192x208 cell, clear silhouette, simple face, stable palette/materials, and crisp edges for chroma-key extraction. Style `3d-toy`: Stylized 3D toy mascot with smooth rounded forms, simple materials, clear silhouette, and no photoreal complexity. User style notes: Figura 3D estilizada de escritorio, cuerpo completo compacto, proporciones ligeramente chibi sin aspecto infantil, materiales metálicos limpios, alto contraste y detalles grandes legibles a 192x208..
Animation continuity: keep apparent pet scale and baseline stable within the row; this is an in-place pushing loop, feet planted, only the arms/torso animate.

State action: Low disk space loop (`PetState.LowDisk`): the pet leans forward pushing down on the lid of a crate that is overfilled and bulging, straining to force it shut.

State requirements:
- Show a single crate at chest height, visibly overstuffed (lid slightly ajar or bulging), with the pet's both hands/gauntlets pressed down on top of it.
- Convey struggle through a forward lean and compressed pose; the crate must stay attached to/overlapping the pet silhouette in every frame.
- Small motion between frames (press down, ease back slightly) is allowed; do not turn it into a walk cycle or show the crate opening fully.
- No sweat, no fire, no other props besides the single overfilled crate.

Clean extraction: crisp opaque edges, safe padding, no scenery, text, guide marks, checkerboard, shadows, glows, motion blur, speed lines, dust, detached effects, stray pixels, or chroma-key colors inside the pet.
