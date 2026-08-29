Create one horizontal animation strip for Codex pet `sharki-desktop`, state `wiping-sweat`.

Use the attached canonical base for identity. Use the attached layout guide only for slot count, spacing, centering, and padding; do not draw the guide.

Output exactly 6 full-body frames in one left-to-right row on flat pure user-selected #00FF00. Treat the row as 6 invisible equal-width slots: one centered complete pose per slot, evenly spaced, with no overlap, clipping, empty slots, labels, or borders.

Identity: same pet in every frame: Humanoide tecnológico compacto con capucha y armadura rojo carmesí, ribetes dorados, visor o rostro oscuro, ojos estrechos luminosos azul cian y guanteletes con núcleos cian. Silueta amistosa, firme y legible; conservar exactamente estos rasgos identificadores.. Preserve silhouette, face, proportions, markings, palette, material, style, and props.
Style: Pet-safe sprite: compact full-body mascot, readable in a 192x208 cell, clear silhouette, simple face, stable palette/materials, and crisp edges for chroma-key extraction. Style `3d-toy`: Stylized 3D toy mascot with smooth rounded forms, simple materials, clear silhouette, and no photoreal complexity. User style notes: Figura 3D estilizada de escritorio, cuerpo completo compacto, proporciones ligeramente chibi sin aspecto infantil, materiales metálicos limpios, alto contraste y detalles grandes legibles a 192x208..
Animation continuity: keep apparent pet scale and baseline stable within the row; this is an in-place loop, feet planted, only the head/arm animate.

State action: High RAM usage loop (`PetState.HighMemory`): the pet looks overworked and wipes its brow/visor with one arm, as if wiping away sweat.

State requirements:
- Show one arm raised to the head/visor in a wiping motion, with a couple of small sweat-drop shapes attached to or overlapping the head silhouette (not floating separately).
- Slightly slumped shoulders and a tired posture are allowed to reinforce "overworked", but keep the pose readable and friendly, not distressed.
- The wiping arm should move across 2-3 of the frames and return to a resting tired pose for the rest of the loop.
- No crates, no fire, no other props besides the small attached sweat drops.

Clean extraction: crisp opaque edges, safe padding, no scenery, text, guide marks, checkerboard, shadows, glows, motion blur, speed lines, dust, detached effects, stray pixels, or chroma-key colors inside the pet.
