using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using TalonAdmin.Extensions;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Controllers
{
    [Authorize]
    [Route("api/Excel")]
    public class ExcelController : ControllerBase
    {
        private readonly Models.Vouchers.Context ctx;
        private readonly Models.Admin.AdminContext adminCtx;

        public ExcelController(Models.Vouchers.Context ctx, Models.Admin.AdminContext adminCtx)
        {
            this.ctx = ctx;
            this.adminCtx = adminCtx;
        }

        /// <summary>
        /// Simple helper method that validates whether the user trying to run an import and an export has actual permossions to do so
        /// </summary>
        private bool ValidateBeneficiaryRequest(int organizationId, int countryId)
        {
            var userId = this.GetUserId();
            if (!String.IsNullOrEmpty(userId))
            {
                // Lazy loading is gone, so the two navigations dereferenced below have to be eager loaded.
                var user = adminCtx.Users
                    .AsNoTracking()
                    .Include(u => u.Organization)
                    .Include(u => u.Countries)
                    .Where(u => u.Id == userId).FirstOrDefault();
                if (user != null)
                {

                    // NOTE: pre-existing bug preserved - Countries is a collection of ApplicationUserCountry join
                    // rows, so "c.Id" is the join row's own key and not the CountryId it should be compared against.
                    return user.Organization.Id == organizationId && user.Countries.Where(c => c.Id == countryId).Any();
                }
            }
            return false;
        }

        /// <summary>
        /// Validates the request to make sure the user trying to import the data has the permissions.
        /// </summary>
        private bool ValidateVendorRequest(int countryId)
        {
            var userId = this.GetUserId();
            if (!String.IsNullOrEmpty(userId))
            {
                // Lazy loading is gone, so Countries has to be eager loaded.
                var user = adminCtx.Users
                    .AsNoTracking()
                    .Include(u => u.Countries)
                    .Where(u => u.Id == userId).FirstOrDefault();
                if (user != null)
                {

                    // NOTE: pre-existing bug preserved - see ValidateBeneficiaryRequest, "c.Id" is the join row key.
                    return user.Countries.Where(c => c.Id == countryId).Any();
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
        // NOTE: the old Web API 2 convention mapped a verb-less action whose name has no known prefix to POST,
        // and the Angular toolbar posts a form at this url, so the verb stays POST.
        // The ids are pinned to [FromQuery] because that form also posts OrganizationId/CountryId fields and
        // ASP.NET Core's form value provider would otherwise win over the query string, unlike Web API 2.
        [HttpPost("ExportBeneficiaries")]
        public async Task<IActionResult> ExportBeneficiaries([FromQuery] int organizationId, [FromQuery] int countryId)
        {
            var groups = await ctx.BeneficiaryGroups
                .AsNoTracking()
                .Where(b => b.OrganizationId == organizationId && b.CountryId == countryId)
                .ToArrayAsync();

            var beneficiaryQuery = await ctx.Beneficiaries
                .AsNoTracking()
                .Include(b => b.AdditionalData)
                .Include(b => b.Location)
                .Where(b => b.OrganizationId == organizationId && b.CountryId == countryId)
                .ToArrayAsync();

            var jsonBeneficiaries = JToken.FromObject(beneficiaryQuery, new JsonSerializer
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            }) as JArray;

            foreach (var jsonBeneficiary in jsonBeneficiaries.OfType<JObject>())
            {
                var groupId = jsonBeneficiary.ValueIfExists<int?>("GroupId");
                if (groupId != null)
                {
                    jsonBeneficiary["Cycle"] = groups.Where(g => g.Id == groupId).First().Name;
                }
                jsonBeneficiary["Location"] = jsonBeneficiary["Location"].Type != JTokenType.Null ? jsonBeneficiary["Location"]["Name"] : "";
                // 1 and 2 are what the interface stores. This read `== "0" ? "Male" : "Female"`,
                // against a value that is never 0, so every household exported as Female.
                jsonBeneficiary["Sex"] = jsonBeneficiary["Sex"].ToString() switch
                {
                    "1" => "Male",
                    "2" => "Female",
                    _ => ""
                };
            }

            jsonBeneficiaries.Flatten("AdditionalDataObject", "Additional Data/");
            jsonBeneficiaries.RemoveProperties("Group", "AdditionalDataObject", "AdditionalData");

            var dataTable = jsonBeneficiaries.ToObject<DataTable>();

            #region Somewhat Necessary Garbage Collection
            jsonBeneficiaries = null;
            System.GC.Collect();
            #endregion

            if (beneficiaryQuery.Count() == 0)
            {
                var empty = JArray.FromObject(new Beneficiary[] { new Beneficiary() });
                empty.RemoveProperties("Group");

                dataTable = empty.ToObject<DataTable>();
                dataTable.Rows.Clear();
            }

            dataTable.TableName = "Beneficiaries";

            // Removing Id Columns because they are parsed later on in the import
            dataTable.Columns.RemoveSafe("AdditionalData");
            dataTable.Columns.RemoveSafe("Name");
            dataTable.Columns.RemoveSafe("GroupId");
            dataTable.Columns.RemoveSafe("LocationId");
            dataTable.Columns.RemoveSafe("OrganizationId");
            dataTable.Columns.RemoveSafe("CountryId");
            dataTable.Columns.RemoveSafe("WasWelcomeMessageSent");
            dataTable.Columns.RemoveSafe("PIN");

            return File(dataTable.ToExcelSpreadsheet(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Beneficiaries.xlsx");
        }

        /// <summary>
        /// Web Api method that takes in an upload of a spreadsheet with a sheet called beneficiaries and extracts beneficiary data from it.
        /// </summary>
        /// <param name="organizationId">Organization Id for the list of beneficiaries</param>
        /// <param name="countryId">Country of beneficiaries</param>
        /// <returns>JSON with any import errors in details</returns>
        [HttpPost("ImportBeneficiaries")]
        public async Task<IActionResult> ImportBeneficiaries([FromQuery] int organizationId, [FromQuery] int countryId)
        {
            if (!IsMimeMultipartContent())
            {
                return BadRequest();
            }
            var currentUser = await this.WhoAmI(adminCtx);

            dynamic response = new JObject();
            response.Errors = new JArray();

            // NOTE: ASP.NET Core buffers the multipart payload itself, so the old MultipartFormDataStreamProvider
            // staging directory under ~/App_Data/uploads (and the delete that cleaned up after it) is no longer needed.
            var form = await Request.ReadFormAsync();

            foreach (IFormFile fileData in form.Files)
            {
                byte[] fileBytes;
                using (var uploadStream = new MemoryStream())
                {
                    await fileData.CopyToAsync(uploadStream);
                    fileBytes = uploadStream.ToArray();
                }

                Models.Admin.Country country = null;

                country = await adminCtx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();

                var beneficiaryQuery = await ctx.Beneficiaries
                    .Where(b => b.CountryId == countryId && b.OrganizationId == organizationId)
                    .ToListAsync();

                var locationQuery = ctx.Locations.Where(l => l.CountryId == countryId);
                var groupQuery = ctx.BeneficiaryGroups.Where(g => g.CountryId == countryId && g.OrganizationId == organizationId);

                var package = new XLWorkbook(new MemoryStream(fileBytes));
                var excelData = package.ExtractData();
                package.Dispose();

                if (excelData.ContainsKey("Beneficiaries"))
                {
                    foreach (var jsonBeneficiary in excelData["Beneficiaries"])
                    {
                        try
                        {
                            var beneficiaryId = jsonBeneficiary.ValueIfExists<int?>("Id");
                            var groupName = jsonBeneficiary.ValueIfExists<string>("Cycle");
                            var locationName = jsonBeneficiary.ValueIfExists<string>("Location");

                            var hasGroupColumn = jsonBeneficiary.HasProperty("Cycle");
                            var hasLocationColumn = jsonBeneficiary.HasProperty("Location");

                            if (jsonBeneficiary.HasProperty("Sex"))
                            {
                                // This mapped anything that was not "male" onto 1, so re-importing
                                // an untouched export turned every woman into a man. Anything
                                // unrecognised now leaves the record's existing value alone rather
                                // than guessing at it.
                                switch ((jsonBeneficiary.ValueIfExists<string>("Sex") ?? "").Trim().ToLower())
                                {
                                    case "male" or "m" or "1":
                                        jsonBeneficiary["Sex"] = 1;
                                        break;

                                    case "female" or "f" or "2":
                                        jsonBeneficiary["Sex"] = 2;
                                        break;

                                    default:
                                        jsonBeneficiary.Remove("Sex");
                                        break;
                                }
                            }

                            if (jsonBeneficiary.HasProperty("Disabled"))
                            {
                                var isDisabled = (jsonBeneficiary.ValueIfExists<string>("Disabled") ?? "false").ToString().Trim().ToLower();
                                jsonBeneficiary["Disabled"] = isDisabled == "true" || isDisabled == "1";
                            }

                            jsonBeneficiary.RemoveProperties("Name", "Cycle", "Location", "OrganizationId", "CountryId");

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

                            if (jsonBeneficiary.HasProperty("MobileNumber"))
                            {

                                var numberRegex = new System.Text.RegularExpressions.Regex(String.Format("^(\\+{0}|{0}|0|1)", country.CountryCallingCode));
                                jsonBeneficiary["MobileNumber"] = String.Format("+{0}{1}", country.CountryCallingCode, numberRegex.Replace(jsonBeneficiary["MobileNumber"].ToString(), ""));
                            }


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
                            else if (hasGroupColumn)
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
                            else if (hasGroupColumn)
                            {
                                beneficiary.Location = null;
                                beneficiary.LocationId = null;
                            }

                            #endregion

                            #region Additional Data
                            var currentData = new BeneficiaryAdditionalData[0];

                            if (!isNew)
                                currentData = await ctx.BeneficiaryAdditionalData.Where(b => b.ParentId == beneficiary.Id).ToArrayAsync();

                            var additionalData = jsonBeneficiary.Properties()
                                .Where(p => p.Name.StartsWith("Additional Data/"))
                                .Select(p => new BeneficiaryAdditionalData
                                {
                                    Id = 0,
                                    ParentId = beneficiary.Id,
                                    Key = p.Name.Replace("Additional Data/", ""),
                                    Value = p.Value.ToString()
                                })
                                .Where(a => !String.IsNullOrEmpty(a.Value))
                                .ToList();

                            additionalData.ForEach(a =>
                            {
                                var q = currentData.Where(o => o.Key == a.Key);
                                if (q.Any())
                                {
                                    var other = q.First();
                                    a.Id = other.Id;
                                    other.Value = a.Value;
                                }
                            });

                            ctx.BeneficiaryAdditionalData.RemoveRange(currentData.Where(a => String.IsNullOrEmpty(a.Value)).ToArray());
                            ctx.BeneficiaryAdditionalData.AddRange(additionalData.Where(a => a.Id == 0));

                            #endregion

                            if (isNew)
                            {
                                ctx.Beneficiaries.Add(beneficiary);
                            }

                            int changeCount = await ctx.SaveChangesAsync();
                            try
                            {
                                if (changeCount > 0)
                                {
                                    ctx.AuditLogItems.Add(new AuditLogItem
                                    {
                                        ModifiedBy = currentUser.UserName,
                                        ModifiedOn = DateTime.UtcNow,
                                        ObjectId = beneficiary.Id,
                                        ObjectType = "Beneficiary"
                                    });

                                    await ctx.SaveChangesAsync();
                                }
                            }
                            catch
                            {
                                // Ignore audit exceptions
                            }
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


            return Ok((JObject)response);
        }

        /// <summary>
        /// Web Api method that exports all vendors in the database for this  country
        /// </summary>
        /// <param name="countryId">Country of vendors</param>
        /// <returns>Attachment with Excel Spreadsheet</returns>
        // NOTE: verb-less Web API 2 action posted to by the Angular toolbar form - see ExportBeneficiaries.
        [HttpPost("ExportVendors")]
        public async Task<IActionResult> ExportVendors([FromQuery] int countryId)
        {
            var vendorQuery = await ctx.Vendors
                .AsNoTracking()
                .Include(v => v.Location)
                .Include(v => v.Type)
                .Where(b => b.CountryId == countryId)
                .ToArrayAsync();
            var jsonString = JsonConvert.SerializeObject(vendorQuery, Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            var jsonCollection = JToken.Parse(jsonString) as JArray;

            foreach (var jsonObject in jsonCollection)
            {
                jsonObject["Location"] = jsonObject["Location"].Type != JTokenType.Null ? jsonObject["Location"]["Name"] : "";
                jsonObject["Type"] = jsonObject["Type"].Type != JTokenType.Null ? jsonObject["Type"]["Name"] : "";
            }

            jsonCollection.Flatten("AdditionalDataObject", "Additional Data/");
            jsonCollection.RemoveProperties("AdditionalDataObject", "AdditionalData");
            jsonCollection.RemoveProperties("LocationId", "CountryId", "TypeId", "ParentRecord");

            var dataTable = jsonCollection.ToObject<DataTable>();

            if (vendorQuery.Count() == 0)
            {
                dataTable = JToken.FromObject(new Vendor[] { new Vendor() }).ToObject<DataTable>();
                dataTable.Rows.Clear();
            }

            dataTable.TableName = "Vendors";

            return File(dataTable.ToExcelSpreadsheet(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Vendors.xlsx");
        }

        /// <summary>
        /// Web Api methods that allow uses to bulk import and update vendors in the system
        /// </summary>
        /// <param name="countryId">Country of vendors</param>
        /// <returns>JSON with any import errors in details</returns>
        [HttpPost("ImportVendors")]
        public async Task<IActionResult> ImportVendors([FromQuery] int countryId)
        {
            if (!IsMimeMultipartContent())
            {
                return BadRequest();
            }
            var currentUser = await this.WhoAmI(adminCtx);

            dynamic response = new JObject();
            response.Errors = new JArray();

            // NOTE: see ImportBeneficiaries - the ~/App_Data/uploads staging directory is gone with
            // MultipartFormDataStreamProvider; ASP.NET Core hands us the uploaded parts directly.
            var form = await Request.ReadFormAsync();

            foreach (IFormFile fileData in form.Files)
            {
                byte[] fileBytes;
                using (var uploadStream = new MemoryStream())
                {
                    await fileData.CopyToAsync(uploadStream);
                    fileBytes = uploadStream.ToArray();
                }

                Models.Admin.Country country = null;

                country = await adminCtx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();

                var vendorQuery = await ctx.Vendors.Where(v => v.CountryId == countryId).ToListAsync();
                var locationQuery = ctx.Locations.Where(l => l.CountryId == countryId);
                var vendorTypeQuery = ctx.VendorTypes.Where(l => l.CountryId == countryId);

                var package = new XLWorkbook(new MemoryStream(fileBytes));
                var excelData = package.ExtractData();
                package.Dispose();

                if (excelData.ContainsKey("Vendors"))
                {
                    foreach (var jsonVendor in excelData["Vendors"])
                    {
                        try
                        {
                            var vendorId = jsonVendor.ValueIfExists<int?>("Id");
                            var locationName = jsonVendor.ValueIfExists<string>("Location");
                            var typeName = jsonVendor.ValueIfExists<string>("Type");

                            var hasTypeColumn = jsonVendor.HasProperty("Type");
                            var hasLocationColumn = jsonVendor.HasProperty("Location");

                            jsonVendor.RemoveProperties("Location", "Type", "CountryId");

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

                            if (jsonVendor.HasProperty("MobileNumber"))
                            {
                                var numberRegex = new System.Text.RegularExpressions.Regex(String.Format("^(\\+{0}|{0}|0|1)", country.CountryCallingCode));
                                jsonVendor["MobileNumber"] = String.Format("+{0}{1}", country.CountryCallingCode, numberRegex.Replace(jsonVendor["MobileNumber"].ToString(), ""));
                            }

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
                            else if (hasLocationColumn)
                            {
                                vendor.Location = null;
                                vendor.LocationId = null;
                            }


                            #endregion

                            #region Assigning Type
                            VendorType type = null;

                            // If location is filled out try to find or create new one
                            if (!String.IsNullOrEmpty(typeName))
                            {
                                // NOTE: pre-existing bug preserved - the lookup matches on locationName rather than
                                // typeName (and throws if the Location column was blank while Type was filled in).
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
                            else if (hasTypeColumn)
                            {
                                vendor.Type = null;
                                vendor.TypeId = null;
                            }


                            #endregion


                            #region Additional Data
                            var currentData = new VendorAdditionalData[0];

                            if (!isNew)
                                currentData = await ctx.VendorAdditionalData.Where(b => b.ParentId == vendor.Id).ToArrayAsync();

                            var additionalData = jsonVendor.Properties()
                                .Where(p => p.Name.StartsWith("Additional Data/"))
                                .Select(p => new VendorAdditionalData
                                {
                                    Id = 0,
                                    ParentId = vendor.Id,
                                    Key = p.Name.Replace("Additional Data/", ""),
                                    Value = p.Value.ToString()
                                })
                                .Where(a => !String.IsNullOrEmpty(a.Value))
                                .ToList();

                            additionalData.ForEach(a =>
                            {
                                var q = currentData.Where(o => o.Key == a.Key);
                                if (q.Any())
                                {
                                    var other = q.First();
                                    a.Id = other.Id;
                                    other.Value = a.Value;
                                }
                            });

                            ctx.VendorAdditionalData.RemoveRange(currentData.Where(a => String.IsNullOrEmpty(a.Value)).ToArray());
                            ctx.VendorAdditionalData.AddRange(additionalData.Where(a => a.Id == 0));

                            #endregion

                            if (isNew)
                            {
                                ctx.Vendors.Add(vendor);
                            }

                            int changeCount = await ctx.SaveChangesAsync();
                            try
                            {
                                if (changeCount > 0)
                                {
                                    ctx.AuditLogItems.Add(new AuditLogItem
                                    {
                                        ModifiedBy = currentUser.UserName,
                                        ModifiedOn = DateTime.UtcNow,
                                        ObjectId = vendor.Id,
                                        ObjectType = "Vendor"
                                    });

                                    await ctx.SaveChangesAsync();
                                }
                            }
                            catch
                            {
                                // Ignore audit exceptions
                            }
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

            return Ok((JObject)response);
        }

        [HttpPost("ImportMetadata")]
        public async Task<IActionResult> ImportMetadata()
        {
            if (!IsMimeMultipartContent())
            {
                return BadRequest();
            }

            dynamic response = new JObject();
            response.Errors = new JArray();

            // NOTE: see ImportBeneficiaries - the ~/App_Data/uploads staging directory is gone with
            // MultipartFormDataStreamProvider; ASP.NET Core hands us the uploaded parts directly.
            var form = await Request.ReadFormAsync();

            foreach (IFormFile fileData in form.Files)
            {
            }

            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("ExportMetadata")]
        public async Task<IActionResult> ExportMetadata()
        {
            var dataTable = new DataTable();
            var jsonSerializer = new JsonSerializer() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

            // AsNoTracking keeps EF Core from doing relationship fixup between these result sets, which is what
            // the old ProxyCreationEnabled/LazyLoadingEnabled = false pair effectively guaranteed.
            var actions = JArray.FromObject(await adminCtx.Actions.AsNoTracking().ToArrayAsync(), jsonSerializer).ToObject<DataTable>();
            var menuCategories = JArray.FromObject(await adminCtx.MenuCategories.AsNoTracking().ToArrayAsync(), jsonSerializer).ToObject<DataTable>();

            var menuItems = JArray.FromObject(await adminCtx.MenuItems.AsNoTracking().ToArrayAsync(), jsonSerializer)
                .RemoveProperties("Category", "Children")
                .ToObject<DataTable>();

            var roles = JArray.FromObject(await adminCtx.Roles.AsNoTracking().ToArrayAsync(), jsonSerializer)
                .RemoveProperties("Users")
                .ToObject<DataTable>();

            var actionRoles = JArray.FromObject(await adminCtx.ActionRoles.AsNoTracking().ToArrayAsync(), jsonSerializer)
                .RemoveProperties("Action", "Role")
                .ToObject<DataTable>();

            var menuCategoryRoles = JArray.FromObject(await adminCtx.MenuCategoryRoles.AsNoTracking().ToArrayAsync(), jsonSerializer)
                .RemoveProperties("Category", "Role")
                .ToObject<DataTable>();

            actions.TableName = "Actions";
            menuCategories.TableName = "MenuCategories";
            menuItems.TableName = "MenuItems";
            roles.TableName = "Roles";
            actionRoles.TableName = "ActionRoles";
            menuCategoryRoles.TableName = "MenuCategoryRoles";

            var dataSet = new DataSet();

            dataSet.Tables.Add(actions);
            dataSet.Tables.Add(menuCategories);
            dataSet.Tables.Add(menuItems);
            dataSet.Tables.Add(roles);
            dataSet.Tables.Add(actionRoles);
            dataSet.Tables.Add(menuCategoryRoles);

            return File(dataSet.ToExcelSpreadsheet(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Metadata.xlsx");
        }

        /// <summary>
        /// Replacement for the Web API 2 <c>Request.Content.IsMimeMultipartContent()</c> check.
        /// </summary>
        private bool IsMimeMultipartContent()
        {
            var contentType = Request.ContentType;

            return !String.IsNullOrEmpty(contentType)
                && contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
