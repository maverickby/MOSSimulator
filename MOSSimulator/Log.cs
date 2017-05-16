using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace MOSSimulator
{
    public partial class Log : Form
    {
        public Log()
        {
            InitializeComponent();
        }

        public bool Open()
        {
            string fileName = "Log_out.txt";
            try
            {
                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader sr = new StreamReader(fs);
                string item;

                while (!sr.EndOfStream)
                {
                    item = sr.ReadLine().ToString(); // read each line in the list
                    listBoxLog.Items.Add(item);
                }

                sr.Close();
                fs.Close();
            }
            catch (IOException e)
            {
                if (e.ToString() != null)
                    MessageBox.Show(e.ToString(), "File IOException", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
        public bool Close_() { return true; }
    }
}
