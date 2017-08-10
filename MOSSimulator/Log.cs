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
using System.Text.RegularExpressions;

namespace MOSSimulator
{
    public partial class Log : Form
    {
        FileStream fs;
        StreamReader sr;
        StreamWriter sw;
        string fileName;
        MainWindow mainWindow;
        public Log(MainWindow mainWin)
        {
            InitializeComponent();
            mainWindow = mainWin;
            //fileName = "Log_out.txt";
            if (mainWindow.LogFileSave)
            {
                string dt_compatible = DateTime.Now.ToString();
                var re = new Regex(":");
                dt_compatible = re.Replace(dt_compatible, ".");
                fileName = "Log_" + dt_compatible + ".txt";
                try
                {
                    fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    sr = new StreamReader(fs);
                    sw = new StreamWriter(fs);
                }
                catch (IOException e)
                {
                    //if (e.ToString() != null)
                    //MessageBox.Show(e.ToString(), "File IOException", MessageBoxButtons.OK, MessageBoxIcon.Error);                
                }
            }
        }

        public bool Open()
        {
            if (mainWindow.LogFileSave)
            {
                string dt_compatible = DateTime.Now.ToString();
                var re = new Regex(":");
                dt_compatible = re.Replace(dt_compatible, ".");
                fileName = "Log_" + dt_compatible + ".txt";
                try
                {
                    fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    sr = new StreamReader(fs);
                    sw = new StreamWriter(fs);
                }
                catch (IOException e)
                {
                    //if (e.ToString() != null)
                    //MessageBox.Show(e.ToString(), "File IOException", MessageBoxButtons.OK, MessageBoxIcon.Error);                
                }
            }
            /*try
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
            }*/
            return true;
        }
        public bool Close_()
        {
            if(sr!=null)
                sr.Close();
            sr = null;
            if (fs != null)
                fs.Close();
            fs = null;

            return true;
        }

        private void Log_Deactivate(object sender, EventArgs e)
        {
            //sr.Close();
            //fs.Close();
        }

        public bool WriteLine(byte[] buff, MainWindow mainWindow_, int direction)
        {
            string str_line_hex = "";
            str_line_hex = BitConverter.ToString(buff);

            string dt_compatible = DateTime.Now.ToString();
            var re = new Regex(":");
            dt_compatible = re.Replace(dt_compatible, ".");
            if(direction==0)
                str_line_hex = "-->" + str_line_hex + "   " + dt_compatible;
            if (direction == 1)
                str_line_hex = "<--" + str_line_hex + "   " + dt_compatible;

            sw.WriteLine(str_line_hex);
            mainWindow_.Dispatcher.BeginInvoke(new Action(delegate
            {
                listBoxLog.Items.Add(str_line_hex);
            }));
            
            return true;
        }
    }
}
