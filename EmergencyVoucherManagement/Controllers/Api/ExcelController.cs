using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using EmergencyVoucherManagement.Controllers.Api;
using OfficeOpenXml;
using EmergencyVoucherManagement.ActionResults;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net.Http;
using System.Net;
using System.Text;

namespace EmergencyVoucherManagement.Controllers
{
    //[Authorize]
    [RoutePrefix("api/Excel")]
    public class ExcelController : ApiController
    {
        [HttpGet]
        [Route("ExportBeneficiaries")]
        public Task<IHttpActionResult> ExportBeneficiaries()
        {
            var result = new Task<IHttpActionResult>(() =>
            {
                using (var ctx = new Models.Vouchers.Context())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;


                    var allBeneficiaries = ctx.Beneficiaries.ToList();
                    var from = JToken.FromObject(allBeneficiaries);
                    var to = from.ToObject<DataTable>();

                    using (var package = new ExcelPackage())
                    {
                        var beneficiaries = package.Workbook.Worksheets.Add("Beneficiaries");
                        for (int i = 0; i < allBeneficiaries.Count; i++)
                        {
                            var beneficiary = allBeneficiaries[i];
                            beneficiaries.Cells["A1"].LoadFromDataTable(to, true);
                        }
                        var outputStream = new MemoryStream();
                        package.SaveAs(outputStream);
                        return this.File(outputStream.ToArray(), "Beneficiaries.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    }

                }
            });
            result.Start();

            return result;
        }

        [Route("ImportBeneficiaries")]
        public async Task<IHttpActionResult> PostFile()
        {
            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                return BadRequest();
            }

            string root = System.Web.HttpContext.Current.Server.MapPath("~/App_Data/uploads");
            var provider = new MultipartFormDataStreamProvider(root);
            StringBuilder b = new StringBuilder();

            var streamProvider = new MultipartFormDataStreamProvider(root);
            await Request.Content.ReadAsMultipartAsync(streamProvider);
            foreach (MultipartFileData fileData in streamProvider.FileData)
            {
                if (string.IsNullOrEmpty(fileData.Headers.ContentDisposition.FileName))
                {
                    return BadRequest("This request is not properly formatted");
                }
                string fileName = fileData.Headers.ContentDisposition.FileName;
                if (fileName.StartsWith("\"") && fileName.EndsWith("\""))
                {
                    fileName = fileName.Trim('"');
                }
                if (fileName.Contains(@"/") || fileName.Contains(@"\"))
                {
                    fileName = Path.GetFileName(fileName);
                }
                var fileBytes = File.ReadAllBytes(fileData.LocalFileName);
                using (var ctx = new Models.Vouchers.Context())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;
                    var allBeneficiaries = ctx.Beneficiaries.ToList();

                using (var package = new ExcelPackage(new MemoryStream(fileBytes)))
                {
                    if (package.Workbook.Worksheets.Where(s => s.Name == "Beneficiaries").Any())
                    {
                        var beneficiaries = package.Workbook.Worksheets["Beneficiaries"];

                        var columns = Enumerable.Range(1, beneficiaries.Dimension.End.Column)
                            .Select(i => (beneficiaries.Cells[1, i].Value??"").ToString())
                            .ToArray();

                        for (int i = 2; i <= beneficiaries.Dimension.End.Row; i++)
                        {
                            var jBeneficiary = new JObject();

                            columns.Select((c, z) => new { Index= z, ColumnName =c}).ToList()
                                .ForEach(o => jBeneficiary.Add(o.ColumnName, JToken.FromObject(beneficiaries.Cells[i, o.Index + 1].Value ?? "")));

                            b.AppendLine(JToken.FromObject(jBeneficiary.ToObject<Models.Vouchers.Beneficiary>()).ToString());
                            b.AppendLine(jBeneficiary.ToString());
                        }

                    }
                }

                }


                File.Delete(fileData.LocalFileName);
                //File.Move(fileData.LocalFileName, Path.Combine(root, fileName));
            }


            return Ok(b.ToString());
        }
    }
}