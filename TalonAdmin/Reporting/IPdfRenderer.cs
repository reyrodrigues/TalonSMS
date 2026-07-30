using System.Threading.Tasks;

namespace TalonAdmin.Reporting
{
    public enum PdfOrientation
    {
        Portrait,
        Landscape
    }

    /// <summary>
    /// Abstraction over HTML-to-PDF conversion. The original implementation was TuesPechkin
    /// driving a Win64-only wkhtmltopdf build, which cannot run on any current runtime target.
    /// </summary>
    public interface IPdfRenderer
    {
        Task<byte[]> RenderAsync(string html, string paperSize = "A4", PdfOrientation orientation = PdfOrientation.Portrait);
    }
}
