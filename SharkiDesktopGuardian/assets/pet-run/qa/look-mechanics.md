# Mecánica de mirada de Sharki

## Construcción física

Sharki es un humanoide tecnológico compacto: botas y pelvis forman la base estable; el torso lleva armadura rígida; la cabeza está separada dentro de una capucha; el visor oscuro contiene dos ojos luminosos estrechos; los guanteletes son rígidos y están unidos a los antebrazos.

## Partes ancladas y liderazgo

- Las botas, la pelvis y el centro inferior del torso conservan posición, escala y línea base en las 16 direcciones.
- Los ojos lideran mediante el desplazamiento coherente de su luz dentro del visor, sin convertirse en pupilas redondas ni ojos superpuestos.
- La cabeza gira o inclina de forma contenida dentro de la capucha; el aro dorado y la apertura del visor cambian de perspectiva con ella.
- Cuello, hombros y pecho acompañan con un giro muy pequeño. Los guanteletes permanecen unidos y solo compensan el peso, sin cambiar de lado ni flotar.
- No se rota, inclina, sesga ni deforma el sprite completo.

## Cardinales

- `000 arriba`: barbilla elevada, parte inferior del visor algo más visible, ojos cerca del borde superior del visor y apertura de capucha orientada hacia arriba; botas y pelvis fijas.
- `090 derecha de pantalla`: nariz/centro implícito del visor y luces oculares se desplazan claramente a la derecha del centro de la cabeza; se ve más el lado izquierdo físico de capucha y torso, con el lado derecho parcialmente ocluido.
- `180 abajo`: barbilla recogida hacia el pecho, visera superior más dominante y ojos cerca del borde inferior del visor; hombros acompañan levemente.
- `270 izquierda de pantalla`: luces y centro implícito del visor se desplazan claramente a la izquierda del centro de la cabeza; se ve más el lado derecho físico de capucha y torso, con el lado izquierdo parcialmente ocluido.

## Continuidad y presupuesto de movimiento

Cada paso de 22,5 grados mueve ojos, cabeza, aro de capucha y hombros aproximadamente una cuarta parte del recorrido entre cardinales. La base no se desplaza. Los cambios de oclusión progresan gradualmente; ningún guantelete salta de lado, el tamaño de cabeza no cambia y `337.5` queda a un solo paso de `000`.

## Identidad protegida

Se conservan capucha y armadura rojo carmesí, ribetes dorados, visor negro, ojos cian estrechos, guanteletes con núcleos cian y proporciones 3D tipo figura. Sin ojos redondos nuevos, texto, símbolos, sombras, efectos separados ni accesorios.
