using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARM.Dolar.Guru.Models
{
    public class ProyeccionDolarJson
    {
        public DateTime FechaEjecucion { get; set; }
        public List<ProyeccionDolar> Proyecciones { get; set; } = new();
    }

    public class ProyeccionDolar
    {
        public string Nombre { get; set; } = "";
        public string Icono { get; set; } = "";
        public decimal ValorSemana { get; set; }
        public decimal ValorMes { get; set; }
        public string Justificacion { get; set; } = "";
        public double Confianza { get; set; } // 0.0 a 1.0
    }
}
