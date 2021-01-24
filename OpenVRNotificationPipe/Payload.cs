using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe
{
    class Payload
    {
        public bool custom = false;
        public string title = "";
        public string message = "";
        public string image = "";
        public bool isEmpty() {
            return title.Equals("") || message.Equals("") || image.Equals("");
        }
    }
}
