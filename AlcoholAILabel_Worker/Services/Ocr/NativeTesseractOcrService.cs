using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AlcoholAILabel_Worker.Services.Ocr;

public class NativeTesseractOcrService : IOcrService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NativeTesseractOcrService> _logger;

    public NativeTesseractOcrService(
        IConfiguration configuration,
        ILogger<NativeTesseractOcrService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [DllImport(
        "AlcoholAILabel_OcrNative.dll",
        CharSet = CharSet.Unicode,
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int OcrImageW(
        string imagePath,
        string tesseractExePath,
        StringBuilder outputBuffer,
        int outputBufferLength);

    public Task<string> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException(
                "Image file was not found.",
                imagePath);
        }

        var tesseractExePath = _configuration["OcrSettings:TesseractExePath"];

        if (string.IsNullOrWhiteSpace(tesseractExePath))
        {
            throw new InvalidOperationException(
                "OcrSettings:TesseractExePath is missing.");
        }

        if (!File.Exists(tesseractExePath))
        {
            throw new FileNotFoundException(
                "Tesseract executable was not found.",
                tesseractExePath);
        }

        var bufferLength = 100_000;
        var outputBuffer = new StringBuilder(bufferLength);

        _logger.LogInformation(
            "Calling native OCR DLL for image {ImagePath}",
            imagePath);

        var resultCode = OcrImageW(
            imagePath,
            tesseractExePath,
            outputBuffer,
            bufferLength);

        if (resultCode != 0)
        {
            throw new InvalidOperationException(
                $"Native OCR failed. Error code: {resultCode}");
        }

        var rawText = outputBuffer.ToString();

        if (string.IsNullOrWhiteSpace(rawText))
        {
            _logger.LogWarning(
                "OCR returned empty text for image {ImagePath}",
                imagePath);
        }

        return Task.FromResult(rawText);
    }
}

