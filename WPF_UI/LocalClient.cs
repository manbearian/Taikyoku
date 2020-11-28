using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace WPF_UI
{
    class LocalClient
    {
        static readonly HttpClient client = new HttpClient();
        static string connectionString = "https://shogibackend20201126101522.azurewebsites.net/api/StartGameSession?name=ian";

        public static void Connect()
        {
            // Update port # in the following line.
#if PRODUCTION
            client.BaseAddress = new Uri("https://shogibackend20201126101522.azurewebsites.net/");
#else
            client.BaseAddress = new Uri("http://localhost:7071/");
#endif
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            StartGameSession().ContinueWith(res => OnGameStarted(res.Result));
        }

        static async Task<string> StartGameSession()
        {
            try
            {
                //HttpResponseMessage response = await client.GetAsync("api/StartGameSession");
                HttpResponseMessage response = await client.GetAsync("api/GameOrchestrator_HttpStart"); 

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (HttpRequestException)
            {
                // report/log failure
            }

            return "ERROR";
        }

        static void OnGameStarted(string text)
        {
            System.Windows.MessageBox.Show(text);
        }
    }
}

