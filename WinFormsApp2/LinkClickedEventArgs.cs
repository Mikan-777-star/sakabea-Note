using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFormsApp2
{
    public class LinkClickedEventArgs : EventArgs
    {
        public string Path { get; set; }
        public string Keyword { get; set; }

        public int LineNumber { get; set; }
        }
}
