using System.Globalization;

namespace SanitasField.Mobile.Converters;

/// <summary>Retorna true si el string no es null ni vacío.</summary>
public class StringNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value?.ToString());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Invierte un booleano.</summary>
public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>true -> "Ingresando..." / false -> "Iniciar sesión"</summary>
public class BoolToLoginTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Ingresando..." : "Iniciar sesión";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>true -> verde (conectado) / false -> rojo (sin conexión)</summary>
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Color.FromArgb("#0e9f6e") : Color.FromArgb("#e02424");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>true -> "Conectado" / false -> "Sin conexión"</summary>
public class BoolToConexionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Conectado" : "Sin conexión";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>true -> "Sincronizando..." / false -> "Sincronizar"</summary>
public class BoolToSyncTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Sincronizando..." : "Sincronizar";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Convierte el estado de asignación a un color de badge.</summary>
public class EstadoColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "pendiente" => Color.FromArgb("#c27803"),   // amber
            "asignada" => Color.FromArgb("#1a56db"),     // blue
            "descargada" => Color.FromArgb("#6b7280"),   // gray
            "en_ejecucion" => Color.FromArgb("#0e9f6e"), // green
            "finalizada" => Color.FromArgb("#047857"),    // dark green
            "sincronizada" => Color.FromArgb("#6366f1"),  // indigo
            "observada" => Color.FromArgb("#e02424"),     // red
            "rechazada" => Color.FromArgb("#991b1b"),     // dark red
            "cerrada" => Color.FromArgb("#374151"),       // dark gray
            _ => Color.FromArgb("#9ca3af")                // default gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>int (0-100) -> double (0.0-1.0) para ProgressBar.</summary>
public class IntToProgressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? i / 100.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>true (es última sección) -> "Cerrar" / false -> "Siguiente"</summary>
public class BoolToNavTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Cerrar inspección" : "Siguiente";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Retorna el comando apropiado según si es la última sección.
/// Nota: Este converter es un placeholder — la lógica real debería
/// manejarse en el ViewModel con un solo comando que evalúe EsUltimaSeccion.
/// </summary>
public class BoolToCommandConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null; // El binding de Command se resolverá por ViewModel

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
