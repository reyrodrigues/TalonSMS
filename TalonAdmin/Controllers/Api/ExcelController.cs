using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using TalonAdmin.Controllers.Api;
using OfficeOpenXml;
using TalonAdmin.ActionResults;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Data.Entity;
using TalonAdmin.Extensions;
using Microsoft.AspNet.Identity;
using TalonAdmin.Models.Vouchers;
using System.Web.Hosting;

namespace TalonAdmin.Controllers
{
    //[Authorize]
    [RoutePrefix("api/Excel")]
    public class ExcelController : ApiController
    {
        /// <summary>
        /// Simple helper method that validates whether the user trying to run an import and an export has actual permossions to do so
        /// </summary>
        private bool ValidateBeneficiaryRequest(int organizationId, int countryId)
        {
            using (var ctx = new Models.Admin.AdminContext())
            {
                var userId = this.RequestContext.Principal.Identity.GetUserId();
                if (!String.IsNullOrEmpty(userId))
                {
                    var user = ctx.Users.Where(u => u.Id == userId).FirstOrDefault();
                    if (user != null)
                    {

                        return user.Organization.Id == organizationId && user.Countries.Where(c => c.Id == countryId).Any();
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Validates the request to make sure the user trying to import the data has the permissions.
        /// </summary>
        private bool ValidateVendorRequest(int countryId)
        {
            using (var ctx = new Models.Admin.AdminContext())
            {
                var userId = this.RequestContext.Principal.Identity.GetUserId();
                if (!String.IsNullOrEmpty(userId))
                {
                    var user = ctx.Users.Where(u => u.Id == userId).FirstOrDefault();
                    if (user != null)
                    {

                        return user.Countries.Where(c => c.Id == countryId).Any();
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Web Api method that exports all beneficiaries in the database for this organization and country
        /// </summary>
        /// <param name="organizationId">Organization Id for the list of beneficiaries</param>
        /// <param name="countryId">Country of beneficiaries</param>
        /// <returns>Attachment with Excel Spreadsheet</returns>
        [Route("ExportBeneficiaries")]
        [HostAuthentication(DefaultAuthenticationTypes.ApplicationCookie)]
        public async Task<IHttpActionResult> ExportBeneficiaries(int organizationId, int countryId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var beneficiaryQuery = await ctx.Beneficiaries
                    .Include("Group")
                    .Include("Location")
                    .Where(b => b.OrganizationId == organizationId && b.CountryId == countryId)
                    .ToArrayAsync();

                var jsonBeneficiaries = JToken.FromObject(beneficiaryQuery) as JArray;
                foreach (var jsonBeneficiary in jsonBeneficiaries)
                {
                    jsonBeneficiary["Group"] = jsonBeneficiary["Group"].Type != JTokenType.Null ? jsonBeneficiary["Group"]["Name"] : "";
                    jsonBeneficiary["Location"] = jsonBeneficiary["Location"].Type != JTokenType.Null ? jsonBeneficiary["Location"]["Name"] : "";
                    jsonBeneficiary["Sex"] = jsonBeneficiary["Sex"].Type != JTokenType.Null ? (jsonBeneficiary["Sex"].ToString() == "0" ? "Male" : "Female") : "";
                }

                var dataTable = jsonBeneficiaries.ToObject<DataTable>();

                if (beneficiaryQuery.Count() == 0)
                {
                    dataTable = JToken.FromObject(new Beneficiary[] { new Beneficiary() }).ToObject<DataTable>();
                    dataTable.Rows.Clear();
                }

                dataTable.TableName = "Beneficiaries";

                // Removing Id Columns because they are parsed later on in the import
                dataTable.Columns.Remove("Name");
                dataTable.Columns.Remove("GroupId");
                dataTable.Columns.Remove("LocationId");
                dataTable.Columns.Remove("Distributions");
                dataTable.Columns.Remove("OrganizationId");
                dataTable.Columns.Remove("CountryId");
                dataTable.Columns.Remove("WasWelcomeMessageSent");
                dataTable.Columns.Remove("PIN");

                return this.File(dataTable.ToExcelSpreadsheet(), "Beneficiaries.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
        }

        /// <summary>
        /// Web Api method that takes in an upload of a spreadsheet with a sheet called beneficiaries and extracts beneficiary data from it.
        /// </summary>
        /// <param name="organizationId">Organization Id for the list of beneficiaries</param>
        /// <param name="countryId">Country of beneficiaries</param>
        /// <returns>JSON with any import errors in details</returns>
        [Route("ImportBeneficiaries")]
        public async Task<IHttpActionResult> ImportBeneficiaries(int organizationId, int countryId)
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BadRequest();
            }

            dynamic response = new JObject();
            response.Errors = new JArray();

            string root = HostingEnvironment.MapPath("~/App_Data/uploads");
            var provider = new MultipartFormDataStreamProvider(root);

            var streamProvider = new MultipartFormDataStreamProvider(root);
            await Request.Content.ReadAsMultipartAsync(streamProvider);

            foreach (MultipartFileData fileData in streamProvider.FileData)
            {
                var fileBytes = File.ReadAllBytes(fileData.LocalFileName);

                // No need to keep the file lying around
                File.Delete(fileData.LocalFileName);

                Models.Admin.Country country = null;

                using (var ctx = new Models.Admin.AdminContext())
                {
                    country = await ctx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                using (var ctx = new Models.Vouchers.Context())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    var beneficiaryQuery = await ctx.Beneficiaries.ToListAsync();
                    var locationQuery = ctx.Locations.Where(l => l.CountryId == countryId);
                    var groupQuery = ctx.BeneficiaryGroups.Where(g => g.CountryId == countryId && g.OrganizationId == organizationId);

                    var package = new ExcelPackage(new MemoryStream(fileBytes));
                    var excelData = package.ExtractData();
                    package.Dispose();

                    if (excelData.ContainsKey("Beneficiaries"))
                    {
                        foreach (var jsonBeneficiary in excelData["Beneficiaries"])
                        {
                            try
                            {
                                var beneficiaryId = jsonBeneficiary.PropertyValueIfExists<int?>("Id");
                                var groupName = jsonBeneficiary.PropertyValueIfExists<string>("Group");
                                var locationName = jsonBeneficiary.PropertyValueIfExists<string>("Location");

                                jsonBeneficiary["Sex"] = (jsonBeneficiary.PropertyValueIfExists<string>("Sex") ?? "").ToString().Trim().ToLower() == "male" ? 0 : 1;

                                jsonBeneficiary.Remove("Name");

                                // Removing string fields
                                jsonBeneficiary.Remove("Group");
                                jsonBeneficiary.Remove("Location");

                                // Trust no one
                                jsonBeneficiary.Remove("OrganizationId");
                                jsonBeneficiary.Remove("CountryId");

                                var isNew = beneficiaryId == null;
                                Models.Vouchers.Beneficiary beneficiary = null;

                                if (beneficiaryId != null)
                                    beneficiary = beneficiaryQuery.Where(o => o.Id == beneficiaryId.Value).FirstOrDefault();
                                else
                                    beneficiary = new Models.Vouchers.Beneficiary();

                                if (beneficiary == null) throw new Exception("This beneficiary belongs to another organization or another country."); // Something doesn't smell right

                                if (isNew)
                                {
                                    jsonBeneficiary["Id"] = 0;
                                    jsonBeneficiary["CountryId"] = countryId;
                                    jsonBeneficiary["OrganizationId"] = organizationId;
                                }

                                var numberRegex = new System.Text.RegularExpressions.Regex(String.Format("^(\\+{0}|{0}|0|1)", country.CountryCallingCode));
                                jsonBeneficiary["MobileNumber"] = String.Format("+{0}{1}", country.CountryCallingCode, numberRegex.Replace(jsonBeneficiary["MobileNumber"].ToString(), ""));


                                jsonBeneficiary.MergeChangesInto(beneficiary);

                                #region Assigning Group

                                BeneficiaryGroup group = null;

                                // If group is filled out, try to find it or create new one
                                if (!String.IsNullOrEmpty(groupName))
                                {
                                    group = groupQuery.Where(g => g.Name.ToLower().Trim() == groupName.Trim().ToLower()).FirstOrDefault();

                                    if (group == null)
                                    {
                                        group = new Models.Vouchers.BeneficiaryGroup
                                        {
                                            Name = groupName,
                                            OrganizationId = organizationId,
                                            CountryId = countryId
                                        };

                                        ctx.BeneficiaryGroups.Add(group);
                                    }
                                }
                                if (group != null)
                                {
                                    beneficiary.Group = group;
                                    beneficiary.GroupId = group.Id;
                                }
                                else
                                {
                                    beneficiary.GroupId = null;
                                    beneficiary.Group = null;
                                }

                                #endregion

                                #region Assigning Location
                                Location location = null;

                                // If location is filled out try to find or create new one
                                if (!String.IsNullOrEmpty(locationName))
                                {
                                    location = locationQuery.Where(l => l.Name.ToLower().Trim() == locationName.Trim().ToLower()).FirstOrDefault();

                                    if (location == null)
                                    {
                                        location = new Models.Vouchers.Location
                                        {
                                            Name = locationName,
                                            CountryId = countryId
                                        };

                                        ctx.Locations.Add(location);
                                    }
                                }

                                if (location != null)
                                {
                                    beneficiary.Location = location;
                                    beneficiary.LocationId = location.Id;
                                }
                                else
                                {
                                    beneficiary.Location = null;
                                    beneficiary.LocationId = null;
                                }

                                #endregion

                                if (isNew)
                                {
                                    ctx.Beneficiaries.Add(beneficiary);
                                }

                                await ctx.SaveChangesAsync();
                            }
                            catch (Exception e)
                            {
                                response.Errors.Add(JObject.FromObject(new
                                {
                                    ErrorText = e.Message,
                                    Line = jsonBeneficiary["__RowNumber"]
                                }));
                            }
                        }
                    }
                }
            }


            return Ok<JObject>(response);
        }

        /// <summary>
        /// Web Api method that exports all vendors in the database for this  country
        /// </summary>
        /// <param name="countryId">Country of vendors</param>
        /// <returns>Attachment with Excel Spreadsheet</returns>
        [Route("ExportVendors")]
        public async Task<IHttpActionResult> ExportVendors([FromBody] int countryId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var vendorQuery = await ctx.Vendors
                    .Include("Location")
                    .Include("Type")
                    .Where(b => b.CountryId == countryId)
                    .ToArrayAsync();

                var jsonBeneficiaries = JToken.FromObject(vendorQuery) as JArray;
                foreach (var jsonVendor in jsonBeneficiaries)
                {
                    jsonVendor["Location"] = jsonVendor["Location"].Type != JTokenType.Null ? jsonVendor["Location"]["Name"] : "";
                    jsonVendor["Type"] = jsonVendor["Type"].Type != JTokenType.Null ? jsonVendor["Type"]["Name"] : "";
                }
                var dataTable = jsonBeneficiaries.ToObject<DataTable>();

                if (vendorQuery.Count() == 0)
                {
                    dataTable = JToken.FromObject(new Vendor[] { new Vendor() }).ToObject<DataTable>();
                    dataTable.Rows.Clear();
                }

                // Removing Id Columns because they are parsed later on in the import
                dataTable.Columns.Remove("LocationId");
                dataTable.Columns.Remove("CountryId");
                dataTable.Columns.Remove("TypeId");
                dataTable.Columns.Remove("Name");
                dataTable.Columns.Remove("ParentRecord");
                dataTable.Columns.Remove("ParentRecordId");

                dataTable.TableName = "Vendors";

                return this.File(dataTable.ToExcelSpreadsheet(), "Vendors.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
        }

        /// <summary>
        /// Web Api methods that allow uses to bulk import and update vendors in the system
        /// </summary>
        /// <param name="countryId">Country of vendors</param>
        /// <returns>JSON with any import errors in details</returns>
        [Route("ImportVendors")]
        public async Task<IHttpActionResult> ImportVendors(int countryId)
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BadRequest();
            }

            dynamic response = new JObject();
            response.Errors = new JArray();

            string root = HostingEnvironment.MapPath("~/App_Data/uploads");
            var provider = new MultipartFormDataStreamProvider(root);

            var streamProvider = new MultipartFormDataStreamProvider(root);
            await Request.Content.ReadAsMultipartAsync(streamProvider);

            foreach (MultipartFileData fileData in streamProvider.FileData)
            {
                var fileBytes = File.ReadAllBytes(fileData.LocalFileName);

                // No need to keep the file lying around
                File.Delete(fileData.LocalFileName);

                Models.Admin.Country country = null;

                using (var ctx = new Models.Admin.AdminContext())
                {
                    country = await ctx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                using (var ctx = new Models.Vouchers.Context())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    var vendorQuery = await ctx.Vendors.Where(v => v.CountryId == countryId).ToListAsync();
                    var locationQuery = ctx.Locations.Where(l => l.CountryId == countryId);
                    var vendorTypeQuery = ctx.VendorTypes.Where(l => l.CountryId == countryId);

                    var package = new ExcelPackage(new MemoryStream(fileBytes));
                    var excelData = package.ExtractData();
                    package.Dispose();

                    if (excelData.ContainsKey("Vendors"))
                    {
                        foreach (var jsonVendor in excelData["Vendors"])
                        {
                            try
                            {
                                var vendorId = jsonVendor.PropertyValueIfExists<int?>("Id");
                                var locationName = jsonVendor.PropertyValueIfExists<string>("Location");
                                var typeName = jsonVendor.PropertyValueIfExists<string>("Type");

                                jsonVendor.Remove("Location");
                                jsonVendor.Remove("Type");

                                // Trust no one
                                jsonVendor.Remove("CountryId");

                                var isNew = vendorId == null;
                                Models.Vouchers.Vendor vendor = null;

                                if (vendorId != null)
                                    vendor = vendorQuery.Where(o => o.Id == vendorId.Value).FirstOrDefault();
                                else
                                    vendor = new Models.Vouchers.Vendor();

                                if (vendor == null) throw new Exception("This vendor is already in another country."); // Something doesn't smell right

                                if (isNew)
                                {
                                    jsonVendor["Id"] = 0;
                                    jsonVendor["CountryId"] = countryId;
                                }

                                var numberRegex = new System.Text.RegularExpressions.Regex(String.Format("^(\\+{0}|{0}|0|1)", country.CountryCallingCode));
                                jsonVendor["MobileNumber"] = String.Format("+{0}{1}", country.CountryCallingCode, numberRegex.Replace(jsonVendor["MobileNumber"].ToString(), ""));


                                jsonVendor.MergeChangesInto(vendor);

                                #region Assigning Location
                                Location location = null;

                                // If location is filled out try to find or create new one
                                if (!String.IsNullOrEmpty(locationName))
                                {
                                    location = locationQuery.Where(l => l.Name.ToLower().Trim() == locationName.Trim().ToLower()).FirstOrDefault();

                                    if (location == null)
                                    {
                                        location = new Models.Vouchers.Location
                                        {
                                            Name = locationName,
                                            CountryId = countryId
                                        };

                                        ctx.Locations.Add(location);
                                    }
                                }

                                if (location != null)
                                {
                                    vendor.Location = location;
                                    vendor.LocationId = location.Id;
                                }
                                else
                                {
                                    vendor.Location = null;
                                    vendor.LocationId = null;
                                }


                                #endregion

                                #region Assigning Type
                                VendorType type = null;

                                // If location is filled out try to find or create new one
                                if (!String.IsNullOrEmpty(locationName))
                                {
                                    type = vendorTypeQuery.Where(l => l.Name.ToLower().Trim() == locationName.Trim().ToLower()).FirstOrDefault();

                                    if (type == null)
                                    {
                                        type = new Models.Vouchers.VendorType
                                        {
                                            Name = typeName,
                                            CountryId = countryId
                                        };

                                        ctx.VendorTypes.Add(type);
                                    }
                                }

                                if (type != null)
                                {
                                    vendor.Type = type;
                                    vendor.TypeId = type.Id;
                                }
                                else
                                {
                                    vendor.Type = null;
                                    vendor.TypeId = null;
                                }


                                #endregion

                                if (isNew)
                                {
                                    ctx.Vendors.Add(vendor);
                                }

                                await ctx.SaveChangesAsync();
                            }
                            catch (Exception e)
                            {
                                response.Errors.Add(JObject.FromObject(new
                                {
                                    ErrorText = e.Message,
                                    Line = jsonVendor["__RowNumber"]
                                }));
                            }
                        }
                    }

                }

            }

            return Ok<JObject>(response);
        }
    }
}