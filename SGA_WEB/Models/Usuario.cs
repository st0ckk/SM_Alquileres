namespace SGA_WEB.Models
{
    public class Usuario
    {
        public string Identificacion { get; set; } = string.Empty;
        public string Contrasenna { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Direccion { get; set; } = string.Empty;
    }
}
