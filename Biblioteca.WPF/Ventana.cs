using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Biblioteca.Entidades;
using Biblioteca.Negocio;

namespace Biblioteca.WPF;

public sealed class Ventana : Window
{
    private readonly LibroNegocio libros;
    private readonly SocioNegocio socios;
    private readonly PrestamoNegocio prestamos;
    private readonly DockPanel cuerpo = new();
    private readonly StackPanel menu = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock estado = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) };
    private readonly List<Button> navegacion = [];
    private readonly List<Libro> seleccion = [];
    private DataGrid tabla = null!, elegidos = null!;
    private TextBox filtro = null!, nombre = null!, codigo = null!, extra = null!;
    private ComboBox autor = null!, socio = null!, libro = null!;
    private DatePicker desde = null!, hasta = null!;
    private Button consultar = null!, nuevo = null!, guardar = null!, baja = null!, agregar = null!;
    private bool ocupado;
    private int? confirmar;

    private static Brush Color(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;

    [STAThread]
    public static void Main()
    {
        var app = new Application();
        app.Run(new Ventana());
    }

    public Ventana()
    {
        string cadena = ConfigurationManager.ConnectionStrings["BibliotecaDB"]?.ConnectionString
            ?? throw new ConfigurationErrorsException("Falta BibliotecaDB en App.config.");
        libros = new LibroNegocio(cadena);
        socios = new SocioNegocio(cadena);
        prestamos = new PrestamoNegocio(cadena);
        Title = "Biblioteca | Laboratorio 07";
        Width = 1120;
        Height = 685;
        MinWidth = 960;
        MinHeight = 615;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        Language = XmlLanguage.GetLanguage("es-PE");
        Foreground = Color("#1D2946");

        var raiz = new DockPanel { Background = Color("#EDF0F8") };
        var cabecera = new StackPanel { Margin = new Thickness(25, 12, 25, 14) };
        cabecera.Children.Add(new TextBlock { Text = "LABORATORIO 07  /  ARQUITECTURA EN CAPAS", Foreground = Color("#C1C9FF"), FontSize = 13 });
        cabecera.Children.Add(new TextBlock { Text = "Biblioteca · Gestión de préstamos", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold });
        Encajar(raiz, new Border { Background = Color("#252950"), Child = cabecera }, Dock.Top);
        menu.Margin = new Thickness(25, 12, 25, 0);
        Encajar(raiz, menu, Dock.Top);
        Opcion("Libros", () => CatalogoAsync(true));
        Opcion("Socios", () => CatalogoAsync(false));
        Opcion("Nuevo préstamo", NuevoPrestamoAsync);
        Opcion("Devoluciones", DevolucionesAsync);
        Opcion("Reporte", () => { MostrarReporte(); return Task.CompletedTask; });
        cuerpo.Margin = new Thickness(20);
        raiz.Children.Add(new Border { Margin = new Thickness(25, 13, 25, 22), Background = Brushes.White,
            CornerRadius = new CornerRadius(9), Child = cuerpo });
        Content = raiz;
        Loaded += async (_, _) => await EjecutarAsync(() => CatalogoAsync(true));
    }

    private static void Encajar(DockPanel panel, UIElement elemento, Dock lado)
    {
        DockPanel.SetDock(elemento, lado);
        panel.Children.Add(elemento);
    }

    private void Opcion(string texto, Func<Task> accion)
    {
        var boton = Boton(texto, accion);
        navegacion.Add(boton);
        menu.Children.Add(boton);
    }

    private Button Boton(string texto, Func<Task> accion, string color = "#424C93")
    {
        var boton = new Button { Content = texto, Background = Color(color), Foreground = Brushes.White,
            Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(0, 0, 8, 0),
            BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        boton.Click += async (_, _) => await EjecutarAsync(accion);
        return boton;
    }

    private async Task EjecutarAsync(Func<Task> accion)
    {
        if (ocupado) return;
        ocupado = true;
        menu.IsEnabled = cuerpo.IsEnabled = false;
        try { await accion(); }
        catch (ReglaNegocioException error) { Mensaje(error.Message, true); }
        finally
        {
            ocupado = false;
            menu.IsEnabled = cuerpo.IsEnabled = true;
        }
    }

    private void Mensaje(string texto, bool error = false)
    {
        estado.Text = texto;
        estado.Foreground = Color(error ? "#A0283A" : "#19604C");
    }

    private void Pantalla(string titulo, string detalle)
    {
        cuerpo.Children.Clear();
        estado.Text = "";
        confirmar = null;
        var encabezado = new StackPanel { Margin = new Thickness(0, 0, 0, 13) };
        encabezado.Children.Add(new TextBlock { Text = titulo, FontSize = 26, FontWeight = FontWeights.SemiBold });
        encabezado.Children.Add(new TextBlock { Text = detalle, Foreground = Color("#506181") });
        Encajar(cuerpo, encabezado, Dock.Top);
        Encajar(cuerpo, estado, Dock.Bottom);
    }

    private static StackPanel Fila(params FrameworkElement[] controles)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var control in controles) fila.Children.Add(control);
        return fila;
    }

    private static StackPanel Campo(string etiqueta, Control entrada)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 4) };
        panel.Children.Add(new TextBlock { Text = etiqueta, Margin = new Thickness(0, 0, 0, 3) });
        entrada.MinHeight = 26;
        panel.Children.Add(entrada);
        return panel;
    }

    private static TextBox Texto(int maximo, double ancho = double.NaN) =>
        new() { MaxLength = maximo, Width = ancho, Padding = new Thickness(5, 3, 5, 3) };

    private static DataGrid Grilla(params (string Propiedad, string Titulo, int Ancho, string? Formato)[] columnas)
    {
        var grilla = new DataGrid
        {
            IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false,
            AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single, RowHeight = 31, Background = Brushes.White,
            GridLinesVisibility = DataGridGridLinesVisibility.None, AlternatingRowBackground = Color("#F0F2FB")
        };
        foreach (var (propiedad, titulo, ancho, formato) in columnas)
            grilla.Columns.Add(new DataGridTextColumn
            {
                Header = titulo, Width = ancho, Binding = new Binding(propiedad) { StringFormat = formato }
            });
        return grilla;
    }

    private async Task CatalogoAsync(bool esLibro)
    {
        Pantalla(esLibro ? "Mantenimiento de libros" : "Mantenimiento de socios",
            "Selecciona un registro para editarlo o pulsa Nuevo para agregar otro.");
        filtro = Texto(esLibro ? 150 : 100, 295);
        consultar = Boton("Buscar", () => CargarCatalogoAsync(esLibro));
        var limpiar = Boton("Limpiar filtro", async () => { filtro.Clear(); await CargarCatalogoAsync(esLibro); }, "#526484");
        Encajar(cuerpo, Fila(Campo(esLibro ? "Título o autor" : "Nombre o DNI", filtro), consultar, limpiar), Dock.Top);
        tabla = esLibro
            ? Grilla(("LibroId","ID",54,null),("Titulo","Título",240,null),("NombreAutor","Autor",170,null),
                ("ISBN","ISBN",123,null),("Ejemplares","Stock",58,null))
            : Grilla(("SocioId","ID",54,null),("DNI","DNI",115,null),("Nombre","Nombre",188,null),("Email","Correo",240,null));
        var lateral = new DockPanel { Width = 300, Margin = new Thickness(15, 0, 0, 0) };
        var panel = new StackPanel();
        var modo = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(modo);
        panel.Children.Add(Campo(esLibro ? "Título" : "Nombre", nombre = Texto(esLibro ? 150 : 100)));
        panel.Children.Add(Campo(esLibro ? "ISBN" : "DNI", codigo = Texto(esLibro ? 20 : 8)));
        if (esLibro)
        {
            autor = new ComboBox { DisplayMemberPath = nameof(Autor.Nombre), SelectedValuePath = nameof(Autor.AutorId) };
            panel.Children.Add(Campo("Autor", autor));
        }
        panel.Children.Add(Campo(esLibro ? "Ejemplares disponibles" : "Correo", extra = Texto(esLibro ? 10 : 120)));
        nuevo = Boton("Nuevo", () =>
        {
            tabla.SelectedItem = null;
            LlenarCatalogo(esLibro, modo);
            Mensaje("Completa los datos y pulsa Guardar.");
            return Task.CompletedTask;
        });
        guardar = Boton("Guardar", () => GuardarCatalogoAsync(esLibro));
        baja = Boton("Dar de baja", () => BajaCatalogoAsync(esLibro), "#9C394E");
        Encajar(lateral, Fila(nuevo, guardar, baja), Dock.Bottom);
        lateral.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Encajar(cuerpo, lateral, Dock.Right);
        cuerpo.Children.Add(tabla);
        tabla.SelectionChanged += (_, _) => LlenarCatalogo(esLibro, modo);
        LlenarCatalogo(esLibro, modo);
        if (esLibro) autor.ItemsSource = await libros.AutoresAsync();
        await CargarCatalogoAsync(esLibro);
    }

    private void LlenarCatalogo(bool esLibro, TextBlock modo)
    {
        confirmar = null;
        baja.Content = "Dar de baja";
        baja.IsEnabled = tabla.SelectedItem is not null;
        modo.Text = baja.IsEnabled ? "Editar registro" : "Nuevo registro";
        if (esLibro)
        {
            var fila = tabla.SelectedItem as Libro;
            nombre.Text = fila?.Titulo ?? "";
            codigo.Text = fila?.ISBN ?? "";
            autor.SelectedValue = fila?.AutorId;
            extra.Text = fila?.Ejemplares.ToString() ?? "0";
        }
        else
        {
            var fila = tabla.SelectedItem as Socio;
            nombre.Text = fila?.Nombre ?? "";
            codigo.Text = fila?.DNI ?? "";
            extra.Text = fila?.Email ?? "";
        }
    }

    private async Task CargarCatalogoAsync(bool esLibro)
    {
        if (esLibro) tabla.ItemsSource = await libros.BuscarAsync(filtro.Text);
        else tabla.ItemsSource = await socios.BuscarAsync(filtro.Text);
        Mensaje($"Registros activos: {tabla.Items.Count}");
    }

    private async Task GuardarCatalogoAsync(bool esLibro)
    {
        int id;
        if (esLibro)
        {
            var fila = tabla.SelectedItem as Libro;
            if (!int.TryParse(extra.Text, out int stock))
                throw new ReglaNegocioException("Ingresa un número entero de ejemplares.");
            id = await libros.GuardarAsync(new Libro { LibroId = fila?.LibroId ?? 0, Titulo = nombre.Text,
                ISBN = codigo.Text, AutorId = (int?)autor.SelectedValue ?? 0, Ejemplares = stock }, fila?.Ejemplares ?? 0);
        }
        else
        {
            var fila = tabla.SelectedItem as Socio;
            id = await socios.GuardarAsync(new Socio { SocioId = fila?.SocioId ?? 0,
                Nombre = nombre.Text, DNI = codigo.Text, Email = extra.Text });
        }
        filtro.Clear();
        await CargarCatalogoAsync(esLibro);
        tabla.SelectedItem = esLibro
            ? tabla.Items.Cast<Libro>().SingleOrDefault(l => l.LibroId == id)
            : tabla.Items.Cast<Socio>().SingleOrDefault(s => s.SocioId == id);
        if (tabla.SelectedItem is not null) tabla.ScrollIntoView(tabla.SelectedItem);
        Mensaje($"Registro #{id} guardado.");
    }

    private async Task BajaCatalogoAsync(bool esLibro)
    {
        int? id = esLibro ? (tabla.SelectedItem as Libro)?.LibroId : (tabla.SelectedItem as Socio)?.SocioId;
        if (!id.HasValue) return;
        if (confirmar != id)
        {
            confirmar = id;
            baja.Content = "Confirmar";
            Mensaje($"Pulsa Confirmar para dar de baja el registro #{id}.");
            return;
        }
        confirmar = null;
        baja.Content = "Dar de baja";
        if (esLibro) await libros.BajaAsync(id.Value);
        else await socios.BajaAsync(id.Value);
        await CargarCatalogoAsync(esLibro);
        Mensaje($"Registro #{id} dado de baja.");
    }

    private async Task NuevoPrestamoAsync()
    {
        Pantalla("Registrar préstamo", "Selecciona un socio y agrega uno o varios libros al mismo préstamo.");
        seleccion.Clear();
        socio = new ComboBox { Width = 230, DisplayMemberPath = nameof(Socio.Nombre), SelectedValuePath = nameof(Socio.SocioId) };
        desde = new DatePicker { Width = 145, SelectedDate = DateTime.Today };
        hasta = new DatePicker { Width = 145, SelectedDate = DateTime.Today.AddDays(7) };
        Encajar(cuerpo, Fila(Campo("Socio", socio), Campo("Fecha del préstamo", desde), Campo("Fecha límite", hasta)), Dock.Top);
        libro = new ComboBox { Width = 350, DisplayMemberPath = nameof(Libro.Titulo) };
        agregar = Boton("Agregar libro", () =>
        {
            if (libro.SelectedItem is not Libro fila) throw new ReglaNegocioException("Selecciona un libro.");
            if (seleccion.Any(l => l.LibroId == fila.LibroId)) throw new ReglaNegocioException("El libro ya está en la selección.");
            seleccion.Add(fila);
            RefrescarSeleccion();
            return Task.CompletedTask;
        });
        var quitar = Boton("Quitar seleccionado", () =>
        {
            if (elegidos.SelectedItem is Libro fila) seleccion.Remove(fila);
            RefrescarSeleccion();
            return Task.CompletedTask;
        }, "#526484");
        Encajar(cuerpo, Fila(Campo("Libro", libro), agregar, quitar), Dock.Top);
        guardar = Boton("Registrar préstamo", RegistrarAsync);
        Encajar(cuerpo, Fila(guardar), Dock.Bottom);
        elegidos = Grilla(("Titulo","Libro",345,null),("NombreAutor","Autor",240,null),("Ejemplares","Disponibles",120,null));
        cuerpo.Children.Add(elegidos);
        socio.ItemsSource = await socios.BuscarAsync();
        libro.ItemsSource = await libros.BuscarAsync();
        RefrescarSeleccion();
    }

    private void RefrescarSeleccion()
    {
        elegidos.ItemsSource = seleccion.ToArray();
        Mensaje($"Libros seleccionados: {seleccion.Count}");
    }

    private async Task RegistrarAsync()
    {
        if (socio.SelectedValue is not int id || desde.SelectedDate is not DateTime fecha || hasta.SelectedDate is not DateTime limite)
            throw new ReglaNegocioException("Selecciona el socio y las dos fechas.");
        int prestamo = await prestamos.RegistrarAsync(id, seleccion.Select(l => l.LibroId).ToArray(), fecha, limite);
        int cantidad = seleccion.Count;
        seleccion.Clear();
        RefrescarSeleccion();
        libro.ItemsSource = await libros.BuscarAsync();
        Mensaje($"Préstamo #{prestamo} registrado con {cantidad} libro(s).");
    }

    private static DataGrid GrillaPrestamos() => Grilla(
        ("PrestamoId","Préstamo",78,null),("Socio","Socio",135,null),("Libro","Libro",264,null),
        ("FechaPrestamo","Préstamo desde",117,"dd/MM/yyyy"),("FechaLimite","Límite",93,"dd/MM/yyyy"),
        ("FechaDevolucion","Devolución",105,"dd/MM/yyyy"),("Estado","Estado",98,null));

    private async Task DevolucionesAsync()
    {
        Pantalla("Registrar devolución", "Selecciona un libro pendiente para devolverlo y calcular la multa.");
        desde = new DatePicker { Width = 150, SelectedDate = DateTime.Today };
        guardar = Boton("Registrar devolución", DevolverAsync);
        Encajar(cuerpo, Fila(Campo("Fecha de devolución", desde), guardar), Dock.Top);
        tabla = GrillaPrestamos();
        cuerpo.Children.Add(tabla);
        tabla.ItemsSource = await prestamos.PendientesAsync();
        Mensaje($"Libros pendientes: {tabla.Items.Count}");
    }

    private async Task DevolverAsync()
    {
        if (tabla.SelectedItem is not FilaPrestamo fila || desde.SelectedDate is not DateTime fecha)
            throw new ReglaNegocioException("Selecciona un libro pendiente y la fecha de devolución.");
        var resultado = await prestamos.DevolverAsync(fila.PrestamoId, fila.LibroId, fecha);
        tabla.ItemsSource = await prestamos.PendientesAsync();
        Mensaje($"Devolución registrada · Multa S/ {resultado.Multa:0.00} ({resultado.DiasRetraso} día(s)) · " +
            (resultado.Cerrado ? "Préstamo devuelto." : "Quedan libros pendientes."));
    }

    private void MostrarReporte()
    {
        Pantalla("Reporte de préstamos", "Consulta un intervalo por fecha de préstamo. Se incluye una fila por libro.");
        desde = new DatePicker { Width = 145, SelectedDate = DateTime.Today.AddDays(-30) };
        hasta = new DatePicker { Width = 145, SelectedDate = DateTime.Today };
        consultar = Boton("Generar reporte", ReporteAsync);
        Encajar(cuerpo, Fila(Campo("Desde", desde), Campo("Hasta", hasta), consultar), Dock.Top);
        tabla = GrillaPrestamos();
        cuerpo.Children.Add(tabla);
    }

    private async Task ReporteAsync()
    {
        tabla.ItemsSource = null;
        if (desde.SelectedDate is not DateTime inicio || hasta.SelectedDate is not DateTime fin)
            throw new ReglaNegocioException("Selecciona las dos fechas del intervalo.");
        tabla.ItemsSource = await prestamos.ReporteAsync(inicio, fin);
        Mensaje($"Detalles encontrados: {tabla.Items.Count}");
    }
}
