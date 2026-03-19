using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.RegularExpressions;

namespace SanitasField.Infrastructure.Persistence;

/// <summary>
/// Convierte valores de enum C# (PascalCase) a snake_case para compatibilidad
/// con los tipos ENUM nativos de PostgreSQL definidos en el schema SQL.
/// 
/// Ejemplo:
///   EstadoUsuario.Activo      → "activo"
///   EstadoAsignacion.EnEjecucion  → "en_ejecucion"
///   TipoControl.FotosMultiples    → "fotos_multiples"
/// </summary>
public class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(
            v => ToSnakeCase(v.ToString()),
            v => ParseFromSnakeCase(v))
    {
    }

    private static string ToSnakeCase(string value)
    {
        // PascalCase / camelCase → snake_case
        var result = Regex.Replace(value, @"([a-z0-9])([A-Z])", "$1_$2");
        return result.ToLowerInvariant();
    }

    private static TEnum ParseFromSnakeCase(string value)
    {
        // Intentar parseo directo primero (ej "activo" → Activo)
        // Convierte snake_case → PascalCase para parsear
        var pascal = string.Concat(
            value.Split('_')
                 .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

        if (Enum.TryParse<TEnum>(pascal, ignoreCase: true, out var result))
            return result;

        // Fallback: buscar por nombre ignorando case y guiones
        foreach (TEnum enumValue in Enum.GetValues<TEnum>())
        {
            var snake = ToSnakeCase(enumValue.ToString());
            if (string.Equals(snake, value, StringComparison.OrdinalIgnoreCase))
                return enumValue;
        }

        throw new InvalidOperationException(
            $"No se puede convertir '{value}' al enum {typeof(TEnum).Name}");
    }
}
