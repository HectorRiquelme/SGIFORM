-- Filtro Lua para Pandoc: convierte bloques ```mermaid``` a imágenes PNG via mmdc
-- Uso: pandoc input.md -o output.pdf --lua-filter=tools/mermaid-filter.lua
-- Ejecutar desde la raíz del proyecto (kobotoolbox/)

local counter = 0
local base_dir = "tools\\mermaid-images"

-- Crear directorio de salida si no existe
os.execute('mkdir "' .. base_dir .. '" 2>nul')

function CodeBlock(block)
    if block.classes[1] == "mermaid" then
        counter = counter + 1
        local input_file = base_dir .. "\\mermaid_" .. counter .. ".mmd"
        local output_file = base_dir .. "\\mermaid_" .. counter .. ".png"

        -- Escribir el código mermaid a un archivo temporal
        local f = io.open(input_file, "w")
        if not f then
            io.stderr:write("ERROR: No se pudo crear " .. input_file .. "\n")
            block.classes = {}
            return block
        end
        f:write(block.text)
        f:close()

        -- Ejecutar mmdc para generar la imagen
        local cmd = string.format(
            'mmdc -i "%s" -o "%s" -b white -w 1200 2>&1',
            input_file, output_file
        )
        io.stderr:write("Renderizando diagrama mermaid #" .. counter .. "...\n")
        local handle = io.popen(cmd)
        local result = handle:read("*a")
        handle:close()

        -- Verificar que el archivo se generó
        local check = io.open(output_file, "r")
        if check then
            check:close()
            -- Reemplazar el bloque de código por la imagen
            return pandoc.Para({
                pandoc.Image({}, output_file, "")
            })
        else
            io.stderr:write("WARN: No se pudo renderizar diagrama #" .. counter .. ": " .. result .. "\n")
            block.classes = {}
            return block
        end
    end
end
