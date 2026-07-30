using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TalonAdmin.Utils
{
    /// <summary>
    /// REST client for the RescueSMS gateway. Credentials used to come from
    /// ConfigurationManager.AppSettings; they are now supplied by the caller, which already
    /// resolves per-country settings from the database.
    /// </summary>
    public class RescueSMSClient
    {
        private static readonly HttpClient SharedClient = new HttpClient();

        public string BaseUrl { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public RescueSMSClient(string baseUrl, string user, string password)
        {
            // The original constructor assigned BaseUrl to itself, so a caller-supplied base URL
            // was silently dropped; that bug is fixed here.
            this.BaseUrl = baseUrl;
            this.User = user;
            this.Password = password;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, HttpContent content = null)
        {
            var auth = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(string.Format("{0}:{1}", User, Password)));

            var request = new HttpRequestMessage(method, BaseUrl + relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            request.Content = content;

            return request;
        }

        private static async Task<JToken> ReadAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(body) ? JValue.CreateNull() : JToken.Parse(body);
        }

        public async Task<JToken> ListConnectionsAsync(string phoneNumber = null)
        {
            var url = "connections/";
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                url += "?identity=" + Uri.EscapeDataString(phoneNumber);
            }

            using var response = await SharedClient.SendAsync(CreateRequest(HttpMethod.Get, url));
            return await ReadAsync(response);
        }

        public async Task<JToken> ListMessagesAsync()
        {
            using var response = await SharedClient.SendAsync(CreateRequest(HttpMethod.Get, "messages/"));
            return await ReadAsync(response);
        }

        public async Task<JToken> CreateContactAsync(JToken contactInformation)
        {
            var content = new StringContent(contactInformation.ToString(), Encoding.UTF8, "application/json");
            using var response = await SharedClient.SendAsync(
                CreateRequest(HttpMethod.Post, "contacts/create_contact/", content));

            return await ReadAsync(response);
        }

        public async Task<JToken> SendMessageAsync(JToken messageInformation)
        {
            var content = new StringContent(messageInformation.ToString(), Encoding.UTF8, "application/json");
            using var response = await SharedClient.SendAsync(
                CreateRequest(HttpMethod.Post, "messages/send_message/", content));

            return await ReadAsync(response);
        }

        public Task<JToken> CreateContactAndSendMessageAsync(string name, string mobileNumber, string message, string groups = "")
        {
            return CreateContactAndSendMessageAsync(JToken.FromObject(new
            {
                Name = name,
                MobileNumber = mobileNumber,
                Message = message,
                Groups = groups
            }));
        }

        public async Task<JToken> CreateContactAndSendMessageAsync(JToken combinedInformation)
        {
            var mobileNumber = combinedInformation.Value<string>("MobileNumber") ?? string.Empty;
            var phoneNumber = mobileNumber.StartsWith("+") ? mobileNumber : "+" + mobileNumber;

            var connections = await ListConnectionsAsync(mobileNumber) as JArray;
            if (connections == null || connections.Count == 0)
            {
                var contactInformation = new JObject
                {
                    ["name"] = combinedInformation.Value<string>("Name"),
                    ["primary_phone"] = phoneNumber,
                    ["groups"] = combinedInformation.Value<string>("Groups")
                };

                await CreateContactAsync(contactInformation);
            }

            var messageInformation = new JObject
            {
                ["to"] = phoneNumber,
                ["message"] = combinedInformation.Value<string>("Message")
            };

            return await SendMessageAsync(messageInformation);
        }
    }
}
