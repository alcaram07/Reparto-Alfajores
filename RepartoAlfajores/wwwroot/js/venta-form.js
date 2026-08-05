// Formulario de venta, compartido entre "Nueva venta" y "Editar venta".
//
// Espera tres globales, declaradas por la vista antes de cargar este archivo:
//   productPrices  { productoId: precio }  precios actuales del catálogo
//   productNames   { productoId: nombre }
//   initialItems   [{ productId, qty, precio, nombre }]  líneas ya cargadas (vacío al crear)
//
// Al servidor sólo viajan ProductoId y Cantidad: el precio lo resuelve VentaService, que
// conserva el precio congelado de las líneas que ya existían en la venta.

const ESTADO_VACIO = `
    <div class="text-center text-muted py-4">
        <i class="bi bi-cart" style="font-size:2rem;opacity:0.2"></i>
        <p class="mt-2 mb-0" style="font-size:0.85rem">Sin productos agregados</p>
    </div>`;

let items = Array.isArray(window.initialItems) ? window.initialItems.slice() : [];

function agregarProducto() {
    const sel = document.getElementById('productoSelect');
    const productId = parseInt(sel.value);
    if (!productId) return;
    const qty = parseInt(document.getElementById('cantidadInput').value) || 1;
    const nombre = productNames[productId] || sel.options[sel.selectedIndex].text;

    const existing = items.findIndex(i => i.productId === productId);
    if (existing >= 0) {
        items[existing].qty += qty;
    } else {
        // Producto nuevo en esta venta: precio actual del catálogo.
        items.push({ productId, qty, precio: productPrices[productId] || 0, nombre });
    }
    renderItems();
}

function eliminarItem(idx) {
    items.splice(idx, 1);
    renderItems();
}

function renderItems() {
    const hidden = document.getElementById('detallesHidden');
    const resumen = document.getElementById('resumenContainer');

    hidden.innerHTML = '';
    items.forEach((item, i) => {
        hidden.innerHTML += `<input type="hidden" name="Detalles[${i}].ProductoId" value="${item.productId}">`;
        hidden.innerHTML += `<input type="hidden" name="Detalles[${i}].Cantidad" value="${item.qty}">`;
    });

    // Antes había un early return acá cuando la lista quedaba vacía, y dejaba la tabla
    // anterior en pantalla con el total viejo. Se asigna siempre, y el total se actualiza
    // pase lo que pase.
    if (items.length === 0) {
        resumen.innerHTML = ESTADO_VACIO;
    } else {
        let filas = '';
        items.forEach((item, i) => {
            const subtotal = item.qty * item.precio;
            filas += `<tr>
                <td>${item.nombre.split('(')[0].trim()}</td>
                <td class="text-center">${item.qty}</td>
                <td class="text-end fw-semibold">$${subtotal.toLocaleString('es-AR', { minimumFractionDigits: 2 })}</td>
                <td><button type="button" class="btn btn-sm btn-outline-danger py-0 px-1" onclick="eliminarItem(${i})"><i class="bi bi-x"></i></button></td>
            </tr>`;
        });
        resumen.innerHTML =
            `<table class="table table-hover mb-0" style="font-size:0.87rem"><tbody>${filas}</tbody></table>`;
    }

    updateTotal();
}

function updateTotal() {
    const total = items.reduce((s, i) => s + i.qty * i.precio, 0);
    document.getElementById('totalDisplay').textContent =
        '$' + total.toLocaleString('es-AR', { minimumFractionDigits: 2 });
}

renderItems();
