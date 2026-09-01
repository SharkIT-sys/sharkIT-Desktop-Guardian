# Mummy

Mascota alternativa Mummy proporcionada como hoja JPEG de 504×1024 píxeles.

La aplicación usa el mismo contrato visual que Sharki: 8 columnas, 15 filas y celdas de 192×208. La conversión conserva la posición y escala de la hoja completa, elimina el fondo negro y genera un atlas RGBA de 1536×3120.

Estados utilizados:

- fila 0: 7 poses de origen; la aplicación mantiene la primera fija en reposo;
- filas 1 y 2: movimiento, 8 fotogramas por dirección;
- fila 3: saludo, 4 fotogramas;
- fila 6: espera, 6 fotogramas;
- filas 11 a 14: carga, disco, memoria y temperatura.

El archivo original se conserva como `source.jpg`. El atlas de ejecución se guarda en `src/SharkiDesktopGuardian/Assets/Pets/Mummy/spritesheet.png`; la validación visual y las animaciones de revisión se generan localmente en `qa/` y no se versionan. Las filas 9 y 10 de la fuente son poses de consulta que la aplicación no utiliza; se dejan transparentes en el atlas para no incorporar una animación discontinua que no puede reproducirse.

Regeneración:

```powershell
python tools/build_mummy_atlas.py --source assets/pets/mummy/source.jpg --output src/SharkiDesktopGuardian/Assets/Pets/Mummy/spritesheet.png --qa-dir assets/pets/mummy/qa
```
