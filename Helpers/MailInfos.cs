using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers
{
    [MessagePackObject]
    public class MailInfos
    {
        [Key(0)]
        public string mail { get; set; }
        [Key(1)]
        public string name { get; set; }
        [Key(2)]
        public string locale { get; set; } = "en";

        public MailInfos(string mail, string name, string locale)
        {
            this.mail = mail;
            this.name = name;
            this.locale = locale;
        }


    }
}
