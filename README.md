# ONKOS IT Toolkit

Aplicación de escritorio portable para soporte técnico local en Clínica Onkos.

## Objetivo
Ejecutarse desde una memoria USB, sin depender de una página web ni de una conexión permanente a internet. GitHub se utiliza para mantener el código y compilar versiones descargables.

## Estructura prevista de la USB
```
USB_ONKOS/
  ONKOS_IT_Toolkit.exe
  Data/
    Config/
    Programs/
    Drivers/
    Templates/
    Reports/
```

La aplicación detectará su propia ubicación y utilizará carpetas relativas a ella. Las copias de seguridad podrán guardarse en otra unidad o carpeta elegida por el técnico.

## Compatibilidad
La compatibilidad con Windows 7, 8.1, 10 y 11 se validará por versiones y funciones. No se debe asumir que todas las versiones de Windows admiten las mismas APIs o runtimes.

## Seguridad
- No formatear ni eliminar particiones.
- Confirmar cambios de red y operaciones administrativas.
- No almacenar contraseñas, tokens ni credenciales de usuarios.
- Tratar los respaldos como información sensible y usar únicamente destinos autorizados.

## Desarrollo
El código se compila en GitHub Actions sobre Windows. No se requiere Visual Studio en el equipo del usuario.
