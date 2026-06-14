using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Agent.Models
{
    public class AlcoholLabelConfidence
    {
        public double BrandName { get; set; }

        public double ClassTypeDesignation { get; set; }

        public double AlcoholContent { get; set; }

        public double NetContents { get; set; }

        public double BottlerProducerNameAddress { get; set; }

        public double CountryOfOrigin { get; set; }

        public double GovernmentWarning { get; set; }
    }
}
