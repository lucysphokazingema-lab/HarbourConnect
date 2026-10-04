using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Script.Serialization;

namespace APDP.Helpers
{
    /// <summary>
    /// Sends notification emails in the background so a slow mail server never delays a page.
    ///
    /// App settings (Azure App Settings / AppSecrets.config):
    ///   EmailMode      Graph | Smtp | (empty = emails off)
    ///   EmailFrom      e.g. noreply@yourcompany.co.za  (the Microsoft 365 mailbox)
    ///   EmailFromName  display name, default "HarbourConnect"
    ///   SiteBaseUrl    e.g. https://harbourconnect.azurewebsites.net (for links in emails)
    ///
    ///   Graph (recommended for Microsoft 365):  GraphTenantId, GraphClientId, GraphClientSecret
    ///     — an Entra ID app registration with the Mail.Send application permission,
    ///       limited to the EmailFrom mailbox with an Exchange application access policy.
    ///   Smtp:  SmtpHost (smtp.office365.com), SmtpPort (587), SmtpUser, SmtpPassword
    /// </summary>
    public static class EmailSender
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly object TokenLock = new object();
        private static string   graphToken;
        private static DateTime graphTokenExpires;

        private static string Setting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static bool IsEnabled
        {
            get
            {
                string mode = Setting("EmailMode");
                return !string.IsNullOrWhiteSpace(mode) && !string.IsNullOrWhiteSpace(Setting("EmailFrom"));
            }
        }

        /// <summary>Queues an email; never throws. <paramref name="link"/> may be app-relative ("/Customer/MyBookings").</summary>
        public static void Queue(string toAddress, string toName, string subject, string message, string link)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(toAddress)) return;

            string html = BuildBody(toName, message, link);
            Func<Task> send = () => SendAsync(toAddress, subject, html);

            if (HostingEnvironment.IsHosted)
                HostingEnvironment.QueueBackgroundWorkItem(ct => send());
            else
                Task.Run(send);
        }

        private static string BuildBody(string toName, string message, string link)
        {
            string baseUrl = (Setting("SiteBaseUrl") ?? "").TrimEnd('/');
            string url = string.IsNullOrEmpty(link) ? baseUrl
                       : link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link
                       : baseUrl + link;

            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#212529;max-width:560px\">");
            sb.Append("<div style=\"background:#0d2c54;color:#fff;padding:16px 20px;font-size:18px;font-weight:600\">&#9875; HarbourConnect</div>");
            sb.Append("<div style=\"padding:20px\">");
            sb.Append("<p>Hello ").Append(HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(toName) ? "there" : toName)).Append(",</p>");
            sb.Append("<p>").Append(HttpUtility.HtmlEncode(message)).Append("</p>");
            if (!string.IsNullOrEmpty(url))
            {
                sb.Append("<p><a href=\"").Append(HttpUtility.HtmlAttributeEncode(url))
                  .Append("\" style=\"display:inline-block;background:#1565c0;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none\">Open HarbourConnect</a></p>");
            }
            sb.Append("<p style=\"color:#6c757d;font-size:12px\">You received this because you have a HarbourConnect account. Please do not reply to this email.</p>");
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static async Task SendAsync(string to, string subject, string html)
        {
            try
            {
                string mode = (Setting("EmailMode") ?? "").Trim();
                if (mode.Equals("Graph", StringComparison.OrdinalIgnoreCase))
                    await SendViaGraphAsync(to, subject, html);
                else if (mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
                    await SendViaSmtpAsync(to, subject, html);
            }
            catch (Exception ex)
            {
                // Email is best-effort: the in-app notification has already been saved.
                Trace.TraceError("HarbourConnect email to {0} failed: {1}", to, ex.Message);
            }
        }

        // ── SMTP (e.g. smtp.office365.com:587 with STARTTLS) ──────────────
        private static async Task SendViaSmtpAsync(string to, string subject, string html)
        {
            int port;
            if (!int.TryParse(Setting("SmtpPort"), out port)) port = 587;

            using (var mail = new MailMessage())
            using (var smtp = new SmtpClient(Setting("SmtpHost"), port))
            {
                mail.From = new MailAddress(Setting("EmailFrom"), Setting("EmailFromName") ?? "HarbourConnect");
                mail.To.Add(to);
                mail.Subject    = subject;
                mail.Body       = html;
                mail.IsBodyHtml = true;

                smtp.EnableSsl      = true;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.Credentials    = new NetworkCredential(Setting("SmtpUser"), Setting("SmtpPassword"));
                await smtp.SendMailAsync(mail);
            }
        }

        // ── Microsoft Graph (app-only, Mail.Send) ─────────────────────────
        private static async Task SendViaGraphAsync(string to, string subject, string html)
        {
            string token = await GetGraphTokenAsync();
            string from  = Setting("EmailFrom");

            var payload = new
            {
                message = new
                {
                    subject = subject,
                    body    = new { contentType = "HTML", content = html },
                    toRecipients = new[] { new { emailAddress = new { address = to } } }
                },
                saveToSentItems = false
            };

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://graph.microsoft.com/v1.0/users/" + Uri.EscapeDataString(from) + "/sendMail");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(new JavaScriptSerializer().Serialize(payload), Encoding.UTF8, "application/json");

            using (var response = await Http.SendAsync(request))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Graph sendMail returned " + (int)response.StatusCode + ": " +
                                                        await response.Content.ReadAsStringAsync());
            }
        }

        private static async Task<string> GetGraphTokenAsync()
        {
            lock (TokenLock)
            {
                if (graphToken != null && DateTime.UtcNow < graphTokenExpires) return graphToken;
            }

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id",     Setting("GraphClientId") },
                { "client_secret", Setting("GraphClientSecret") },
                { "scope",         "https://graph.microsoft.com/.default" },
                { "grant_type",    "client_credentials" }
            });

            string url = "https://login.microsoftonline.com/" + Uri.EscapeDataString(Setting("GraphTenantId") ?? "") + "/oauth2/v2.0/token";
            using (var response = await Http.PostAsync(url, form))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Graph token request failed (" + (int)response.StatusCode + ").");

                var json = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(body);
                int expiresIn = Convert.ToInt32(json["expires_in"]);
                lock (TokenLock)
                {
                    graphToken        = (string)json["access_token"];
                    graphTokenExpires = DateTime.UtcNow.AddSeconds(expiresIn - 300);
                    return graphToken;
                }
            }
        }
    }
}
