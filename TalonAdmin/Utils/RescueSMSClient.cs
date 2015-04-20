using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Text;
using System.Threading.Tasks;

namespace TalonAdmin.Utils
{
    public class RescueSMSClient
    {
        public string BaseUrl { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public RescueSMSClient()
        {
            this.BaseUrl = ConfigurationManager.AppSettings["RescueSMS.URL"];
            this.User = ConfigurationManager.AppSettings["RescueSMS.User"];
            this.Password = ConfigurationManager.AppSettings["RescueSMS.Password"];
        }

        public RescueSMSClient(string baseUrl, string user, string password) {
            this.BaseUrl = BaseUrl;
            this.User = user;
            this.Password = password;
        }

        public async Task<JToken> ListConnectionsAsync(string phoneNumber = null)
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", User, Password)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            string fullUrl = String.Format("{0}{1}{2}", BaseUrl, "connections/", (!String.IsNullOrEmpty(phoneNumber) ? "?identity=" + HttpUtility.UrlEncode(phoneNumber) : ""));

            var response = await client.GetAsync(fullUrl);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public async Task<JToken> ListMessagesAsync()
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", User, Password)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            string fullUrl = String.Format("{0}{1}", BaseUrl, "messages/");

            var response = await client.GetAsync(fullUrl);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public async Task<JToken> CreateContactAsync(JToken contactInformation)
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", User, Password)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
            var content = new StringContent(contactInformation.ToString(), Encoding.UTF8, "application/json");

            string fullUrl = String.Format("{0}{1}", BaseUrl, "contacts/create_contact/");

            var response = await client.PostAsync(fullUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public async Task<JToken> SendMessageAsync(JToken messageInformation)
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", User, Password)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
            var content = new StringContent(messageInformation.ToString(), Encoding.UTF8, "application/json");

            string fullUrl = String.Format("{0}{1}", BaseUrl, "messages/send_message/");

            var response = await client.PostAsync(fullUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public async Task<JToken> CreateContactAndSendMessageAsync(string Name, string MobileNumber, string Message, string Groups = "")
        {
            return await CreateContactAndSendMessageAsync(JToken.FromObject(new
            {
                Name,
                MobileNumber,
                Message,
                Groups
            }));
        }

        public async Task<JToken> CreateContactAndSendMessageAsync(JToken combinedInformation)
        {
            dynamic contactInformation = new JObject();
            dynamic messageInformation = new JObject();
            dynamic parameter = combinedInformation;

            string phoneNumber = parameter.MobileNumber;
            if (!phoneNumber.StartsWith("+"))
                phoneNumber = "+" + phoneNumber;

            contactInformation.name = parameter.Name;
            contactInformation.primary_phone = phoneNumber;
            contactInformation.groups = parameter.Groups;

            messageInformation.to = phoneNumber;
            messageInformation.message = parameter.Message;

            var connections = (await ListConnectionsAsync(parameter.MobileNumber.ToString())) as JArray;
            if (connections == null || connections.Count == 0)
            {
                await CreateContactAsync(contactInformation);
            }


            return await SendMessageAsync(messageInformation);
        }

    }
}