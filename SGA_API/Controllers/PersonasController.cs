using Microsoft.AspNetCore.Mvc;
using SGA.Infrastructure.Services;

namespace SGA_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonasController : ControllerBase
{
    private readonly ISgaDataService _sgaDataService;

    public PersonasController(ISgaDataService sgaDataService)
    {
        _sgaDataService = sgaDataService;
    }

    [HttpGet("cedula/{cedula}")]
    public async Task<IActionResult> BuscarPorCedula(string cedula)
    {
        var dato = await _sgaDataService.GetClientByCedulaAsync(cedula);
        if (dato is null)
        {
            return NotFound();
        }

        var partes = (dato.NombreCompleto ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nombre = partes.Length > 0 ? partes[0] : string.Empty;
        var apellidos = partes.Length > 1 ? string.Join(" ", partes.Skip(1)) : string.Empty;

        return Ok(new
        {
            dato.Cedula,
            Nombre = nombre,
            Apellidos = apellidos,
            dato.Telefono,
            dato.Email
        });
    }
}
