# Reparto Alfajores — CLAUDE.md

## Qué es este proyecto

App web para gestionar un reparto de alfajores: ventas diarias, clientes, productos, cobros y cuenta corriente. Stack: **ASP.NET MVC 8, EF Core 8, PostgreSQL (Neon), Razor Views, cookie auth**.

Desplegado en Render. La base de datos es Neon (PostgreSQL serverless en sa-east-1).

## Correr localmente

```bash
cd RepartoAlfajores
dotnet run --urls "http://localhost:5000"
```

Requiere `appsettings.json` con la connection string correcta (ver `appsettings.example.json`).

## Migraciones

```bash
cd RepartoAlfajores
dotnet ef migrations add <NombreMigracion>
dotnet ef database update
```

Las migraciones están en `RepartoAlfajores/Data/Migrations/`. EF Core las aplica automáticamente al arrancar (`MigrateAsync` en `Program.cs`).

## Estructura del proyecto

```
RepartoAlfajores/
├── Controllers/        # AuthController, VentasController, CobrosController,
│                       # ClientesController, ProductosController,
│                       # ReportesController, HomeController, ConfiguracionController
├── Data/
│   ├── AppDbContext.cs
│   ├── Seeder.cs
│   └── Migrations/
├── Models/             # Venta, Cliente, Cobro, Producto, Zona,
│                       # DetalleVenta, MovimientoCC, Configuracion, …
├── Services/
│   ├── Interfaces/
│   └── Implementations/
├── ViewModels/
├── Views/
└── wwwroot/
```

## Modelos clave

| Modelo | Descripción |
|--------|-------------|
| `Venta` | Venta del día. `MetodoPago` (enum) + `EstadoCobro` (enum). Si MetodoPago=CuentaCorriente → EstadoCobro=CuentaCorriente |
| `DetalleVenta` | Línea de producto por venta. `PrecioUnitario` se congela al momento de venta |
| `Cobro` | Pago recibido de un cliente (no vinculado a venta específica) |
| `MovimientoCC` | Libro mayor de cuenta corriente. Un registro por cada venta CC (Cargo) y cada cobro (Abono). `SaldoAcumulado` = saldo DESPUÉS del movimiento |
| `Cliente` | Tiene zona, puede tener deuda en CC |
| `Configuracion` | Tabla clave-valor para settings del negocio (incluye Groq API key) |

## Enums importantes

```csharp
enum MetodoPago   { Efectivo, Transferencia, QR, CuentaCorriente }
enum EstadoCobro  { Cobrado, CuentaCorriente }
enum TipoMovimientoCC { Cargo, Abono }
```

## Cuenta corriente — cómo funciona

- Al crear una venta con `MetodoPago=CuentaCorriente`: `VentaService` inserta un `MovimientoCC` tipo `Cargo`.
- Al registrar un `Cobro`: `CobroService` inserta un `MovimientoCC` tipo `Abono`.
- El saldo actual de un cliente = `SaldoAcumulado` del último `MovimientoCC` del cliente.
- `ClienteService.GetSaldoPendienteAsync` y `CobroService.GetDeudoresAsync` leen de `MovimientosCC` (no hacen SUM histórico).

## Auth

- Cookie auth. Un solo usuario (admin). Password en `appsettings.json` como SHA-256 hash en `Auth:PasswordHash`.
- Hash por defecto: `8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918` = "admin"

## Integración IA (Groq)

- `AIService` + `VentaVozService`: graba audio, lo manda a Groq Whisper (transcripción) y luego a Llama (extrae productos).
- La API key se configura en `Groq:ApiKey` en appsettings o en la pantalla de Configuración (se guarda en tabla `Configuraciones`).

## Deploy

- **Render** con Docker. Puerto interno: 10000 (`ASPNETCORE_URLS=http://+:10000`).
- Variables de entorno en Render: `DATABASE_URL` (postgres://...), `AUTH_PASSWORD_HASH`, `GROQ_API_KEY`, `NOMBRE_NEGOCIO`.
- `Program.cs` parsea `DATABASE_URL` en formato URI automáticamente.
