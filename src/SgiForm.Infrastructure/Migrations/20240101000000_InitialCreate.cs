using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SgiForm.Infrastructure.Migrations
{
    /// <summary>
    /// Migración base vacía.
    /// El schema real fue creado mediante los scripts SQL en /database/.
    /// Esta migración existe únicamente para que EF Core tenga un punto de partida
    /// y pueda rastrear migraciones futuras sin intentar recrear el schema existente.
    /// 
    /// Para aplicar: dotnet ef database update
    /// Esto solo registra esta migración en __EFMigrationsHistory, sin ejecutar DDL.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Schema ya existe — creado por database/01_schema.sql + 02_seed.sql
            // No se genera DDL aquí para no entrar en conflicto con el schema existente.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No revertir — el schema base se gestiona con los scripts SQL
        }
    }
}
