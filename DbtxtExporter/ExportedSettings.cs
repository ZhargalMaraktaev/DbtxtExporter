using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbtxtExporter.Data
{
    public class ExportSettings
    {
        public string OutputPath { get; set; } = "";
        public string FilePrefix { get; set; } = "data";
    }
}
