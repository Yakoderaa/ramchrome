# RAMChrome

Optimizador ligero de memoria para Google Chrome en Windows.

Componentes:
- Chrome Extension: detecta pestañas inactivas y permite descartarlas para liberar memoria.
- RAMChrome Desktop: panel de Windows para medir Chrome, ejecutar optimizaciones y comprobar/instalar actualizaciones desde GitHub Releases.
- Updater: pequeño ejecutable independiente que reemplaza la aplicación después de que esta se cierre.

Actualizaciones:
La aplicación consulta el endpoint latest release de GitHub para detectar nuevas versiones, descarga el asset RAMChrome.zip, verifica su SHA-256 y ejecuta el updater para instalarlo.

Desarrollo:
Requiere .NET 8 SDK y Windows.
- desktop/RAMChrome/RAMChrome.csproj
- desktop/RAMChrome.Updater/RAMChrome.Updater.csproj
- extension/
