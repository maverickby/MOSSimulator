using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MOSSimulator
{
    /// <summary>
    /// Interaction logic for JoystickWindow.xaml
    /// </summary>
    public partial class JoystickWindow : Window
    {
        MainWindow mainWindow;
        public JoystickWindow()
        {
            InitializeComponent();
            Init();
        }

        public JoystickWindow(MainWindow mW)
        {
            mainWindow = mW;
            InitializeComponent();
            Init();
        }


        private void Init()
        {
            string strGuid="";
            if (mainWindow.MyJoystick!=null)
                strGuid = "Joystick initialized, GUID: " + mainWindow.MyJoystick.JoystikGuid;
            textBoxJoystickstate.Text = strGuid;
            numJoystickK.Value = mainWindow.NumJoystickK;
            numTresholdHorizont.Value = mainWindow.JoystickZoneInsensibilityX;
            numTresholdVertical.Value = mainWindow.JoystickZoneInsensibilityY;
        }

        private void numJoystickKValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            mainWindow.ControlChanged(sender, e);
        }

        private void JoystickWindow1_Loaded(object sender, RoutedEventArgs e)
        {
            //if (msgAppeared != null) msgAppeared("Joystick initialized, GUID: " + joystikGuid);
        }

        private void buttonJoystickSettingsApply_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.JoystickZoneInsensibilityX= (int)numTresholdHorizont.Value;
            mainWindow.JoystickZoneInsensibilityY= (int)numTresholdVertical.Value;
            mainWindow.ControlChanged(sender, e);
        }
    }
}
