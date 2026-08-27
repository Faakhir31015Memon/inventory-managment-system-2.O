using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SIOMS.Helpers
{
    public static class EmailHelper
    {
        public static async Task SendOtpEmailAsync(IConfiguration config, string toEmail, string siteName, string otp)
        {
            var host = config["Smtp:Host"];
            var port = int.Parse(config["Smtp:Port"] ?? "587");
            var username = config["Smtp:Username"];
            var password = config["Smtp:Password"];
            var fromEmail = config["Smtp:FromEmail"] ?? username;
            var fromName = config["Smtp:FromName"] ?? siteName;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = $"{siteName} - Password Reset OTP",
                Body = $"Your OTP for resetting the {siteName} admin password is: {otp}\n\n" +
                       $"This code expires in 10 minutes. If you did not request this, please ignore this email.",
                IsBodyHtml = false
            };
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}