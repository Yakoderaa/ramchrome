# RAMChrome + RAMDiscord

Utilidades ligeras para Windows que ayudan a reducir el conjunto de trabajo de memoria de Chrome y Discord.

## RAMChrome
- Complemento opcional para descartar pestañas inactivas.
- Aplicación de Windows con optimización manual y periódica, estado visible, bandeja del sistema e inicio con Windows.
- Actualización desde GitHub Releases con verificación SHA-256.

## RAMDiscord
- Aplicación de Windows para medir procesos y memoria de Discord Stable, PTB y Canary.
- Optimización manual y automática del conjunto de trabajo de los procesos detectados.
- Botón **Establecer automático / Desactivar automático**, intervalo configurable, estado de última ejecución.
- Cerrar con la X oculta la ventana en la bandeja; el menú permite abrir, optimizar o salir.
- Opción para iniciar con Windows minimizado.
- Actualizaciones desde releases con etiqueta `discord-v*`, verificadas mediante SHA-256.

**Nota:** estas aplicaciones no cierran Discord ni eliminan sus procesos. La optimización del conjunto de trabajo puede reducir la RAM física usada en ese momento, pero Windows o Discord pueden volver a cargar memoria; no equivale a una función oficial de descarga de pestañas ni garantiza una reducción permanente.

## Desarrollo
Requiere .NET 8 SDK y Windows. Los builds se publican como artefactos de GitHub Actions.
