using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace TalonAdmin.ActionResults
{
    public class FileResult : IHttpActionResult
    {
        private readonly string _filePath;
        private readonly string _attachmentName;
        private readonly byte[] _fileContent;
        private readonly string _contentType;

        public FileResult(string filePath, string attachmentName = null, string contentType = null)
        {
            if (filePath == null) throw new ArgumentNullException("filePath");

            _filePath = filePath;
            _attachmentName = attachmentName;
            _fileContent = null;
            _contentType = contentType;
        }

        public FileResult(byte[] fileContent, string attachmentName = null, string contentType = null)
        {
            if (fileContent == null) throw new ArgumentNullException("fileContent");

            _filePath = null;
            _attachmentName = attachmentName;
            _fileContent = fileContent;
            _contentType = contentType;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var stream = _filePath == null ? (Stream)new MemoryStream(_fileContent) : (Stream)File.OpenRead(_filePath);

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(stream)
                };

                if (_attachmentName != null)
                {
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = _attachmentName
                    };
                }

                var contentType = _contentType ?? MimeMapping.GetMimeMapping(Path.GetExtension(_filePath));
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                return response;

            }, cancellationToken);
        }
    }

    public static class FileResultExensions
    {
        public static IHttpActionResult File(this ApiController controller, string filePath, string attachementName, string contentType = null)
        {
            return new FileResult(filePath, attachementName, contentType);
        }

        public static IHttpActionResult File(this ApiController controller, byte[] fileContent, string attachementName, string contentType = null)
        {
            return new FileResult(fileContent, attachementName, contentType);
        }
    }
}