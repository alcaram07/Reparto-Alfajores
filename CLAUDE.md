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

## Tests

```bash
dotnet test RepartoAlfajores.Tests
```

Corren contra **PostgreSQL real**, no un proveedor en memoria: la lógica de cuenta corriente
depende de transacciones sobre la execution strategy de reintentos, `SELECT … FOR UPDATE` y
FKs con `RESTRICT`, que un proveedor in-memory no reproduce.

Cada corrida crea una base descartable (`reparto_tests_<guid>`), le aplica las migraciones y la
borra al terminar. Por defecto se conecta a `localhost:5432` con `postgres`/`postgres`;
se puede cambiar con `TEST_PG_HOST`, `TEST_PG_PORT`, `TEST_PG_USER` y `TEST_PG_PASSWORD`.

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
├── Utils/              # FechaAr (zona horaria Argentina)
├── ViewModels/
├── Views/
└── wwwroot/

RepartoAlfajores.Tests/ # xUnit, corre contra PostgreSQL real
```

## Fechas y zona horaria

Las fechas se guardan **siempre en UTC**, pero el negocio opera en hora argentina (UTC−3).
Nunca usar `DateTime.UtcNow.Date` para definir "el día": usar `FechaAr` (`Utils/FechaAr.cs`).

- `FechaAr.Hoy` → el día de hoy en el calendario argentino.
- `FechaAr.RangoDia(dia)` / `FechaAr.Rango(desde, hasta)` → límites `[desde, hasta)` en UTC para filtrar.
- `fecha.ALocal()` → convierte a hora argentina para mostrar (usarlo en las vistas).

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

Toda la lógica de saldos vive en **`CuentaCorrienteService`**. `VentaService`, `CobroService` y
`ClienteService` delegan ahí; no dupliquen el cálculo de saldos en otro lado.

- Al crear una venta con `MetodoPago=CuentaCorriente` se registra un `MovimientoCC` tipo `Cargo`.
- Al registrar un `Cobro` se registra un `MovimientoCC` tipo `Abono`.
- El saldo actual de un cliente = `SaldoAcumulado` del último `MovimientoCC` del cliente
  (no se hace SUM histórico).

Reglas que hay que respetar al tocar este código:

- **Venta/cobro y su movimiento van en una transacción.** Como el `DbContext` usa
  `EnableRetryOnFailure`, la transacción debe abrirse a través de
  `Database.CreateExecutionStrategy()`; `BeginTransactionAsync` suelto tira excepción.
- **Bloquear al cliente antes de leer el saldo** (`BloquearClienteAsync`, un `SELECT … FOR UPDATE`).
  Sin eso, dos operaciones simultáneas leen el mismo saldo previo y una pisa a la otra.
- **El saldo puede ser negativo**: significa crédito a favor del cliente (pagó de más).
  No recortarlo con `Math.Max(0, …)` — eso hace desaparecer plata del libro mayor.
- **`SaldoAcumulado` es un valor materializado.** Si se elimina un movimiento del medio hay que
  llamar a `RecalcularSaldosAsync` para rehacer la cadena posterior.
- `MovimientosCC` referencia `Ventas` y `Cobros` con FK `RESTRICT`: para borrar una venta de
  cuenta corriente hay que borrar antes su movimiento.

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
