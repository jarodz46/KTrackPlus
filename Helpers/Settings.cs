using Java.Util.Prefs;
using KTrackPlus.Helpers.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers
{
    public class Settings
    {
        public int UpdateInterval { get; set; }
        public string Name { get; set; }
        public string Mails { get; set; }
        public bool AskSendMail { get; set; }

        public Settings(bool askSendMail = false)
        {
            UpdateInterval = int.Parse(Xamarin.Essentials.Preferences.Get("updateInterval", "30"));
            Name = Xamarin.Essentials.Preferences.Get("riderName", "Anon");
            Mails = Xamarin.Essentials.Preferences.Get("sendMailTo", "");
            AskSendMail = askSendMail;
        }

        public Settings(byte[] data)
        {
            UpdateInterval = 30;
            Name = string.Empty;
            Mails = string.Empty;
            AskSendMail = false;
            if (data.Length >= 1 + 4 * 4 && data[0] == Manager.bSETTINGS)
            {
                int index = 1;
                var nameLenght = BitConverter.ToInt32(data, index);
                index += 4;                
                if (nameLenght > 0)
                {
                    Name = Encoding.UTF8.GetString(data, index, nameLenght);
                }
                index += nameLenght;
                var mailsLenght = BitConverter.ToInt32(data, index);
                index += 4;
                if (mailsLenght > 0)
                {
                    Mails = Encoding.UTF8.GetString(data, index, mailsLenght);
                }
                index += mailsLenght;
                UpdateInterval = BitConverter.ToInt32(data, index);
                index += 4;
                AskSendMail = BitConverter.ToBoolean(data, index);
                index += 1;
            }
        }

        public byte[] GetBytes()
        {
            var nameBtytes = Encoding.UTF8.GetBytes(Name);
            var nameLenght = BitConverter.GetBytes(nameBtytes.Length);
            var mailsBtytes = Encoding.UTF8.GetBytes(Mails);
            var mailsLenght = BitConverter.GetBytes(mailsBtytes.Length);
            var intevalBytes = BitConverter.GetBytes(UpdateInterval);
            var askSendMailBytes = BitConverter.GetBytes(AskSendMail);
            return new byte[] { Manager.bSETTINGS }.Concat(nameLenght).Concat(nameBtytes)
                .Concat(mailsLenght).Concat(mailsBtytes).Concat(intevalBytes).Concat(askSendMailBytes)
                .ToArray();
        }

        public override string ToString()
        {
            return "Name : " + Name + Environment.NewLine + "Mails : " + Mails + Environment.NewLine + "Update interval : " + UpdateInterval;
        }
    }
}
