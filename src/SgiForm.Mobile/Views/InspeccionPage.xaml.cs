using SgiForm.Mobile.Models;
using SgiForm.Mobile.ViewModels;

namespace SgiForm.Mobile.Views;

[QueryProperty(nameof(AsignacionId), "asignacion_id")]
public partial class InspeccionPage : ContentPage
{
    private readonly InspeccionViewModel _vm;

    public string? AsignacionId
    {
        get => _vm.AsignacionId;
        set => _vm.AsignacionId = value;
    }

    public InspeccionPage(InspeccionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.InicializarAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"InspeccionPage error: {ex.Message}"); }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_vm.PreguntasActuales))
            MainThread.BeginInvokeOnMainThread(RebuildPreguntas);
        if (e.PropertyName == nameof(_vm.MensajeError))
            MainThread.BeginInvokeOnMainThread(UpdateError);
    }

    private void UpdateError()
    {
        ErrorLabel.Text = _vm.MensajeError;
        ErrorFrame.IsVisible = !string.IsNullOrEmpty(_vm.MensajeError);
    }

    private void RebuildPreguntas()
    {
        PreguntasContainer.Children.Clear();
        foreach (var pregunta in _vm.PreguntasActuales)
        {
            if (!pregunta.VisibleRuntime) continue;
            var card = BuildPreguntaCard(pregunta);
            PreguntasContainer.Children.Add(card);
        }
    }

    private View BuildPreguntaCard(PreguntaLocal p)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };

        // Question label
        var label = new Label
        {
            Text = p.Texto,
            FontSize = 14,
            TextColor = Color.FromArgb("#374151"),
            FontAttributes = p.ObligatorioRuntime ? FontAttributes.Bold : FontAttributes.None
        };
        stack.Children.Add(label);

        // Input control based on TipoControl
        var control = p.TipoControl switch
        {
            "si_no" => BuildSiNoControl(p),
            "texto_corto" => BuildEntryControl(p, Keyboard.Default, false),
            "texto_largo" => BuildEditorControl(p),
            "decimal" => BuildEntryControl(p, Keyboard.Numeric, false),
            "entero" => BuildEntryControl(p, Keyboard.Numeric, false),
            "numero" => BuildEntryControl(p, Keyboard.Numeric, false),
            "seleccion_unica" => BuildSeleccionUnicaControl(p),
            "seleccion_multiple" => BuildSeleccionMultipleControl(p),
            "fecha" => BuildFechaControl(p),
            "foto_unica" => BuildFotoControl(p, false),
            "fotos_multiples" => BuildFotoControl(p, true),
            "coordenadas" => BuildCoordenadasControl(p),
            "firma" => BuildFirmaControl(p),
            _ => new Label { Text = $"[{p.TipoControl}]", TextColor = Colors.Gray, FontSize = 12 }
        };

        stack.Children.Add(control);

        return new Frame
        {
            CornerRadius = 8,
            Padding = 12,
            BackgroundColor = Colors.White,
            Content = stack
        };
    }

    // ── SI/NO ────────────────────────────────────────────────────────────────
    private View BuildSiNoControl(PreguntaLocal p)
    {
        var existing = _vm.GetRespuesta(p.Id);
        bool? current = existing?.ValorBooleano;

        var btnSi = new Button
        {
            Text = "Sí",
            CornerRadius = 8,
            HeightRequest = 44,
            BackgroundColor = current == true ? Color.FromArgb("#16a34a") : Color.FromArgb("#f3f4f6"),
            TextColor = current == true ? Colors.White : Color.FromArgb("#374151"),
            FontSize = 15
        };
        var btnNo = new Button
        {
            Text = "No",
            CornerRadius = 8,
            HeightRequest = 44,
            BackgroundColor = current == false ? Color.FromArgb("#dc2626") : Color.FromArgb("#f3f4f6"),
            TextColor = current == false ? Colors.White : Color.FromArgb("#374151"),
            FontSize = 15
        };

        btnSi.Clicked += (_, _) =>
        {
            btnSi.BackgroundColor = Color.FromArgb("#16a34a"); btnSi.TextColor = Colors.White;
            btnNo.BackgroundColor = Color.FromArgb("#f3f4f6"); btnNo.TextColor = Color.FromArgb("#374151");
            _vm.ResponderPregunta(p.Id, true);
            MainThread.BeginInvokeOnMainThread(RebuildPreguntas);
        };
        btnNo.Clicked += (_, _) =>
        {
            btnNo.BackgroundColor = Color.FromArgb("#dc2626"); btnNo.TextColor = Colors.White;
            btnSi.BackgroundColor = Color.FromArgb("#f3f4f6"); btnSi.TextColor = Color.FromArgb("#374151");
            _vm.ResponderPregunta(p.Id, false);
            MainThread.BeginInvokeOnMainThread(RebuildPreguntas);
        };

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Add(btnSi, 0, 0);
        grid.Add(btnNo, 1, 0);
        return grid;
    }

    // ── TEXTO CORTO / DECIMAL ────────────────────────────────────────────────
    private View BuildEntryControl(PreguntaLocal p, Keyboard kb, bool isPassword)
    {
        var existing = _vm.GetRespuesta(p.Id);
        var entry = new Entry
        {
            Keyboard = kb,
            IsPassword = isPassword,
            Placeholder = string.IsNullOrEmpty(p.Placeholder) ? p.Texto : p.Placeholder,
            Text = existing?.ValorTexto ?? "",
            FontSize = 14,
            BackgroundColor = Color.FromArgb("#f9fafb"),
        };
        entry.TextChanged += (_, e) =>
        {
            if (kb == Keyboard.Numeric && double.TryParse(e.NewTextValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                _vm.ResponderPregunta(p.Id, num);
            else
                _vm.ResponderPregunta(p.Id, e.NewTextValue);
        };
        return entry;
    }

    // ── TEXTO LARGO ───────────────────────────────────────────────────────────
    private View BuildEditorControl(PreguntaLocal p)
    {
        var existing = _vm.GetRespuesta(p.Id);
        var editor = new Editor
        {
            Placeholder = string.IsNullOrEmpty(p.Placeholder) ? p.Texto : p.Placeholder,
            Text = existing?.ValorTexto ?? "",
            FontSize = 14,
            HeightRequest = 90,
            BackgroundColor = Color.FromArgb("#f9fafb"),
        };
        editor.TextChanged += (_, e) => _vm.ResponderPregunta(p.Id, e.NewTextValue);
        return editor;
    }

    // ── SELECCIÓN ÚNICA ────────────────────────────────────────────────────────
    private View BuildSeleccionUnicaControl(PreguntaLocal p)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var existing = _vm.GetRespuesta(p.Id);
        string? selectedCodigo = existing?.ValorTexto;
        var opciones = GetOpciones(p);
        var buttons = new List<Button>();

        foreach (var op in opciones)
        {
            bool isSelected = selectedCodigo == op.Codigo;
            var btn = new Button
            {
                Text = op.Texto ?? op.Codigo,
                CornerRadius = 8,
                HeightRequest = 44,
                BackgroundColor = isSelected ? Color.FromArgb("#1a56db") : Color.FromArgb("#f3f4f6"),
                TextColor = isSelected ? Colors.White : Color.FromArgb("#374151"),
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Fill
            };
            var capture = op;
            btn.Clicked += (_, _) =>
            {
                foreach (var b in buttons)
                {
                    b.BackgroundColor = Color.FromArgb("#f3f4f6");
                    b.TextColor = Color.FromArgb("#374151");
                }
                btn.BackgroundColor = Color.FromArgb("#1a56db");
                btn.TextColor = Colors.White;
                _vm.ResponderPregunta(p.Id, capture.Codigo);
            };
            buttons.Add(btn);
            stack.Children.Add(btn);
        }

        if (!opciones.Any())
        {
            var entry = new Entry { Placeholder = "Escriba su respuesta", Text = existing?.ValorTexto ?? "", FontSize = 14 };
            entry.TextChanged += (_, e) => _vm.ResponderPregunta(p.Id, e.NewTextValue);
            stack.Children.Add(entry);
        }

        return stack;
    }

    // ── SELECCIÓN MÚLTIPLE ────────────────────────────────────────────────────
    private View BuildSeleccionMultipleControl(PreguntaLocal p)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        var existing = _vm.GetRespuesta(p.Id);
        var selected = new HashSet<string>();
        if (!string.IsNullOrEmpty(existing?.ValorJson))
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(existing.ValorJson);
                if (list != null) foreach (var s in list) selected.Add(s);
            }
            catch { }
        }

        var opciones = GetOpciones(p);
        foreach (var op in opciones)
        {
            var row = new HorizontalStackLayout { Spacing = 10 };
            var chk = new CheckBox { IsChecked = selected.Contains(op.Codigo ?? "") };
            var lbl = new Label { Text = op.Texto ?? op.Codigo, FontSize = 13, VerticalOptions = LayoutOptions.Center };
            var capture = op;
            chk.CheckedChanged += (_, e) =>
            {
                if (e.Value) selected.Add(capture.Codigo ?? "");
                else selected.Remove(capture.Codigo ?? "");
                _vm.ResponderPregunta(p.Id, selected.ToList());
            };
            row.Children.Add(chk);
            row.Children.Add(lbl);
            stack.Children.Add(row);
        }
        return stack;
    }

    // ── FECHA ─────────────────────────────────────────────────────────────────
    private View BuildFechaControl(PreguntaLocal p)
    {
        var existing = _vm.GetRespuesta(p.Id);
        var picker = new DatePicker { Format = "dd/MM/yyyy" };
        if (DateTime.TryParse(existing?.ValorTexto, out var d)) picker.Date = d;
        picker.DateSelected += (_, e) => _vm.ResponderPregunta(p.Id, e.NewDate?.ToShortDateString() ?? DateTime.Now.ToShortDateString());
        return picker;
    }

    // ── FOTO ──────────────────────────────────────────────────────────────────
    private View BuildFotoControl(PreguntaLocal p, bool multiple)
    {
        var existing = _vm.GetRespuesta(p.Id);
        var stack = new VerticalStackLayout { Spacing = 8 };

        // For single photo: path stored in ValorTexto
        // For multiple photos: paths stored in ValorJson (JSON array)
        var existingPath = multiple ? existing?.ValorJson : existing?.ValorTexto;
        var hasFoto = !string.IsNullOrEmpty(existingPath);

        var statusLabel = new Label
        {
            Text = hasFoto ? (multiple ? "Foto(s) capturadas ✓" : "Foto capturada ✓") : "Sin foto",
            FontSize = 12,
            TextColor = hasFoto ? Color.FromArgb("#16a34a") : Colors.Gray
        };

        // Thumbnail image (single photo only — shown when path exists)
        Image? thumbnail = null;
        if (!multiple)
        {
            thumbnail = new Image
            {
                HeightRequest = 120,
                Aspect = Aspect.AspectFill,
                IsVisible = hasFoto && File.Exists(existingPath)
            };
            if (thumbnail.IsVisible)
                thumbnail.Source = ImageSource.FromFile(existingPath);
        }

        var btn = new Button
        {
            Text = multiple ? "Tomar fotos" : "Tomar foto",
            BackgroundColor = Color.FromArgb("#1a56db"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44
        };

        btn.Clicked += async (_, _) =>
        {
            try
            {
                if (multiple)
                {
                    var photos = new List<string>();
                    for (int i = 0; i < 2; i++)
                    {
                        var photo = await MediaPicker.CapturePhotoAsync();
                        if (photo != null) photos.Add(photo.FullPath);
                        else break;
                    }
                    if (photos.Any())
                    {
                        _vm.ResponderPregunta(p.Id, photos);
                        statusLabel.Text = $"{photos.Count} foto(s) capturadas ✓";
                        statusLabel.TextColor = Color.FromArgb("#16a34a");
                    }
                }
                else
                {
                    var photo = await MediaPicker.CapturePhotoAsync();
                    if (photo != null)
                    {
                        _vm.ResponderPregunta(p.Id, photo.FullPath);
                        statusLabel.Text = "Foto capturada ✓";
                        statusLabel.TextColor = Color.FromArgb("#16a34a");
                        if (thumbnail != null)
                        {
                            thumbnail.Source = ImageSource.FromFile(photo.FullPath);
                            thumbnail.IsVisible = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
            }
        };

        stack.Children.Add(btn);
        stack.Children.Add(statusLabel);
        if (thumbnail != null)
            stack.Children.Add(thumbnail);
        return stack;
    }

    // ── COORDENADAS ────────────────────────────────────────────────────────────
    private View BuildCoordenadasControl(PreguntaLocal p)
    {
        var existing = _vm.GetRespuesta(p.Id);
        var stack = new VerticalStackLayout { Spacing = 6 };

        var coordLabel = new Label
        {
            Text = existing?.ValorTexto ?? "Sin coordenadas",
            FontSize = 12,
            TextColor = string.IsNullOrEmpty(existing?.ValorTexto) ? Colors.Gray : Color.FromArgb("#16a34a")
        };

        var btn = new Button
        {
            Text = "Capturar GPS",
            BackgroundColor = Color.FromArgb("#1a56db"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44
        };

        btn.Clicked += async (_, _) =>
        {
            try
            {
                btn.Text = "Obteniendo GPS...";
                btn.IsEnabled = false;
                var location = await Geolocation.GetLastKnownLocationAsync()
                    ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
                var val = location != null
                    ? $"{location.Latitude:F6},{location.Longitude:F6}"
                    : "-29.950000,-71.335000";
                _vm.ResponderPregunta(p.Id, val);
                coordLabel.Text = location != null ? $"GPS: {val} ✓" : $"GPS (aprox): {val}";
                coordLabel.TextColor = Color.FromArgb("#16a34a");
            }
            catch
            {
                // Use mock coordinates if GPS unavailable
                var val = "-29.950000,-71.335000";
                _vm.ResponderPregunta(p.Id, val);
                coordLabel.Text = $"GPS (mock): {val}";
                coordLabel.TextColor = Color.FromArgb("#f59e0b");
            }
            finally
            {
                btn.Text = "Capturar GPS";
                btn.IsEnabled = true;
            }
        };

        stack.Children.Add(btn);
        stack.Children.Add(coordLabel);
        return stack;
    }

    // ── FIRMA ─────────────────────────────────────────────────────────────────
    private View BuildFirmaControl(PreguntaLocal p)
    {
        var existing = _vm.GetRespuesta(p.Id);
        var stack = new VerticalStackLayout { Spacing = 6 };

        var canvas = new GraphicsView
        {
            HeightRequest = 120,
            BackgroundColor = Color.FromArgb("#f9fafb"),
            WidthRequest = 300
        };

        var firmaDrawable = new FirmaDrawable();
        canvas.Drawable = firmaDrawable;

        var statusLabel = new Label
        {
            Text = string.IsNullOrEmpty(existing?.ValorTexto) ? "Firme en el recuadro" : "Firma registrada ✓",
            FontSize = 12,
            TextColor = string.IsNullOrEmpty(existing?.ValorTexto) ? Colors.Gray : Color.FromArgb("#16a34a")
        };

        canvas.StartInteraction += (_, e) =>
        {
            firmaDrawable.StartPath(e.Touches[0]);
            canvas.Invalidate();
        };
        canvas.DragInteraction += (_, e) =>
        {
            firmaDrawable.AddPoint(e.Touches[0]);
            canvas.Invalidate();
            if (!firmaDrawable.HasSignature) return;
            _vm.ResponderPregunta(p.Id, "firma_registrada");
            statusLabel.Text = "Firma registrada ✓";
            statusLabel.TextColor = Color.FromArgb("#16a34a");
        };

        var clearBtn = new Button
        {
            Text = "Limpiar",
            BackgroundColor = Color.FromArgb("#f3f4f6"),
            TextColor = Color.FromArgb("#374151"),
            CornerRadius = 6,
            HeightRequest = 36,
            FontSize = 12
        };
        clearBtn.Clicked += (_, _) =>
        {
            firmaDrawable.Clear();
            canvas.Invalidate();
            statusLabel.Text = "Firme en el recuadro";
            statusLabel.TextColor = Colors.Gray;
        };

        var border = new Border
        {
            Stroke = Color.FromArgb("#d1d5db"),
            StrokeThickness = 1,
            Content = canvas,
            HeightRequest = 120
        };

        stack.Children.Add(border);
        stack.Children.Add(statusLabel);
        stack.Children.Add(clearBtn);
        return stack;
    }

    // ── SIGUIENTE / CERRAR ───────────────────────────────────────────────────
    private async void OnSiguienteClicked(object? sender, EventArgs e)
    {
        if (_vm.EsUltimaSeccion)
        {
            var ok = await _vm.CerrarInspeccionAsync();
            if (ok) await Shell.Current.GoToAsync("..");
        }
        else
        {
            _vm.SiguienteSeccionCommand.Execute(null);
        }
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private List<OpcionLocal> GetOpciones(PreguntaLocal p)
    {
        // Load from DB synchronously via task (opciones were loaded by FlowEngine)
        var task = Task.Run(async () =>
        {
            var db = Handler?.MauiContext?.Services.GetService<SgiForm.Mobile.Database.AppDatabase>();
            return db != null ? await db.GetOpcionesAsync(p.Id) : new List<OpcionLocal>();
        });
        return task.GetAwaiter().GetResult();
    }
}

// ── FIRMA DRAWABLE ────────────────────────────────────────────────────────────
public class FirmaDrawable : IDrawable
{
    private readonly List<List<PointF>> _paths = new();
    private List<PointF>? _current;

    public bool HasSignature => _paths.Any(p => p.Count > 2);

    public void StartPath(PointF p)
    {
        _current = new List<PointF> { p };
        _paths.Add(_current);
    }

    public void AddPoint(PointF p) => _current?.Add(p);

    public void Clear() { _paths.Clear(); _current = null; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = Color.FromArgb("#1e293b");
        canvas.StrokeSize = 2;
        canvas.FillColor = Color.FromArgb("#f9fafb");
        canvas.FillRectangle(dirtyRect);

        foreach (var path in _paths)
        {
            for (int i = 1; i < path.Count; i++)
                canvas.DrawLine(path[i - 1].X, path[i - 1].Y, path[i].X, path[i].Y);
        }
    }
}
