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
    public static class RescueSMSClient
    {

        public static async Task<JToken> ListConnectionsAsync(string phoneNumber = null)
        {
            var baseUrl = ConfigurationManager.AppSettings["RescueSMS.URL"];
            var rsmsUser = ConfigurationManager.AppSettings["RescueSMS.User"];
            var rsmsPassword = ConfigurationManager.AppSettings["RescueSMS.Password"];
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", rsmsUser, rsmsPassword)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            string fullUrl = String.Format("{0}{1}{2}", baseUrl, "connections/", (!String.IsNullOrEmpty(phoneNumber) ? "?identity=" + HttpUtility.UrlEncode(phoneNumber) : ""));

            var response = await client.GetAsync(fullUrl);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public static async Task<JToken> ListMessagesAsync()
        {
            var baseUrl = ConfigurationManager.AppSettings["RescueSMS.URL"];
            var rsmsUser = ConfigurationManager.AppSettings["RescueSMS.User"];
            var rsmsPassword = ConfigurationManager.AppSettings["RescueSMS.Password"];
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", rsmsUser, rsmsPassword)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            string fullUrl = String.Format("{0}{1}", baseUrl, "messages/");

            var response = await client.GetAsync(fullUrl);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public static async Task<JToken> CreateContactAsync(JToken contactInformation)
        {
            var baseUrl = ConfigurationManager.AppSettings["RescueSMS.URL"];
            var rsmsUser = ConfigurationManager.AppSettings["RescueSMS.User"];
            var rsmsPassword = ConfigurationManager.AppSettings["RescueSMS.Password"];
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", rsmsUser, rsmsPassword)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
            var content = new StringContent(contactInformation.ToString(), Encoding.UTF8, "application/json");

            string fullUrl = String.Format("{0}{1}", baseUrl, "contacts/create_contact/");

            var response = await client.PostAsync(fullUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public static async Task<JToken> SendMessageAsync(JToken messageInformation)
        {
            var baseUrl = ConfigurationManager.AppSettings["RescueSMS.URL"];
            var rsmsUser = ConfigurationManager.AppSettings["RescueSMS.User"];
            var rsmsPassword = ConfigurationManager.AppSettings["RescueSMS.Password"];
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", rsmsUser, rsmsPassword)));

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
            var content = new StringContent(messageInformation.ToString(), Encoding.UTF8, "application/json");

            string fullUrl = String.Format("{0}{1}", baseUrl, "messages/send_message/");

            var response = await client.PostAsync(fullUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            return JToken.Parse(responseContent);
        }

        public static async Task<JToken> CreateContactAndSendMessageAsync(string Name, string MobileNumber, string Message, string Groups= "")
        {
            return await CreateContactAndSendMessageAsync(JToken.FromObject(new  { 
                Name,
                MobileNumber,
                Message,
                Groups
            }));
        }

        public static async Task<JToken> CreateContactAndSendMessageAsync(JToken combinedInformation)
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
            if(connections == null || connections.Count == 0)
            {
                await CreateContactAsync(contactInformation);
            }


            return await SendMessageAsync(messageInformation);
        }

    }
}