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
        FileStream fs;
        StreamReader sr;
        string fileName;
        public Log()
        {
            InitializeComponent();
            fileName = "Log_out.txt";
            try
            {
                fs = new FileStream(fileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
                sr = new StreamReader(fs);
            }
            catch (IOException e)
            {
                if (e.ToString() != null)
                    MessageBox.Show(e.ToString(), "File IOException", MessageBoxButtons.OK, MessageBoxIcon.Error);                
            }
        }

        public bool Open()
        {            
            try
            {             
                string item;
                while (!sr.EndOfStream)
                {
                    item = sr.ReadLine().ToString(); // read each line in the list
                    listBoxLog.Items.Add(item);
                }                
            }
            catch (IOException e)
            {
                if (e.ToString() != null)
                    MessageBox.Show(e.ToString(), "File IOException", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
        public bool Close_()
        {
            sr.Close();
            fs.Close();
            return true;
        }

        private void Log_Deactivate(object sender, EventArgs e)
        {
            sr.Close();
            fs.Close();
        }
    }
}
