using PayPal.Api;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web;

namespace GlobeTrek.Models
{
    public static class PaypalConfiguration
    {
        public readonly static string ClientId;
        public readonly static string ClientSecret;

        static PaypalConfiguration()
        {
            try
            {
                var config = GetConfig();
                ClientId = config["clientId"];
                ClientSecret = config["clientSecret"];

                if (string.IsNullOrEmpty(ClientId))
                {
                    throw new ConfigurationErrorsException("PayPal ClientId is missing or empty in web.config under <appSettings>.");
                }
                if (string.IsNullOrEmpty(ClientSecret))
                {
                    throw new ConfigurationErrorsException("PayPal ClientSecret is missing or empty in web.config under <appSettings>.");
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi chi tiết
                System.Diagnostics.Debug.WriteLine($"PaypalConfiguration Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw; // Ném lại để xử lý ở lớp gọi
            }
        }

        public static Dictionary<string, string> GetConfig()
        {
            var config = new Dictionary<string, string>
    {
        { "mode", ConfigurationManager.AppSettings["PayPalMode"] ?? "sandbox" },
        { "clientId", ConfigurationManager.AppSettings["PayPalClientId"] },
        { "clientSecret", ConfigurationManager.AppSettings["PayPalClientSecret"] },
        { "connectionTimeout", ConfigurationManager.AppSettings["PayPalConnectionTimeout"] ?? "360000" },
        { "requestRetries", ConfigurationManager.AppSettings["PayPalRequestRetries"] ?? "1" }
    };

            System.Diagnostics.Debug.WriteLine($"PayPalMode: {config["mode"]}");
            System.Diagnostics.Debug.WriteLine($"ClientId: {config["clientId"]}");
            System.Diagnostics.Debug.WriteLine($"ClientSecret: {config["clientSecret"]}");

            return config;
        }

        private static string GetAccessToken()
        {
            string accessToken = new OAuthTokenCredential(ClientId, ClientSecret, GetConfig()).GetAccessToken();
            return accessToken;
        }

        public static APIContext GetAPIContext()
        {
            APIContext apiContext = new APIContext(GetAccessToken());
            apiContext.Config = GetConfig();
            return apiContext;
        }
    }
}