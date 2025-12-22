using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbtxtExporter.Models
{
    public class NewUostIzmLinDelay
    {
        public int Id { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public int Pipes { get; set; }
        public int? Bad { get; set; }
        public int PlanA { get; set; }
        public int DelayTime { get; set; }
        public int Smena { get; set; }
        public decimal Veracity { get; set; }
        public float? AvgCycle { get; set; }
    }
}
