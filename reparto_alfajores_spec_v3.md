# Especificación completa v2: App de reparto de alfajores y masas dulces

## Objetivo

Construir una aplicación web completa para gestionar clientes, ventas, cobros y reportes de un negocio de reparto de alfajores y masas dulces. El sistema es usado por una sola persona (dueño y repartidor).

---

## Stack técnico

- ASP.NET Core 8 con MVC (Controllers + Views)
- PostgreSQL como base de datos
- Entity Framework Core 8 (Code First)
- Bootstrap 5 para estilos y layout
- Bootstrap Icons para íconos
- Chart.js (CDN) para gráficos
- Vanilla JS para interactividad
- Despliegue en Render (free tier)

---

## Colores principales

- Naranja principal: `#BA7517`
- Naranja claro (hover/activo): `#FAEEDA`
- Naranja oscuro: `#633806`
- Verde éxito: `#3B6D11` / `#EAF3DE`
- Rojo deuda: `#A32D2D` / `#FCEBEB`
- Amarillo advertencia: `#633806` / `#FAEEDA`

---

## Arquitectura general

### Capas de la aplicación

```
Presentación  →  Controllers + Views  (MVC)
Lógica        →  Services             (Interfaces + Implementaciones)
Datos         →  AppDbContext         (EF Core, solo accedido desde servicios)
```

### Reglas de capas
- Los **Controllers** nunca acceden directamente a `AppDbContext`. Solo inyectan servicios vía interfaz.
- Los **Services** contienen toda la lógica de negocio y acceso a datos.
- Las **Views** solo renderizan ViewModels — nunca modelos de dominio directamente expuestos.
- Toda entrada del usuario pasa por **ViewModels validados** con Data Annotations.

---

## Paso 1 — Modelos de base de datos

Crear los siguientes modelos en la carpeta `Models/`:

### Zona
```csharp
public class Zona
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public bool Activa { get; set; } = true;
    public ICollection<Cliente> Clientes { get; set; }
}
```

### Cliente
```csharp
public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public string Telefono { get; set; }
    public string Direccion { get; set; }
    public int ZonaId { get; set; }
    public Zona Zona { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    public ICollection<Venta> Ventas { get; set; }
}
```

### CategoriaProducto
```csharp
public class CategoriaProducto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public ICollection<Producto> Productos { get; set; }
}
```

### Producto
```csharp
public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public int CategoriaId { get; set; }
    public CategoriaProducto Categoria { get; set; }
    public decimal PrecioUnitario { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<DetalleVenta> Detalles { get; set; }
}
```

### Venta
```csharp
public enum EstadoCobro { Cobrado, CuentaCorriente }
public enum MetodoPago { Efectivo, Transferencia, QR, CuentaCorriente }

public class Venta
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public EstadoCobro EstadoCobro { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public string? Nota { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; }
}
```

### DetalleVenta
```csharp
public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public Venta Venta { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }  // precio al momento de la venta
    public decimal Subtotal => Cantidad * PrecioUnitario;
}
```

### Cobro
```csharp
public class Cobro
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; }
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Nota { get; set; }
}
```

---

## Paso 2 — DbContext y migraciones

Crear `Data/AppDbContext.cs` con todos los DbSet. Configurar la cadena de conexión para PostgreSQL en `appsettings.json`. Usar `Npgsql.EntityFrameworkCore.PostgreSQL`.

**Importante:** El DbContext solo es accedido desde las clases de servicio. Nunca inyectar `AppDbContext` en un Controller.

Correr las migraciones y crear un seeder con datos iniciales:
- 4 zonas: Norte, Centro, Sur, Oeste
- 4 categorías: Alfajores, Tortas, Masas, Otros
- 5 productos de ejemplo activos
- 3 clientes de ejemplo

---

## Paso 3 — ViewModels

Crear la carpeta `ViewModels/` con un ViewModel por cada formulario. Los ViewModels son los únicos objetos que se reciben en POST y que se pasan a las vistas. **Nunca exponer modelos de dominio directamente en formularios.**

### Ejemplo: ClienteViewModel
```csharp
public class ClienteViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
    public string Nombre { get; set; }

    [Phone(ErrorMessage = "Teléfono inválido")]
    [StringLength(20)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una zona")]
    public int ZonaId { get; set; }

    public bool Activo { get; set; } = true;

    public IEnumerable<SelectListItem> Zonas { get; set; } = new List<SelectListItem>();
}
```

Crear ViewModels equivalentes para: `ProductoViewModel`, `VentaViewModel`, `CobroViewModel`, `ZonaViewModel`, `CategoriaViewModel`.

---

## Paso 4 — Capa de servicios

### Estructura de carpetas
```
Services/
  Interfaces/
    IClienteService.cs
    IProductoService.cs
    IVentaService.cs
    ICobroService.cs
    IZonaService.cs
    ICategoriaService.cs
    IReporteService.cs
    IDashboardService.cs
  Implementations/
    ClienteService.cs
    ProductoService.cs
    VentaService.cs
    CobroService.cs
    ZonaService.cs
    CategoriaService.cs
    ReporteService.cs
    DashboardService.cs
```

### Registro en Program.cs
```csharp
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<ICobroService, CobroService>();
builder.Services.AddScoped<IZonaService, ZonaService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
```

### IClienteService (ejemplo representativo)
```csharp
public interface IClienteService
{
    Task<IEnumerable<Cliente>> GetAllAsync(string? busqueda = null, int? zonaId = null, string? estadoDeuda = null);
    Task<Cliente?> GetByIdAsync(int id);
    Task<Cliente> CreateAsync(ClienteViewModel vm);
    Task UpdateAsync(ClienteViewModel vm);
    Task<bool> DeleteAsync(int id);
    Task<decimal> GetSaldoPendienteAsync(int clienteId);
    Task<IEnumerable<Venta>> GetVentasByClienteAsync(int clienteId);
    Task<IEnumerable<Cobro>> GetCobrosByClienteAsync(int clienteId);
}
```

Definir interfaces equivalentes para cada servicio con los métodos que correspondan a su responsabilidad.

### Regla de acceso a datos en servicios
Los servicios usan **solo LINQ to Entities y EF Core** — nunca SQL raw. Esto garantiza protección contra SQL injection por defecto. Si en algún caso excepcional se necesitara SQL raw, usar **únicamente parámetros parametrizados**:

```csharp
// ✅ Correcto si se usa SQL raw (excepcional)
await _db.Database.ExecuteSqlRawAsync(
    "SELECT * FROM clientes WHERE nombre = {0}", nombre);

// ❌ Nunca concatenar strings en SQL
await _db.Database.ExecuteSqlRawAsync(
    $"SELECT * FROM clientes WHERE nombre = '{nombre}'");
```

---

## Paso 5 — Controllers

### Estructura de carpetas
```
Controllers/
  HomeController.cs       ← Dashboard
  ClientesController.cs
  ProductosController.cs
  VentasController.cs
  CobrosController.cs
  ReportesController.cs
  ConfiguracionController.cs
  AuthController.cs
```

### Reglas de Controllers
- Solo inyectan interfaces de servicios (nunca `AppDbContext` directamente).
- Toda acción POST valida `ModelState.IsValid` antes de procesar.
- Todas las acciones están decoradas con `[Authorize]`, excepto las de `AuthController`.
- Las acciones que modifican datos usan el patrón PRG (Post-Redirect-Get).

### Ejemplo: ClientesController
```csharp
[Authorize]
public class ClientesController : Controller
{
    private readonly IClienteService _clienteService;
    private readonly IZonaService _zonaService;

    public ClientesController(IClienteService clienteService, IZonaService zonaService)
    {
        _clienteService = clienteService;
        _zonaService = zonaService;
    }

    public async Task<IActionResult> Index(string? busqueda, int? zonaId, string? estadoDeuda)
    {
        var clientes = await _clienteService.GetAllAsync(busqueda, zonaId, estadoDeuda);
        return View(clientes);
    }

    public async Task<IActionResult> Nuevo()
    {
        var vm = new ClienteViewModel
        {
            Zonas = await _zonaService.GetSelectListAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(ClienteViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Zonas = await _zonaService.GetSelectListAsync();
            return View(vm);
        }
        await _clienteService.CreateAsync(vm);
        return RedirectToAction(nameof(Index));
    }

    // Editar, Detalle, Eliminar siguen el mismo patrón
}
```

---

## Paso 6 — Seguridad

### 6.1 Autenticación con Cookie

Configurar en `Program.cs`:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

app.UseAuthentication();
app.UseAuthorization();
```

La contraseña se guarda en `appsettings.json` (no en código):
```json
{
  "Auth": {
    "Password": "tu_contraseña_segura"
  }
}
```

El `AuthController` compara el hash SHA-256 del input contra el hash almacenado — nunca comparar passwords en texto plano.

### 6.2 Protección CSRF

Todas las acciones POST, PUT y DELETE deben tener:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Guardar(ClienteViewModel vm) { ... }
```

En todas las vistas con formularios incluir:
```html
<form asp-action="Guardar" method="post">
    @Html.AntiForgeryToken()
    ...
</form>
```

Registrar el filtro globalmente para que aplique a todos los controllers:
```csharp
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
```

### 6.3 Protección XSS

- Usar siempre Razor (`@variable`) para renderizar datos — Razor escapa HTML automáticamente.
- **Nunca usar `@Html.Raw()`** con datos provenientes del usuario.
- `@Html.Raw()` solo se permite para JSON serializado desde el servidor (ej: datos para Chart.js), y ese JSON debe generarse con `System.Text.Json` — nunca concatenando strings.

```csharp
// ✅ Correcto
public string VentasDiaJson { get; set; }
VentasDiaJson = JsonSerializer.Serialize(datos);

// En la vista:
var datos = @Html.Raw(Model.VentasDiaJson);  // ← OK porque viene del servidor, no del usuario
```

### 6.4 Headers de seguridad HTTP

Agregar en `Program.cs` antes de `app.Run()`:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline'; " +
        "style-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline'; " +
        "font-src 'self' https://cdnjs.cloudflare.com; " +
        "img-src 'self' data:;"
    );
    await next();
});
```

### 6.5 Validación de entrada (server-side)

Toda entrada del usuario se valida en el ViewModel con Data Annotations. Además, en los servicios aplicar sanitización defensiva:

```csharp
// En el servicio, antes de persistir
public async Task<Cliente> CreateAsync(ClienteViewModel vm)
{
    var cliente = new Cliente
    {
        Nombre = vm.Nombre.Trim(),
        Telefono = vm.Telefono?.Trim(),
        Direccion = vm.Direccion?.Trim(),
        ZonaId = vm.ZonaId,
        Activo = vm.Activo
    };
    _db.Clientes.Add(cliente);
    await _db.SaveChangesAsync();
    return cliente;
}
```

**Regla:** nunca confiar solo en la validación del cliente (JavaScript). Siempre validar en servidor.

### 6.6 Protección contra Mass Assignment

Los ViewModels actúan como barrera de Mass Assignment — el modelo de dominio nunca se bindea directamente desde el request. El controller solo recibe ViewModels, y el servicio mapea explícitamente cada campo.

```csharp
// ❌ Nunca esto (expone todos los campos del modelo)
[HttpPost]
public async Task<IActionResult> Guardar([Bind] Cliente cliente) { ... }

// ✅ Siempre usar ViewModel
[HttpPost]
public async Task<IActionResult> Guardar(ClienteViewModel vm) { ... }
```

### 6.7 Logging de errores y excepciones

No exponer stack traces al usuario. Configurar manejo de errores:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
```

Crear una vista `Views/Shared/Error.cshtml` genérica sin detalles de implementación.

---

## Paso 7 — Layout compartido

Crear `Views/Shared/_Layout.cshtml` con:

### Sidebar (escritorio)
```
Logo: "🥐 [NombreNegocio]" + subtítulo "Reparto de dulces"
Navegación:
  - Inicio         → /Home
  - Ventas        → /Ventas
  - Clientes       → /Clientes
  - Productos      → /Productos
  - Cobros         → /Cobros
  - Reportes       → /Reportes
  Al final:
  - Configuración  → /Configuracion
  - Cerrar sesión  → /Auth/Logout
```

El ítem activo se resalta con `background: #FAEEDA; color: #633806; font-weight: 500`.

Para marcar el ítem activo, comparar con `ViewContext.RouteData.Values["controller"]`.

### Responsivo (celular)
En mobile el sidebar se convierte en una barra de navegación inferior con íconos. Usar Bootstrap para el colapso.

### Estilos globales
Agregar en `wwwroot/css/site.css`:
- Variables de color
- Estilos del sidebar
- Clases utilitarias de badge (`.badge-ok`, `.badge-warn`, `.badge-danger`)
- Tabla base con hover

---

## Paso 8 — Página: Dashboard (Home)

**Controller:** `HomeController.cs`
**View:** `Views/Home/Index.cshtml`
**Servicio:** `IDashboardService`

### Datos que muestra

**Métricas del día (4 tarjetas):**
- Total vendido hoy
- Cantidad de ventas hoy
- Total por cobrar (suma de saldos en cuenta corriente)
- Total cobrado hoy

**Ventas de hoy** (tabla):
- Columnas: cliente, productos (resumen), total, estado (cobrado / cuenta cte.)
- Ordenados por hora descendente
- Máximo 10 registros, con link "ver todos"

**Deudores** (lista):
- Top 5 clientes con mayor saldo negativo
- Mostrar nombre, días desde primera venta sin cobrar, monto
- Badge de color según antigüedad: verde <7 días, amarillo 7-20, rojo >20

**Acceso rápido** (4 botones):
- Nueva venta → /Ventas/Nuevo
- Registrar cobro → /Cobros
- Nuevo cliente → /Clientes/Nuevo
- Ver reportes → /Reportes

### Cálculo de saldo de cliente
```
SaldoPendiente = SUM(ventas en cuenta corriente) - SUM(cobros registrados)
```

---

## Paso 9 — Página: Clientes

**Controller:** `ClientesController.cs`
**Views:** `Views/Clientes/Index.cshtml`, `Nuevo.cshtml`, `Editar.cshtml`, `Detalle.cshtml`
**Servicio:** `IClienteService`

### Lista de clientes (Index)

**Filtros:**
- Buscador por nombre o teléfono
- Selector de estado: Todos / Sin deuda / Con deuda
- Tabs de zona: Todas / y las que existan en DB

**Tabla:**
- Columnas: avatar con iniciales, nombre + teléfono, zona (badge de color), dirección, última venta, saldo, acciones
- Acciones: ver detalle, editar, ver ventas del cliente
- Paginación: 15 por página

**Colores de zona:**
- Norte → azul (`#E6F1FB` / `#0C447C`)
- Centro → naranja (`#FAEEDA` / `#633806`)
- Sur → verde (`#EAF3DE` / `#27500A`)
- Oeste → violeta (`#EEEDFE` / `#3C3489`)

### Formulario nuevo/editar cliente
Campos del ViewModel:
- Nombre (requerido, máx. 100)
- Teléfono (opcional, validación de formato)
- Dirección (opcional)
- Zona (select con zonas activas de la DB)
- Activo (toggle)

### Detalle de cliente
- Info del cliente
- Saldo actual (breakdown: total ventas en cuenta cte. vs total cobrado)
- Historial de ventas (tabla)
- Historial de cobros (tabla)

---

## Paso 10 — Página: Ventas

**Controller:** `VentasController.cs`
**Views:** `Views/Ventas/Index.cshtml`, `Nuevo.cshtml`, `Detalle.cshtml`
**Servicio:** `IVentaService`

### Lista de ventas (Index)

**Layout:** dos columnas — lista a la izquierda, formulario de nueva venta a la derecha (siempre visible en desktop).

**Filtros de la lista:**
- Buscador por nombre de cliente
- Selector de estado: Todos / Cobrado / Cuenta corriente
- Selector de zona
- Selector de fecha (por defecto: hoy)

**Tabla:**
- Columnas: #ID, cliente, zona, productos (resumen textual), total, estado cobro, acciones
- Acciones: ver detalle, imprimir, eliminar

**Pie de tabla:** total de ventas del día y suma total.

### Formulario nueva venta

3 pasos visibles simultáneamente:

**Paso 1 — Cliente:**
- Select con todos los clientes activos (nombre + zona)
- Si el cliente tiene deuda, mostrarla debajo en rojo

**Paso 2 — Productos:**
- Select de producto + input de cantidad + botón "+"
- Lista de ítems agregados con: nombre, cantidad, subtotal, botón eliminar
- Subtotal calculado automáticamente en JS

**Paso 3 — Forma de cobro:**
- Opciones: Efectivo / Transferencia / Cuenta corriente
- Si elige "Cuenta corriente", la venta queda como deuda

**Total:** se muestra y actualiza en tiempo real.

**Botón "Confirmar venta"** → POST al servidor con `[ValidateAntiForgeryToken]`, redirige a la lista.

### Detalle de venta
- Info completa: cliente, fecha, productos, total, método de pago
- Versión imprimible (sin sidebar, solo datos de la venta)

---

## Paso 11 — Página: Productos

**Controller:** `ProductosController.cs`
**Views:** `Views/Productos/Index.cshtml`, `Nuevo.cshtml`, `Editar.cshtml`
**Servicio:** `IProductoService`

### Lista de productos (Index)

**Layout:** grilla de cards a la izquierda + formulario de nuevo producto a la derecha.

**Filtros:**
- Buscador por nombre
- Selector: Todos / Activos / Inactivos
- Tabs de categoría (dinámicas desde DB)

**Cards de productos:**
- Icono emoji según categoría (🍫 Alfajores, 🎂 Tortas, 🥐 Masas, 🍬 Otros)
- Nombre, categoría, precio
- Badge activo/inactivo
- Botones: editar, activar/desactivar (sin borrar)

**Productos inactivos:** mostrar con `opacity: 0.5`. No aparecen en el selector de ventas.

---

## Paso 12 — Página: Cobros y deudores

**Controller:** `CobrosController.cs`
**View:** `Views/Cobros/Index.cshtml`
**Servicio:** `ICobroService`

### Layout: columna principal + formulario lateral

**Métricas (3 tarjetas):**
- Total por cobrar (suma saldos negativos de todos los clientes)
- Total cobrado hoy
- Cliente con deuda más antigua (nombre + días)

**Tabla de deudores:**
- Columnas: cliente (avatar + nombre + zona), zona (badge), saldo pendiente, días sin pagar, último pago, acciones
- Acciones: botón "Registrar pago" (pre-selecciona ese cliente en el formulario lateral), ver detalle
- Ordenado por días sin pagar (mayor primero)
- Badge de días: verde <7, amarillo 7-20, rojo >20

**Historial de cobros de hoy:**
- Lista simple: hora, cliente, método, monto

### Formulario registrar cobro (panel derecho)
Campos del ViewModel:
- ClienteId (select — solo clientes con deuda, con saldo entre paréntesis)
- MontoCobrado (decimal, requerido, `[Range(0.01, double.MaxValue)]`)
- MetodoPago (Efectivo / Transferencia / QR)
- Nota (opcional)

Al confirmar → POST con `[ValidateAntiForgeryToken]`.

---

## Paso 13 — Página: Reportes

**Controller:** `ReportesController.cs`
**View:** `Views/Reportes/Index.cshtml`
**Servicio:** `IReporteService`

### Filtros
- Tabs rápidos: Hoy / Semana / Mes
- Fechas custom: desde / hasta

### Métricas (4 tarjetas)
- Total vendido en el período
- Cantidad de ventas
- Ticket promedio
- Total cobrado vs pendiente

### Gráficos y rankings
- Ventas por día (línea, Chart.js)
- Ventas por zona (torta + lista)
- Productos más vendidos (Top 8, barra de progreso)
- Mejores clientes (Top 5 por monto)

### Serialización para Chart.js
```csharp
// En el Controller
public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta)
{
    var vm = await _reporteService.GetReporteAsync(desde, hasta);
    vm.VentasDiaJson = JsonSerializer.Serialize(vm.VentasDia);
    vm.VentasZonaJson = JsonSerializer.Serialize(vm.VentasZona);
    return View(vm);
}

// En la vista (datos del servidor, no del usuario)
var ventas = @Html.Raw(Model.VentasDiaJson);
```

---

## Paso 14 — Página: Configuración

**Controller:** `ConfiguracionController.cs`
**View:** `Views/Configuracion/Index.cshtml`
**Servicios:** `IZonaService`, `ICategoriaService`

Secciones:
- **Nombre del negocio** (se muestra en el sidebar y en impresiones)
- **Zonas:** ABM de zonas (agregar, renombrar, activar/desactivar)
- **Categorías de productos:** ABM de categorías

---

## Paso 15 — Autenticación (AuthController)

**Controller:** `AuthController.cs` (sin `[Authorize]`)

```csharp
public class AuthController : Controller
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string password)
    {
        var storedHash = _config["Auth:PasswordHash"];
        var inputHash = ComputeSha256(password);

        if (inputHash != storedHash)
        {
            ModelState.AddModelError("", "Contraseña incorrecta");
            return View();
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    private string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}
```

En `appsettings.json` guardar el **hash SHA-256** de la contraseña, nunca el texto plano:
```json
{
  "Auth": {
    "PasswordHash": "hash_sha256_de_tu_contraseña"
  }
}
```

---

## Notas generales de implementación

### Formato de valores monetarios
```csharp
// En C#
value.ToString("N2", new CultureInfo("es-AR"))

// En JS
value.toLocaleString('es-AR', { minimumFractionDigits: 2 })
```

### Saldo de cliente (cálculo)
```csharp
var saldo = ventas
    .Where(p => p.EstadoCobro == EstadoCobro.CuentaCorriente)
    .Sum(p => p.Total)
    - cobros.Sum(c => c.Monto);
```

### Resumen textual de productos en tabla
```csharp
string.Join(" · ", venta.Detalles
    .Select(d => $"{d.Cantidad}× {d.Producto.Nombre}"))
```

### Iniciales del cliente (para avatar)
```csharp
var partes = cliente.Nombre.Split(' ');
var iniciales = partes.Length >= 2
    ? $"{partes[0][0]}{partes[1][0]}"
    : partes[0].Substring(0, Math.Min(2, partes[0].Length));
iniciales = iniciales.ToUpper();
```

### Días desde primera venta sin cobrar
```csharp
var primeraVentaSinCobrar = ventas
    .Where(v => v.EstadoCobro == EstadoCobro.CuentaCorriente)
    .OrderBy(v => v.Fecha)
    .FirstOrDefault();
var dias = primeraVentaSinCobrar != null
    ? (DateTime.UtcNow - primeraVentaSinCobrar.Fecha).Days
    : 0;
```

### Chart.js CDN
```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.1/chart.umd.js"></script>
```

### Responsive / mobile
- En pantallas < 768px el sidebar se oculta y aparece una barra de navegación inferior con íconos
- Los formularios laterales se apilan debajo de la lista en mobile
- Las tablas tienen scroll horizontal en mobile

---

## Orden de implementación sugerido

1. Modelos + DbContext + migraciones + seeder
2. Interfaces y clases de servicios (vacías con sus métodos definidos)
3. Registro de servicios en Program.cs + configuración de autenticación y seguridad
4. Layout compartido + CSS base
5. AuthController + Views/Auth/Login.cshtml
6. HomeController + IDashboardService
7. ClientesController + IClienteService
8. ProductosController + IProductoService
9. VentasController + IVentaService
10. CobrosController + ICobroService
11. ReportesController + IReporteService
12. ConfiguracionController + IZonaService + ICategoriaService

---

## Estructura de carpetas esperada

```
/
├── Controllers/
│   ├── HomeController.cs
│   ├── AuthController.cs
│   ├── ClientesController.cs
│   ├── ProductosController.cs
│   ├── VentasController.cs
│   ├── CobrosController.cs
│   ├── ReportesController.cs
│   └── ConfiguracionController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── Seeder.cs
├── Models/
│   ├── Zona.cs
│   ├── Cliente.cs
│   ├── CategoriaProducto.cs
│   ├── Producto.cs
│   ├── Venta.cs
│   ├── DetalleVenta.cs
│   └── Cobro.cs
├── ViewModels/
│   ├── ClienteViewModel.cs
│   ├── ProductoViewModel.cs
│   ├── VentaViewModel.cs
│   ├── CobroViewModel.cs
│   ├── ZonaViewModel.cs
│   └── CategoriaViewModel.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IClienteService.cs
│   │   ├── IProductoService.cs
│   │   ├── IVentaService.cs
│   │   ├── ICobroService.cs
│   │   ├── IZonaService.cs
│   │   ├── ICategoriaService.cs
│   │   ├── IReporteService.cs
│   │   └── IDashboardService.cs
│   └── Implementations/
│       ├── ClienteService.cs
│       ├── ProductoService.cs
│       ├── VentaService.cs
│       ├── CobroService.cs
│       ├── ZonaService.cs
│       ├── CategoriaService.cs
│       ├── ReporteService.cs
│       └── DashboardService.cs
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _ValidationScriptsPartial.cshtml
│   │   └── Error.cshtml
│   ├── Home/
│   │   └── Index.cshtml
│   ├── Auth/
│   │   └── Login.cshtml
│   ├── Clientes/
│   │   ├── Index.cshtml
│   │   ├── Nuevo.cshtml
│   │   ├── Editar.cshtml
│   │   └── Detalle.cshtml
│   ├── Productos/
│   │   ├── Index.cshtml
│   │   ├── Nuevo.cshtml
│   │   └── Editar.cshtml
│   ├── Ventas/
│   │   ├── Index.cshtml
│   │   ├── Nuevo.cshtml
│   │   └── Detalle.cshtml
│   ├── Cobros/
│   │   └── Index.cshtml
│   ├── Reportes/
│   │   └── Index.cshtml
│   └── Configuracion/
│       └── Index.cshtml
└── wwwroot/
    └── css/
        └── site.css
```
