# ONKOS IT Toolkit

Aplicación de escritorio portable para soporte técnico local en Clínica Onkos.

## Objetivo
Ejecutarse desde una memoria USB, sin depender de una página web ni de una conexión permanente a internet. GitHub se utiliza para mantener el código y compilar versiones descargables.

## Módulos incluidos

### 01 · Backup y migración
- Selección granular de carpetas personales (Escritorio, Documentos, Descargas, etc.).
- Agregar carpetas personalizadas de cualquier disco.
- Agregar perfiles de **otros usuarios de Windows** (útil antes de formatear el PC de otra persona).
- Exportación opcional de la configuración IPv4 con `netsh`.
- Copia con **verificación de existencia y tamaño** por archivo.
- **Cancelación** en cualquier momento del backup.
- **Validación de espacio libre** antes de iniciar la copia.
- Preserva carpetas vacías y evita ciclos por enlaces simbólicos.
- Genera `MANIFIESTO_BACKUP.csv`, `INFORME_ERRORES_BACKUP.txt` y `BACKUP_INFO.txt`.
- Incluye la biblioteca propia `Data/` (drivers y programas) si se marca la opción.
- Restauración con **remapeo de perfiles** (`C:\Users\Pedro\Documents` → `C:\Users\Nuevo\Documents`), sobrescritura opcional y reaplicación de IPv4.

### 02 · Programas
- Biblioteca local de instaladores `.exe` y `.msi`.
- Agregar ejecutables portables (solo el `.exe` seleccionado).
- Personalizar nombre y logo de cada programa.
- Ejecución múltiple de instaladores uno a uno con UAC (`runas`).
- Soporte MSI vía `msiexec`.

### 03 · Impresoras
- Biblioteca local de instaladores de drivers.
- Detección automática del instalador principal por carpeta.
- Instalación `.exe` / `.msi` con `runas`.
- Instalación `.inf` vía `pnputil /add-driver ... /install`.
- Personalización de nombre y logo.

### 04 · Inventario
- Recolecta información del equipo vía WMI:
  - Sistema operativo, equipo, procesador, discos, red e impresoras instaladas.
- Exportación a **CSV** en `Data/Reports/Inventario_<equipo>_<fecha>.csv`.
- Útil para diagnóstico rápido y para adjuntar al ticket de soporte.

## Estructura prevista de la USB
USB_ONKOS/
ONKOS_IT_Toolkit.exe
Data/
Config/
Programs/
Drivers/
Templates/
Reports/

La aplicación detecta su propia ubicación y utiliza carpetas relativas a ella. Las copias de seguridad pueden guardarse en otra unidad o carpeta elegida por el técnico.

## Compatibilidad
- **Windows 10 y 11 (x64):** compatibilidad completa.
- **Windows 8.1:** funciona con .NET 8 Desktop Runtime instalado, o con la versión `self-contained` incluida en el artefacto.
- **Windows 7:** **no soportado** por .NET 8. Si se requiere, se debe recompilar como multi-target `net48` con mantenimiento adicional.

La publicación oficial es `self-contained win-x64` single-file (`PublishReadyToRun` habilitado para acelerar el arranque desde USB). **No requiere instalar .NET** en el equipo destino en Windows 10/11.

## Seguridad
- No formatear ni eliminar particiones.
- Confirmar cambios de red y operaciones administrativas.
- No almacenar contraseñas, tokens ni credenciales de usuarios.
- Tratar los respaldos como información sensible y usar únicamente destinos autorizados.
- La restauración de IPv4 requiere ejecutar ONKOS como administrador.

## Desarrollo
El código se compila en GitHub Actions sobre Windows. No se requiere Visual Studio en el equipo del usuario.

### Versión
La versión se define en `ONKOS.IT.Toolkit.csproj` (`<Version>`, `<FileVersion>`, `<AssemblyVersion>`).

### Publicar una nueva versión
1. Subir cambios a `main` (o mergear PR).
2. GitHub Actions ejecuta `build.yml` y produce el artefacto `ONKOS-IT-Toolkit-win-x64`.
3. Descargar el artefacto desde la pestaña **Actions** del repositorio.
4. Copiar el contenido de `publish/win-x64/` a la raíz de la USB.
5. Copiar la carpeta `Data/` existente (si se quiere conservar la biblioteca local de drivers/programas).
