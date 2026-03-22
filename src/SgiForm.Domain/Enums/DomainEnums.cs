namespace SgiForm.Domain.Enums;

public enum EstadoUsuario { Activo, Inactivo, Bloqueado }

public enum TipoControl
{
    TextoCorto, TextoLargo, Entero, Decimal, Fecha, Hora, FechaHora,
    SiNo, SeleccionUnica, SeleccionMultiple, Lista,
    FotoUnica, FotosMultiples, Coordenadas, Firma,
    Calculado, Etiqueta, Checkbox, QrCodigo, Archivo
}

public enum OperadorRegla
{
    Eq, Neq, Gt, Lt, Gte, Lte,
    Contains, NotContains, In, NotIn,
    IsEmpty, IsNotEmpty, StartsWith, EndsWith
}

public enum AccionRegla
{
    Mostrar, Ocultar, Obligatorio, Opcional,
    SaltarSeccion, BloquearCierre, Calcular,
    AsignarValor, MinFotos, MaxFotos
}

public enum EstadoFlujoVersion { Borrador, Publicado, Archivado }

public enum EstadoImportacion
{
    Pendiente, Procesando, Completado,
    CompletadoConErrores, Fallido
}

public enum EstadoAsignacion
{
    Pendiente, Asignada, Descargada, EnEjecucion,
    Finalizada, Sincronizada, Observada, Rechazada, Cerrada
}

public enum EstadoInspeccion
{
    Borrador, EnProgreso, Completada, Enviada,
    Aprobada, Observada, Rechazada
}

public enum TipoSync { Download, Upload, Photos, Confirm, Full }

public enum Prioridad { Baja, Normal, Alta, Urgente }
