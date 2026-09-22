using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace CMIMAPSicherung
{
    public class ImapAutoDetectResult
    {
        public bool Success { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Security { get; set; }
        public string Provider { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }
    }

    public static class ImapAutoDetector
    {
        private class ProviderMap
        {
            public string[] Domains;
            public string Host;
            public int Port;
            public string Provider;
            public string Security;
        }

        private static readonly ProviderMap[] KnownProviders = new ProviderMap[]
        {
            new ProviderMap { Domains = new [] { "gmail.com", "googlemail.com" }, Host = "imap.gmail.com", Port = 993, Provider = "Google", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "outlook.com", "hotmail.com", "live.de", "live.com", "msn.com", "office365.com", "microsoft.com" }, Host = "outlook.office365.com", Port = 993, Provider = "Microsoft 365 / Outlook", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "yahoo.com", "yahoo.de", "ymail.com" }, Host = "imap.mail.yahoo.com", Port = 993, Provider = "Yahoo", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "icloud.com", "me.com", "mac.com" }, Host = "imap.mail.me.com", Port = 993, Provider = "Apple iCloud", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "gmx.de", "gmx.net", "gmx.at" }, Host = "imap.gmx.net", Port = 993, Provider = "GMX", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "web.de" }, Host = "imap.web.de", Port = 993, Provider = "WEB.DE", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "t-online.de" }, Host = "secureimap.t-online.de", Port = 993, Provider = "Telekom", Security = "SSL/TLS" },
            new ProviderMap { Domains = new [] { "ionos.de", "ionos.com", "1und1.de", "1and1.com", "1and1.de" }, Host = "imap.ionos.de", Port = 993, Provider = "IONOS", Security = "SSL/TLS" },
        };

        public static ImapAutoDetectResult Detect(string email)
        {
            if (String.IsNullOrWhiteSpace(email) || email.IndexOf('@') < 1)
                return new ImapAutoDetectResult { Success = false, Message = "Bitte zuerst eine vollständige E-Mail-Adresse eingeben." };

            email = email.Trim();
            string domain = email.Substring(email.LastIndexOf('@') + 1).Trim().ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(domain))
                return new ImapAutoDetectResult { Success = false, Message = "Die Domain der E-Mail-Adresse konnte nicht ermittelt werden." };

            ProviderMap known = KnownProviders.FirstOrDefault(p => p.Domains.Any(d => String.Equals(d, domain, StringComparison.OrdinalIgnoreCase)));
            if (known != null)
            {
                return new ImapAutoDetectResult
                {
                    Success = true,
                    Host = known.Host,
                    Port = known.Port,
                    Provider = known.Provider,
                    Security = known.Security,
                    UserName = email,
                    Message = "Bekannter Mail-Anbieter erkannt."
                };
            }

            ImapAutoDetectResult autoConfig = TryAutoconfig(email, domain);
            if (autoConfig != null && autoConfig.Success) return autoConfig;

            foreach (var guess in BuildHostCandidates(domain))
            {
                if (CanConnect(guess.Host, guess.Port, 2200))
                {
                    return new ImapAutoDetectResult
                    {
                        Success = true,
                        Host = guess.Host,
                        Port = guess.Port,
                        Provider = "Automatisch erkannt",
                        Security = guess.Port == 993 ? "SSL/TLS" : "STARTTLS",
                        UserName = email,
                        Message = "Typischer IMAP-Server wurde über die Domain geprüft."
                    };
                }
            }

            return new ImapAutoDetectResult
            {
                Success = false,
                UserName = email,
                Message = "Es konnte kein IMAP-Server automatisch bestätigt werden. Bitte Server und Port manuell prüfen. Versucht wurden bekannte Anbieter, Autoconfig und typische Hostnamen wie imap." + domain + "."
            };
        }

        private static ImapAutoDetectResult TryAutoconfig(string email, string domain)
        {
            string[] urls = new[]
            {
                "https://autoconfig." + domain + "/mail/config-v1.1.xml?emailaddress=" + Uri.EscapeDataString(email),
                "https://" + domain + "/.well-known/autoconfig/mail/config-v1.1.xml?emailaddress=" + Uri.EscapeDataString(email)
            };

            foreach (string url in urls)
            {
                try
                {
                    string xml = DownloadString(url);
                    if (String.IsNullOrWhiteSpace(xml)) continue;
                    var result = ParseAutoconfig(xml, email);
                    if (result != null && result.Success) return result;
                }
                catch { }
            }
            return null;
        }

        private static string DownloadString(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 2500;
            request.ReadWriteTimeout = 2500;
            request.UserAgent = "CM-IMAP-Sicherung/1.4.0.1";
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static ImapAutoDetectResult ParseAutoconfig(string xml, string email)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList servers = doc.GetElementsByTagName("incomingServer");
            foreach (XmlNode server in servers)
            {
                var typeAttr = server.Attributes == null ? null : server.Attributes["type"];
                if (typeAttr == null || !String.Equals(typeAttr.Value, "imap", StringComparison.OrdinalIgnoreCase)) continue;

                string host = GetChildText(server, "hostname");
                string portText = GetChildText(server, "port");
                string socketType = GetChildText(server, "socketType");
                string user = GetChildText(server, "username");
                int port;
                if (!Int32.TryParse(portText, out port)) port = 993;
                if (String.IsNullOrWhiteSpace(host)) continue;
                return new ImapAutoDetectResult
                {
                    Success = true,
                    Host = host.Trim(),
                    Port = port,
                    Provider = "Autoconfig",
                    Security = NormalizeSecurity(socketType, port),
                    UserName = NormalizeUsername(user, email),
                    Message = "IMAP-Einstellungen über Autoconfig gefunden."
                };
            }
            return null;
        }

        private static IEnumerable<ImapHostGuess> BuildHostCandidates(string domain)
        {
            string[] hostNames = new[] { "imap." + domain, "mail." + domain, domain };
            foreach (string host in hostNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return new ImapHostGuess { Host = host, Port = 993 };
                yield return new ImapHostGuess { Host = host, Port = 143 };
            }
        }

        private static string GetChildText(XmlNode parent, string childName)
        {
            foreach (XmlNode child in parent.ChildNodes)
                if (String.Equals(child.Name, childName, StringComparison.OrdinalIgnoreCase)) return child.InnerText;
            return "";
        }

        private static string NormalizeUsername(string value, string email)
        {
            if (String.IsNullOrWhiteSpace(value)) return email;
            value = value.Trim();
            if (value.Contains("%EMAILADDRESS%")) return value.Replace("%EMAILADDRESS%", email);
            if (value.Contains("%EMAILLOCALPART%")) return value.Replace("%EMAILLOCALPART%", email.Split('@')[0]);
            return value;
        }

        private static string NormalizeSecurity(string socketType, int port)
        {
            string s = (socketType ?? "").Trim().ToUpperInvariant();
            if (s == "SSL" || s == "SSL/TLS") return "SSL/TLS";
            if (s == "STARTTLS") return "STARTTLS";
            return port == 993 ? "SSL/TLS" : "STARTTLS";
        }

        private static bool CanConnect(string host, int port, int timeoutMs)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(host, port, null, null);
                    using (ar.AsyncWaitHandle)
                    {
                        if (!ar.AsyncWaitHandle.WaitOne(timeoutMs, false))
                        {
                            try { client.Close(); } catch { }
                            return false;
                        }
                        client.EndConnect(ar);
                        return client.Connected;
                    }
                }
            }
            catch { return false; }
        }

        private class ImapHostGuess
        {
            public string Host;
            public int Port;
        }
    }
}
