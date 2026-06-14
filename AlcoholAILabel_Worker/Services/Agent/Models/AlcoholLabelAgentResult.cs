using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Agent.Models;

public class AlcoholLabelAgentResult
{
    public string? BrandName { get; set; }

    public string? ClassTypeDesignation { get; set; }

    public string? AlcoholContent { get; set; }

    public string? NetContents { get; set; }

    public string? BottlerProducerNameAddress { get; set; }

    public string? CountryOfOrigin { get; set; }

    public string? GovernmentWarning { get; set; }

    public AlcoholLabelConfidence Confidence { get; set; } = new();

    public bool UsedAi { get; set; }

    public bool NeedsHumanReview { get; set; }
}
