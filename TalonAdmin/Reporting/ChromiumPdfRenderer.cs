using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Renders report HTML with headless Chromium. The browser is downloaded on first use and
    /// cached, so a deployment that never asks for a PDF never pays the download cost.
    /// </summary>
    public class ChromiumPdfRenderer : IPdfRenderer, IAsyncDisposable
    {
        private readonly ILogger<ChromiumPdfRenderer> _logger;
        private readonly SemaphoreSlim _startupLock = new SemaphoreSlim(1, 1);
        private IBrowser _browser;

        public ChromiumPdfRenderer(ILogger<ChromiumPdfRenderer> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> RenderAsync(string html, string paperSize = "A4", PdfOrientation orientation = PdfOrientation.Portrait)
        {
            var browser = await GetBrowserAsync();

            await using var page = await browser.NewPageAsync();
            // Report HTML is self-contained (images arrive as data URIs), so there is nothing to
            // wait on beyond the document itself.
            await page.SetContentAsync(html, new SetContentOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Load }
            });

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = ResolvePaperFormat(paperSize),
                Landscape = orientation == PdfOrientation.Landscape,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "1cm",
                    Bottom = "1cm",
                    Left = "1cm",
                    Right = "1cm"
                }
            });
        }

        private static PaperFormat ResolvePaperFormat(string paperSize)
        {
            if (string.IsNullOrWhiteSpace(paperSize))
            {
                return PaperFormat.A4;
            }

            switch (paperSize.Trim().ToUpperInvariant())
            {
                case "A3": return PaperFormat.A3;
                case "A5": return PaperFormat.A5;
                case "LETTER": return PaperFormat.Letter;
                case "LEGAL": return PaperFormat.Legal;
                case "TABLOID": return PaperFormat.Tabloid;
                default: return PaperFormat.A4;
            }
        }

        private async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser != null && _browser.IsConnected)
            {
                return _browser;
            }

            await _startupLock.WaitAsync();
            try
            {
                if (_browser != null && _browser.IsConnected)
                {
                    return _browser;
                }

                var options = new LaunchOptions { Headless = true };

                // Container images install Chromium through the package manager and point at it
                // with PUPPETEER_EXECUTABLE_PATH; only fetch a browser when there isn't one.
                var executablePath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
                if (!string.IsNullOrWhiteSpace(executablePath) && System.IO.File.Exists(executablePath))
                {
                    _logger.LogInformation("Using the Chromium build at {Path} for PDF rendering.", executablePath);
                    options.ExecutablePath = executablePath;
                    options.Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" };
                }
                else
                {
                    _logger.LogInformation("Downloading a headless Chromium build for PDF rendering (first use only).");
                    await new BrowserFetcher().DownloadAsync();
                }

                _browser = await Puppeteer.LaunchAsync(options);
                return _browser;
            }
            finally
            {
                _startupLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_browser != null)
            {
                await _browser.DisposeAsync();
                _browser = null;
            }

            _startupLock.Dispose();
        }
    }
}
