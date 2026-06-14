using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Ocr;

public interface IOcrService
{
    Task<string> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}