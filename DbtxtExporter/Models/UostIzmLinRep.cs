using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbtxtExporter.Models
{
    public class UostIzmLinRep
    {
        public int id { get; set; }
        public DateTime DateTime { get; set; }
        public int? Smena { get; set; }
        public int? Plan { get; set; }
        public int? DelayTimePrevHour { get; set; }
        public int? DelayTimeThisHour { get; set; }
    }
}
