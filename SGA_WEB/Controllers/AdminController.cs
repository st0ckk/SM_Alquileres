using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SGA.Infrastructure.Services;
using SGA_WEB.Models;

namespace SGA_WEB.Controllers;

public class AdminController : Controller
{
    private readonly ISgaDataService _sgaDataService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ISgaDataService sgaDataService, IWebHostEnvironment env, ILogger<AdminController> logger)
    {
        _sgaDataService = sgaDataService;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fechaDesde, DateTime? fechaHasta, string? estado, string? tab = null, int? editar = null)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para acceder a esta seccion.";
            return RedirectToAction("Index", "Home");
        }

        var propiedades = await _sgaDataService.GetPropiedadesAdminAsync();
        EditarPropiedadFormModel? formEdicion = null;
        if (editar is > 0)
        {
            var fila = propiedades.FirstOrDefault(p => p.IdPropiedad == editar.Value);
            if (fila is null)
            {
                TempData["Error"] = "No se encontró la propiedad indicada.";
            }
            else
            {
                formEdicion = new EditarPropiedadFormModel
                {
                    IdPropiedad = fila.IdPropiedad,
                    Tipo = fila.Tipo,
                    Descripcion = fila.Descripcion,
                    PrecioMensualTexto = MontoEntrada.Formatear(fila.PrecioMensual),
                    Ubicacion = fila.Ubicacion,
                    EspacioTotalM2 = fila.EspacioTotalM2,
                    NumeroPiso = fila.NumeroPiso,
                    NumeroHabitaciones = fila.NumeroHabitaciones,
                    EstacionamientoDisponible = fila.EstacionamientoDisponible,
                    ProcesoPago = fila.ProcesoPago,
                    UrlFotoActual = fila.UrlFoto
                };
            }
        }

        var vm = new AdminDashboardViewModel
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            Estado = estado ?? string.Empty,
            Citas = await _sgaDataService.GetCitasAsync(fechaDesde, fechaHasta, estado),
            Clientes = await _sgaDataService.GetClientesAsync(),
            Mensajes = await _sgaDataService.GetMensajesContactoAsync(),
            Propiedades = propiedades,
            FormularioEdicion = formEdicion
        };

        ViewBag.ActiveTab = string.Equals(tab, "clientes", StringComparison.OrdinalIgnoreCase)
            ? "clientes"
            : string.Equals(tab, "mensajes", StringComparison.OrdinalIgnoreCase)
                ? "mensajes"
                : string.Equals(tab, "propiedades", StringComparison.OrdinalIgnoreCase)
                    ? "propiedades"
                    : "citas";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPropiedad(CrearPropiedadInputModel modelo)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para registrar propiedades.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        ArgumentNullException.ThrowIfNull(modelo);

        var tiposValidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Apartamento", "Casa", "Villa" };
        if (!tiposValidos.Contains(modelo.Tipo))
        {
            TempData["Error"] = "Tipo de propiedad no valido.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revise los datos del formulario de propiedad.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        if (!MontoEntrada.TryParse(modelo.PrecioMensualTexto, out var precio, out var errPrecio))
        {
            TempData["Error"] = errPrecio ?? "Precio no válido.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        var emailSesion = HttpContext.Session.GetString("UsuarioEmail");
        if (string.IsNullOrWhiteSpace(emailSesion))
        {
            TempData["Error"] = "Sesion invalida.";
            return RedirectToAction("Index", "Home");
        }

        var idUsuario = await _sgaDataService.GetUsuarioIdByEmailAsync(emailSesion);
        if (idUsuario is null)
        {
            TempData["Error"] = "No se pudo identificar al administrador en la base de datos.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        var (urlFoto, errFoto) = await TryGuardarFotoNuevaAsync(modelo.Foto);
        if (errFoto is not null)
        {
            TempData["Error"] = errFoto;
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        try
        {
            await _sgaDataService.InsertarPropiedadAsync(
                idUsuario.Value,
                modelo.Tipo.Trim(),
                modelo.Descripcion.Trim(),
                precio,
                modelo.Ubicacion.Trim(),
                modelo.EspacioTotalM2,
                modelo.NumeroPiso.Trim(),
                modelo.NumeroHabitaciones,
                modelo.EstacionamientoDisponible,
                modelo.ProcesoPago.Trim(),
                urlFoto);
            TempData["Success"] = "Propiedad registrada correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al insertar propiedad.");
            TempData["Error"] = "No se pudo guardar la propiedad. Intente de nuevo.";
        }

        return RedirectToAction("Index", new { tab = "propiedades" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarPropiedad(EditarPropiedadFormModel modelo)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para editar propiedades.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        ArgumentNullException.ThrowIfNull(modelo);

        var tiposValidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Apartamento", "Casa", "Villa" };
        if (!tiposValidos.Contains(modelo.Tipo))
        {
            TempData["Error"] = "Tipo de propiedad no valido.";
            return RedirectToAction("Index", new { tab = "propiedades", editar = modelo.IdPropiedad });
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revise los datos del formulario de edición.";
            return RedirectToAction("Index", new { tab = "propiedades", editar = modelo.IdPropiedad });
        }

        if (!MontoEntrada.TryParse(modelo.PrecioMensualTexto, out var precio, out var errPrecio))
        {
            TempData["Error"] = errPrecio ?? "Precio no válido.";
            return RedirectToAction("Index", new { tab = "propiedades", editar = modelo.IdPropiedad });
        }

        var (nuevaRuta, errFoto) = await TryGuardarFotoNuevaAsync(modelo.Foto);
        if (errFoto is not null)
        {
            TempData["Error"] = errFoto;
            return RedirectToAction("Index", new { tab = "propiedades", editar = modelo.IdPropiedad });
        }

        var urlFinal = nuevaRuta ?? (string.IsNullOrWhiteSpace(modelo.UrlFotoActual) ? null : modelo.UrlFotoActual.Trim());
        var anteriorSubida = string.IsNullOrWhiteSpace(modelo.UrlFotoActual) ? null : modelo.UrlFotoActual.Trim();

        try
        {
            await _sgaDataService.ActualizarPropiedadAsync(
                modelo.IdPropiedad,
                modelo.Tipo.Trim(),
                modelo.Descripcion.Trim(),
                precio,
                modelo.Ubicacion.Trim(),
                modelo.EspacioTotalM2,
                modelo.NumeroPiso.Trim(),
                modelo.NumeroHabitaciones,
                modelo.EstacionamientoDisponible,
                modelo.ProcesoPago.Trim(),
                urlFinal);

            if (nuevaRuta is not null && !string.Equals(anteriorSubida, nuevaRuta, StringComparison.OrdinalIgnoreCase))
            {
                TryEliminarArchivoFotoSubida(anteriorSubida);
            }

            TempData["Success"] = "Propiedad actualizada correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar propiedad {Id}.", modelo.IdPropiedad);
            TempData["Error"] = "No se pudo actualizar la propiedad.";
            if (nuevaRuta is not null)
            {
                TryEliminarArchivoFotoSubida(nuevaRuta);
            }
        }

        return RedirectToAction("Index", new { tab = "propiedades" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPropiedadDisponible(int idPropiedad, bool disponible)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para cambiar la disponibilidad.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        if (idPropiedad <= 0)
        {
            TempData["Error"] = "Identificador de propiedad no valido.";
            return RedirectToAction("Index", new { tab = "propiedades" });
        }

        try
        {
            await _sgaDataService.SetPropiedadDisponibleAsync(idPropiedad, disponible);
            TempData["Success"] = disponible ? "Propiedad reactivada: vuelve a mostrarse en la web." : "Propiedad desactivada: ya no aparece para los clientes.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar disponibilidad de propiedad {Id}.", idPropiedad);
            TempData["Error"] = "No se pudo actualizar el estado de la propiedad.";
        }

        return RedirectToAction("Index", new { tab = "propiedades" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCitaEstado(int idCita, string estado)
    {
        var role = HttpContext.Session.GetString("UsuarioRol");
        if (!string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "No tiene permisos para actualizar citas.";
            return RedirectToAction("Index", new { tab = "citas" });
        }

        var estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();
        if (estadoNormalizado is not ("PENDIENTE" or "APROBADA" or "CANCELADA"))
        {
            TempData["Error"] = "Estado de cita no valido.";
            return RedirectToAction("Index", new { tab = "citas" });
        }

        await _sgaDataService.UpdateCitaStatusAsync(idCita, estadoNormalizado);
        TempData["Success"] = "Estado de cita actualizado.";
        return RedirectToAction("Index", new { tab = "citas" });
    }

    private async Task<(string? relativePath, string? error)> TryGuardarFotoNuevaAsync(IFormFile? archivo)
    {
        if (archivo is not { Length: > 0 })
        {
            return (null, null);
        }

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return (null, "La foto debe ser JPG, PNG o WEBP.");
        }

        if (archivo.Length > 5 * 1024 * 1024)
        {
            return (null, "La foto no debe superar 5 MB.");
        }

        var uploads = Path.Combine(_env.WebRootPath, "uploads", "propiedades");
        Directory.CreateDirectory(uploads);
        var nombre = $"{Guid.NewGuid():N}{ext}";
        var rutaFisica = Path.Combine(uploads, nombre);
        await using (var fs = new FileStream(rutaFisica, FileMode.CreateNew))
        {
            await archivo.CopyToAsync(fs);
        }

        return ("/uploads/propiedades/" + nombre, null);
    }

    private void TryEliminarArchivoFotoSubida(string? urlRelativa)
    {
        if (string.IsNullOrWhiteSpace(urlRelativa) ||
            !urlRelativa.StartsWith("/uploads/propiedades/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nombre = Path.GetFileName(urlRelativa);
        if (string.IsNullOrEmpty(nombre) || nombre.Contains("..", StringComparison.Ordinal))
        {
            return;
        }

        var full = Path.Combine(_env.WebRootPath, "uploads", "propiedades", nombre);
        try
        {
            if (System.IO.File.Exists(full))
            {
                System.IO.File.Delete(full);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la foto anterior {Ruta}.", full);
        }
    }
}
