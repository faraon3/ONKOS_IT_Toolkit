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
