using ClosedXML.Excel;

var outputPath = args.Length > 0
    ? args[0]
    : Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "database", "demo_servicios.xlsx");

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

using var wb = new XLWorkbook();
var ws = wb.AddWorksheet("Servicios");

// Headers
var headers = new[] {
    "id_servicio", "numero_medidor", "Marca", "diametro", "direccion",
    "nombre", "coordenadax", "coordenaday", "lote", "localidad",
    "ruta", "libreta", "observacion_libre"
};
for (int i = 0; i < headers.Length; i++)
{
    ws.Cell(1, i + 1).Value = headers[i];
    ws.Cell(1, i + 1).Style.Font.Bold = true;
    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
}

// Datos demo: 50 servicios simulando una campaña real
var marcas = new[] { "Actaris", "Elster", "Itron", "Zenner", "Sensus" };
var diametros = new[] { "13", "15", "20", "25" };
var localidades = new[] { "La Serena", "Coquimbo", "Ovalle" };
var rutas = new[] { "R01", "R02", "R03", "R04", "R05" };
var lotes = new[] { "L2024-001", "L2024-002" };
var calles = new[] {
    "Av. Francisco de Aguirre", "Calle Los Carrera", "Av. del Mar",
    "Calle Balmaceda", "Av. Costanera", "Calle Colón",
    "Calle O'Higgins", "Av. La Paz", "Pasaje Los Álamos",
    "Calle Prat", "Av. Vicuña Mackenna", "Calle Benavente"
};
var nombres = new[] {
    "Juan Pérez", "María González", "Carlos Muñoz", "Ana Rojas",
    "Pedro Soto", "Laura Torres", "Diego Ramírez", "Sofía López",
    "Fernando Díaz", "Catalina Herrera", "Roberto Silva", "Camila Vargas",
    "Andrés Morales", "Valentina Flores", "Martín Contreras"
};

var rng = new Random(42); // seed fijo para reproducibilidad

for (int row = 0; row < 50; row++)
{
    int r = row + 2;
    var localidad = localidades[row % localidades.Length];

    // Coordenadas centradas en La Serena/Coquimbo
    double baseX = localidad switch
    {
        "La Serena" => -71.2520,
        "Coquimbo"  => -71.3437,
        "Ovalle"    => -71.1996,
        _ => -71.25
    };
    double baseY = localidad switch
    {
        "La Serena" => -29.9027,
        "Coquimbo"  => -29.9533,
        "Ovalle"    => -30.5983,
        _ => -29.95
    };

    ws.Cell(r, 1).Value  = $"SRV-{(row + 1):D4}";                                  // id_servicio
    ws.Cell(r, 2).Value  = $"{rng.Next(100000, 999999)}";                           // numero_medidor
    ws.Cell(r, 3).Value  = marcas[rng.Next(marcas.Length)];                         // marca
    ws.Cell(r, 4).Value  = diametros[rng.Next(diametros.Length)];                   // diametro
    ws.Cell(r, 5).Value  = $"{calles[rng.Next(calles.Length)]} {rng.Next(100, 999)}"; // direccion
    ws.Cell(r, 6).Value  = nombres[rng.Next(nombres.Length)];                       // nombre
    ws.Cell(r, 7).Value  = Math.Round(baseX + rng.NextDouble() * 0.02 - 0.01, 6);  // coordenadax
    ws.Cell(r, 8).Value  = Math.Round(baseY + rng.NextDouble() * 0.02 - 0.01, 6);  // coordenaday
    ws.Cell(r, 9).Value  = lotes[row < 25 ? 0 : 1];                                // lote
    ws.Cell(r, 10).Value = localidad;                                               // localidad
    ws.Cell(r, 11).Value = rutas[row % rutas.Length];                               // ruta
    ws.Cell(r, 12).Value = $"LIB-{(row / 10 + 1):D2}";                             // libreta
    ws.Cell(r, 13).Value = row % 7 == 0 ? "Requiere verificación especial" : "";    // observacion_libre
}

// Autoajustar columnas
ws.Columns().AdjustToContents();

wb.SaveAs(outputPath);
Console.WriteLine($"Excel generado: {outputPath}");
Console.WriteLine($"Filas de datos: 50");
