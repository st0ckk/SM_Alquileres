namespace SGA_WEB.Models
{
    public class Propiedad
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Habitaciones { get; set; }
        public int Banos { get; set; }
        public decimal Area { get; set; }
        public string Tipo { get; set; } = string.Empty; // Casa, Apartamento, Villa, etc.
        public string ImagenUrl { get; set; } = string.Empty;
        public bool Disponible { get; set; } = true;
    }
}
