
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace GrblPlotter
{
    public static class Notifier
    {
        // Trace, Debug, Info, Warn, Error, Fatal
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly CultureInfo culture = CultureInfo.InvariantCulture;

        public static void SendMessage(string message)
        { SendMessage(message, ""); }
        public static void SendMessage(string message, string titleAddon)
        {
            bool mail = Properties.Settings.Default.notifierMailEnable;
            bool push = Properties.Settings.Default.notifierPushbulletEnable;
            if (!string.IsNullOrEmpty(message))
            {
                if (mail || push)
                    Logger.Info(culture, "Mail:{0} Push:{1}   Msg:{2}  Addon:{3}   Interval:{4}", mail, push, message.Replace("\r\n", " | "), titleAddon, Properties.Settings.Default.notifierMessageProgressInterval);
                if (mail)
                {
                    Task tm = Task.Factory.StartNew(() =>
                    {
                        SendMail(message, titleAddon);
                    });
                    tm.Wait();
                }

                if (push)
                {
                    Task tp = Task.Factory.StartNew(() =>
                    {
                        PushBullet(message, titleAddon);
                    });
                    tp.Wait();
                }
            }
        }

        public static string SendMail(string message, string titleAddon)
        {   // http://csharp.net-informations.com/communications/csharp-smtp-mail.htm
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient(Properties.Settings.Default.notifierMailClientAdr);

                mail.From = new MailAddress(Properties.Settings.Default.notifierMailSendFrom);
                mail.To.Add(Properties.Settings.Default.notifierMailSendTo);
                mail.Subject = Properties.Settings.Default.notifierMailSendSubject + " " + titleAddon;
                mail.Body = message;

                SmtpServer.EnableSsl = true;
                SmtpServer.Port = (int)Properties.Settings.Default.notifierMailClientPort;
                SmtpServer.Credentials = new System.Net.NetworkCredential(Properties.Settings.Default.notifierMailClientUser, Properties.Settings.Default.notifierMailClientPass);

                SmtpServer.Send(mail);
                SmtpServer.Dispose();
                mail.Dispose();
                return "Email sent";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, " sendMail() ");
                return "Error sending email:\r\n" + ex.ToString();
            }
        }

        public static string PushBullet(string message, string titleAddon = "")
        {
            // Pushbullet functionality disabled in Lite version
            return "PushBullet disabled";
        }
    }
}
