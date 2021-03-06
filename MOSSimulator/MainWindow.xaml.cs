﻿using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Multimedia;
using System.Net;
using SharpDX.DirectInput;
using System.Text.RegularExpressions;
using System.Collections;
using System.Threading;
using System.Diagnostics;

namespace MOSSimulator
{
    /// <summary>
    /// делегат для инициализации посылки команды
    /// <summary>
    public delegate void delInitSendCommand();
    public delegate void delUpdateMUGSPErrorsWindow();
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    enum GearMode { DONOTCHANGE_MODE, OFF, STAB_AIM, NONSTAB_AIM, MOVE_SET_ANGLE_AUTO_STAB_AIM, MOVE_SET_ANGLE_SEEK_ANGLE, RESERVE }; //режимы работы приводов   
    enum Device { MU_GSP, TVK1,TVK2,TPVK,LD};


    public partial class MainWindow : Window
    {
        public Log frmLog;
        public StatusWindow ldStatusWindow;
        public StatusWindow tvk1StatusWindow;
        public StatusWindow tvk2StatusWindow;
        public StatusWindow tpvkStatusWindow;
        public Uart uart;
        int cntStartErrors, cntTimeOuts, cntChkSums, numOfPocket, cntSuccess, cntOrderErrors;
        private Multimedia.Timer tmrExchange;//таймер для ПЕРЕДАЧИ
        private Multimedia.Timer tmrMUGSPErrors;//таймер для обновления ошибок МУ ГСП
        MyJoystick myJoystick;
        public MOSSimulator.MyJoystick MyJoystick
        {
            get { return myJoystick; }
            set { myJoystick = value; }
        }
        Thread uartThread;
        Cmd com;
        CmdTVK1 comTVK1;
        CmdTVK2 comTVK2;
        CmdTPVK comTPVK;
        CmdLD comLD;
        StructureCommand st_out, st_in;
        StructureCommandLD st_outLD, st_inLD;
        StructureCommandTVK2 st_outTVK2;
        StructureCommandTVK1 st_outTVK1;
        StructureCommandTPVK st_outTPVK;

        Device currentDevice;
        bool tagUartReceived;//признак события приема по интерфейсу uart
        GearMode gearMode;
        JoystickWindow joystickWindow;
        int cycleIndex;
        int[] arrayCycleDeviceOrder = new int[8];
        bool[] activeDevices = new bool[5];
        int numJoystickK;//коэффициент джойстика
        public int NumJoystickK
        {
            get { return numJoystickK; }
            set { numJoystickK = value; }
        }
        private System.ComponentModel.IContainer components = null;

        int[] GearModeValues = new int[] { 0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x80 };
        int[] LDCommand = new int[] { 0x00, 0x3b, 0xc4, 0xa5, 0x89 };

        string[] GearModeOutStr = new string[] {"Не менять режим", "выключен", "стабилизированное наведение", "нестабилизированное наведение",
        "переход в заданное угловое положение с автоматическим переходом в режим стабилизированного наведения при достижении заданного угла",
        "переход в заданное угловое положение с последующим слежением за заданным углом", "резерв" };

        string[] GearModeInStr = new string[] {"авария (привод выключен)", "выключен", "стабилизированное наведение", "нестабилизированное наведение",
        "переход в заданное угловое положение с автоматическим переходом в режим стабилизированного наведения при достижении заданного угла",
        "переход в заданное угловое положение с последующим слежением за заданным углом", "инициализация" };

        const int GSP_PACKET_SIZE = 18;
        const int GSP_DATA_SIZE_IN = 12;
        const int GSP_DATA_SIZE_OUT = 10;

        const double koeff_lsb_speed = 0.2197265625 / 3600;

        const int LD_DATA_SIZE_OUT = 4;
        const int LD_DATA_SIZE_IN = 8;
        const int LD_PACKET_SIZE_OUT = 12;
        const int TVK2_DATA_SIZE_OUT = 22;
        const int TVK2_DATA_SIZE_IN = 22;
        const int TVK2_PACKET_SIZE_OUT = 30;
        const int TVK1_DATA_SIZE_OUT = 18;
        const int TVK1_DATA_SIZE_IN = 18;
        const int TVK1_PACKET_SIZE_OUT = 26;
        const int TPVK_DATA_SIZE_OUT = 11;
        const int TPVK_DATA_SIZE_IN = 18;
        const int TPVK_PACKET_SIZE_OUT = 19;

        double AZSpeed, ELSpeed;//хранение значений скоростей наведения при управлении джойстиком, значение в диапазоне 0..1
        int AZValue;//значение азимута МУ ГСП
        int ELValue;//значение тангажа МУ ГСП
        int joystickZoneInsensibilityX, joystickZoneInsensibilityY;
        bool camZoomTeleVariableSend;
        bool camZoomWideVariableSend;
        bool camZoomStopSend;
        bool camZoomDirectSend;
        //bool camZoomTeleSend;
        //bool camZoomWideSend;
        bool camFocusFarVariableSend;
        bool camFocusNearVariableSend;
        //bool camFocusFarSend;
        //bool camFocusNearSend;
        bool camFocusStopSend;
        bool camFocusDirectSend;
        bool camAutoFocusSend;

        //необходимость послать Inquiry Command для камеры TVK1
        bool CAM_ZoomPosInq;
        bool CAM_FocusPosInq;
        bool CAM_FocusModeInq;

        int CinFocusPosInq;
        int CinZoomPosInq;

        byte numTVK1ZoomTeleVariablePVal;
        int numTVK1ZoomVal;
        byte numTVK1FocusFarNearVariablePVal;
        int numTVK1FocusVal;
        bool checkBoxTVK1CAM_FocusAutoVal;

        int TVK1FocusPosition;
        int TVK1ZoomPosition;
        string TVK1AutoFocus;        

        bool LDCommandSend;

        bool[] JoystickKeyboardsStates;

        bool MUGSPSpeedNavFixed;//признак блокировки наведения по скорости при управлении джойстиком

        bool[] activeDevicesArr;
        bool[] arrayActiveDevicesInCycle;

        byte Cin, Cout;
        bool TVK1DataChanged;
        bool MUGSPInfExchangeONOFF;
        bool LDInfExchangeONOFF;
        bool TVK2InfExchangeONOFF;
        bool TVK1InfExchangeONOFF;
        bool TPVKInfExchangeONOFF;
        object locker;
        object locker2;
        bool tagUartReceivedInterm;
        ItemCollection collection;
        List<ComboBoxItem> LDcbItemsList;
        bool logFileSave;
        public bool LogFileSave
        {
            get { return logFileSave; }
            set { logFileSave = value; }
        }

        int CountMUGSPErrDatchUglAz;
        int CountMUGSPErrDatchUglTang;
        int CountMUGSPErrAzNeVDopuske;
        int CountMUGSPErrTangNeVDopuske;
        string str_err;

        int timeTPVKZoomChange;//переменная для анализа задержки 250 мс для зума ТПВК

        int TPVKZoomPrev;//предыдущее значение зума
        int TPVKZoom3Count;//счетчик пакетов при посылке 3х пакета зума
        bool TPVKZoomPacketsSeries;//признак пакета 3х зума
        int TPVKZoomMeditVal;//промежуточное значение при посылке 3х пакета зума

        int TPVKFocusPrev;//предыдущее значение фокуса
        int TPVKFocus3Count;//счетчик пакетов при посылке 3х пакета фокуса
        bool TPVKFocusPacketsSeries;//признак пакета 3х фокуса
        int TPVKFocusMeditVal;//промежуточное значение при посылке 3х пакета фокуса

        bool TPVKCalibrationEngaged;//признак калибровки
        int TPVKCalibration3Count;//счетчик пакетов при посылке 3х пакета калибровки
        bool TPVKCalibrationPacketsSeries;//признак пакета 3х калибровки
        byte TPVKCalibrationMeditVal;//промежуточное значение при посылке 3х пакета калибровки
        int TPVKZoom_IN;
        int TPVKFocus_IN;
        int TPVKExposure_IN;

        int TVK1ZoomPrev;//предыдущее значение зума
        int TVK1Zoom3Count;//счетчик пакетов при посылке 3х пакета зума
        bool TVK1ZoomPacketsSeries;//признак пакета 3х зума
        int TVK1ZoomMeditVal;//промежуточное значение при посылке 3х пакета зума

        int TVK1FocusPrev;//предыдущее значение фокуса
        int TVK1Focus3Count;//счетчик пакетов при посылке 3х пакета фокуса
        bool TVK1FocusPacketsSeries;//признак пакета 3х фокуса
        int TVK1FocusMeditVal;//промежуточное значение при посылке 3х пакета фокуса

        public int JoystickZoneInsensibilityY
        {
            get { return joystickZoneInsensibilityY; }
            set { joystickZoneInsensibilityY = value; }
        }
        public int JoystickZoneInsensibilityX
        {
            get { return joystickZoneInsensibilityX; }
            set { joystickZoneInsensibilityX = value; }
        }
        protected void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            //base.Dispose(disposing);
        }

        public MainWindow()
        {
            InitializeComponent();
            Init();
        }
        void Init()
        {
            components = new System.ComponentModel.Container();
            cntChkSums = cntStartErrors = cntTimeOuts = numOfPocket = cntSuccess = cntOrderErrors = 0;
            Uart.FillComPorts(cbComPorts);
            com = new Cmd();
            comTVK1 = new CmdTVK1();
            comTVK2 = new CmdTVK2();
            comTPVK = new CmdTPVK();
            comLD = new CmdLD();
            frmLog = new Log(this);
            //другое поведение WPF кантролов, не надо создавать тут экземпляры окон, в отличие от WinForms
            /*ldStatusWindow = new StatusWindow();
            tvk1StatusWindow = new StatusWindow();
            tvk2StatusWindow = new StatusWindow();
            tpvkStatusWindow = new StatusWindow();*/

            tmrExchange = new Multimedia.Timer(this.components);
            tmrExchange.Mode = Multimedia.TimerMode.OneShot;
            tmrExchange.Period = 1;
            tmrExchange.Resolution = 0;
            tmrExchange.SynchronizingObject = null;
            tmrExchange.Tick += new System.EventHandler(this.tmrExchange_Tick);

            tmrMUGSPErrors = new Multimedia.Timer(this.components);
            tmrMUGSPErrors.Mode = Multimedia.TimerMode.Periodic;
            tmrMUGSPErrors.Period = 5000;
            tmrMUGSPErrors.Resolution = 0;
            tmrMUGSPErrors.SynchronizingObject = null;
            tmrMUGSPErrors.Tick += new System.EventHandler(this.tmrMUGSPErrors_Tick);
            MUGSPSpeedNavFixed = false;
            AZValue = 0;
            ELValue = 0;

            st_out = new StructureCommand();
            //st_in = new StructureCommand();            
            st_outLD = new StructureCommandLD();
            //st_inLD = new StructureCommandLD();
            st_outTVK2 = new StructureCommandTVK2();
            st_outTVK1 = new StructureCommandTVK1();
            st_outTPVK = new StructureCommandTPVK();

            sliderAZAngle.IsEnabled = false;
            sliderELAngle.IsEnabled = false;
            numAZAngle.IsEnabled = false;
            numELAngle.IsEnabled = false;
            buttonResetGSPAngleControls.IsEnabled = false;

            sliderAZSpeed.IsEnabled = false;
            sliderELSpeed.IsEnabled = false;
            numAZSpeed.IsEnabled = false;
            numELSpeed.IsEnabled = false;

            sliderAZSpeedCompensation.IsEnabled = false;
            sliderELSpeedCompensation.IsEnabled = false;
            numAZSpeedCompensation.IsEnabled = false;
            numELSpeedCompensation.IsEnabled = false;

            buttonResetGSPSpeedControls.IsEnabled = false;
            checkBoxJoystickUse.IsEnabled = false;
            radioButtonGSPSpeedControl1X.IsEnabled = false;
            radioButtonGSPSpeedControl2X.IsEnabled = false;
            radioButtonGSPSpeedControl4X.IsEnabled = false;

            sliderTVK1Focus.IsEnabled = false;
            numTVK1Focus.IsEnabled = false;
            buttonFocusLeft.IsEnabled = false;
            buttonFocusRight.IsEnabled = false;

            sliderTVK2Exposure.IsEnabled = false;
            numTVK2Exposure.IsEnabled = false;

            currentDevice = Device.MU_GSP;
            cycleIndex = 0;

            arrayCycleDeviceOrder[0] = 0;//ГСП
            arrayCycleDeviceOrder[1] = 1;//ТВК1
            arrayCycleDeviceOrder[2] = 0;
            arrayCycleDeviceOrder[3] = 2;//ТВК2
            arrayCycleDeviceOrder[4] = 0;
            arrayCycleDeviceOrder[5] = 3;//ТПВК
            arrayCycleDeviceOrder[6] = 0;
            arrayCycleDeviceOrder[7] = 4;//ЛД

            tagUartReceived = true;//для самого первого цикла в циклограмме
            locker = new object();
            locker2 = new object();
            //joystickWindow = new JoystickWindow(this);
            gearMode = GearMode.OFF;
            NumJoystickK = 1;
            JoystickZoneInsensibilityX = JoystickZoneInsensibilityY = 2000;
            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;
            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;
            Cin = Cout = 0;
            TVK1DataChanged = false;

            CAM_ZoomPosInq =false;
            CAM_FocusPosInq = false;
            CAM_FocusModeInq = false;

            LDCommandSend = false;
            JoystickKeyboardsStates= new bool[5];
            activeDevicesArr = new bool[5];
            for (int i = 0; i < 5; i++)
                activeDevicesArr[i] = false;

            arrayActiveDevicesInCycle = new bool[8];
            for (int i = 0; i < 8; i++)
                arrayActiveDevicesInCycle[i] = false;
            TVK1AutoFocus = "Режим фокуса: автофокус\n";
            collection = comboBoxLDCommands.Items;
            LogFileSave=false;

            CountMUGSPErrDatchUglAz=0;
            CountMUGSPErrDatchUglTang = 0;
            CountMUGSPErrAzNeVDopuske = 0;
            CountMUGSPErrTangNeVDopuske = 0;
            str_err = "";

            timeTPVKZoomChange = Environment.TickCount;
            TPVKZoomPrev = 0;
            TPVKZoom3Count = 0;
            TPVKZoomPacketsSeries = false;
            TPVKZoomMeditVal = 0;

            TPVKCalibrationEngaged=false;
            TPVKCalibration3Count=0;
            TPVKCalibrationPacketsSeries=false;

            sliderTPVKGain.IsEnabled = false;
            numTPVKGain.IsEnabled = false;
            sliderTPVKLevel.IsEnabled = false;
            numTPVKLevel.IsEnabled = false;
            comboBoxTPVKExpositionMode.IsEnabled = false;
            sliderTPVKEnhancementEdge.IsEnabled = false;
            numTPVKEnhancementEdge.IsEnabled = false;
            sliderTPVKEnhancementContrastModes.IsEnabled = false;
            numTPVKEnhancementContrastModes.IsEnabled = false;
        }
        private void Log_Click(object sender, RoutedEventArgs e)
        {
            if(frmLog.IsDisposed)
                frmLog = new Log(this);
            frmLog.Text = "Лог";
            frmLog.Show();
            frmLog.Open();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void formMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (uart != null)
            {
                //tmrExchange.Stop();
                uart.Close();
            }
            //if (sets != null)
            //    SaveSettings();
            MyJoystick.Close();
            DoEvents(); //Application.DoEvents();
        }

        void TPVKZoomUp()
        {
            sliderTPVKZoom.Value++;
            ControlChanged(null, null);
        }

        void TPVKZoomDown()
        {
            sliderTPVKZoom.Value--;
            ControlChanged(null, null);
        }

        void myJoystickStateReceived(MyJoystickState state)
        {
            //if (joystickWindow == null)
               // return;        
            Dispatcher.BeginInvoke(new Action(delegate            
            {
                if (joystickWindow != null)
                {
                    joystickWindow.textBoxJoystickXVal.Text = state.x.ToString();
                    joystickWindow.textBoxJoystickYVal.Text = state.y.ToString();
                }

                if (state.buttons[0])//курок
                {                    
                    JoystickKeyboardsStates[4] = true;
                    //MUGSPSpeedNavFixed = !MUGSPSpeedNavFixed;//признак блокировки наведения по скорости при управлении джойстиком
                    //checkBoxMUGSPSpeedNavFixed.IsChecked = MUGSPSpeedNavFixed;
                }
                else
                {
                    if (JoystickKeyboardsStates[4] == true && (bool)checkBoxJoystickUse.IsChecked)
                    {
                        MUGSPSpeedNavFixed = !MUGSPSpeedNavFixed;//признак блокировки наведения по скорости при управлении джойстиком
                        checkBoxMUGSPSpeedNavFixed.IsChecked = MUGSPSpeedNavFixed;
                        JoystickKeyboardsStates[4] = false;
                    }
                }

                if (state.buttons[1])
                {                    
                    buttonZoomRight_PreviewMouseLeftButtonDown(this, null);
                    JoystickKeyboardsStates[0]=true;                    
                }
                else
                {
                    if (JoystickKeyboardsStates[0] == true)
                    {
                        buttonZoomRight_PreviewMouseLeftButtonUp(this, null);
                        JoystickKeyboardsStates[0] = false;
                    }
                }

                if (state.buttons[3])
                {
                    buttonZoomLeft_PreviewMouseLeftButtonDown(this, null);
                    JoystickKeyboardsStates[2] = true;
                }
                else
                {                    
                    if (JoystickKeyboardsStates[2] == true)
                    {
                        buttonZoomLeft_PreviewMouseLeftButtonUp(this, null);
                        JoystickKeyboardsStates[2] = false;
                    }
                }

                if (state.buttons[2])
                {
                    //buttonFocusRight_PreviewMouseLeftButtonDown(this, null);
                    if (Environment.TickCount > timeTPVKZoomChange + 250)//задержка 250 мс для зума ТПВК
                    {
                        TPVKZoomUp();
                        timeTPVKZoomChange = Environment.TickCount;
                    }
                    JoystickKeyboardsStates[1] = true;
                }
                else
                {
                    if (JoystickKeyboardsStates[1] == true)
                    {
                        //buttonFocusRight_PreviewMouseLeftButtonUp(this, null);
                        JoystickKeyboardsStates[1] = false;
                    }
                }

                if (state.buttons[4])
                {
                    //buttonFocusLeft_PreviewMouseLeftButtonDown(this, null);
                    if (Environment.TickCount > timeTPVKZoomChange + 250)//задержка 250 мс для зума ТПВК
                    {
                        TPVKZoomDown();
                        timeTPVKZoomChange = Environment.TickCount;
                    }                    
                    JoystickKeyboardsStates[3] = true;
                }
                else
                {
                    if (JoystickKeyboardsStates[3] == true)
                    {
                        //buttonFocusLeft_PreviewMouseLeftButtonUp(this, null);
                        JoystickKeyboardsStates[3] = false;
                    }
                }


                if ((bool)checkBoxJoystickUse.IsChecked && !MUGSPSpeedNavFixed)
                {                    
                    //значения сохраняются, т.к. state изменяется в другом потоке (джойстика)
                    int state_x = state.x - 32768;//коррекция значения положения ручки джойстика X для приведения к диапазону -32768..32767
                    int state_y = state.y - 32768;//коррекция значения положения ручки джойстика Y для приведения к диапазону -32768..32767
                    //32768 - длина шкалы значения джойстика при отклонении от центрального положения до упора

                    //коррекция по зоне нечувствительности джойстика
                    if (Math.Abs(state_x) < JoystickZoneInsensibilityX)
                        state_x = 0;
                    if (Math.Abs(state_y) < JoystickZoneInsensibilityY)
                        state_y = 0;
                    /*if (state_x > JoystickZoneInsensibilityX)
                        state_x = state_x - JoystickZoneInsensibilityX;
                    if (state_x < -JoystickZoneInsensibilityX)
                        state_x = state_x + JoystickZoneInsensibilityX;

                    if (state_y > JoystickZoneInsensibilityY)
                        state_y = state_y - JoystickZoneInsensibilityY;
                    if (state_y < -JoystickZoneInsensibilityY)
                        state_y = state_y + JoystickZoneInsensibilityY;*/
                    //

                    double valX = (1 - Math.Cos((state.x / 32768.0) * (3.1415926 / 2)));
                    double valY = (1 - Math.Cos((state.y / 32768.0) * (3.1415926 / 2)));

                    if (state_x > 0)//анализ положения ручки джойстика (справа или слева от центрального положения)
                        AZSpeed = ( 1 - Math.Cos((state_x/ 32768.0) * (3.1415926/2)) );//значение просчитано в диапазоне 0..1
                    else
                        AZSpeed = -(1 - Math.Cos((state_x / 32768.0) * (3.1415926 / 2)));//значение просчитано в диапазоне -1..0

                    if (state_y > 0)//анализ положения ручки джойстика (сверху или снизу от центрального положения)
                        ELSpeed = -(1 - Math.Cos((state_y / 32768.0) * (3.1415926 / 2)));//значение просчитано в диапазоне 0..1
                    else
                        ELSpeed = (1 - Math.Cos((state_y / 32768.0) * (3.1415926 / 2)));//значение просчитано в диапазоне -1..0

                    //умножается на коэффициент для корректного отображения градусов в секунду на кантролах,
                    if (NumJoystickK == 1)
                    {
                        sliderAZSpeed.Value = AZSpeed * 5.0;
                        sliderELSpeed.Value = ELSpeed * 5.0;
                    }
                    if (NumJoystickK == 2)
                    {
                        sliderAZSpeed.Value = AZSpeed * 20.0;
                        sliderELSpeed.Value = ELSpeed * 20.0;
                    }
                    if (NumJoystickK == 4)
                    {
                        sliderAZSpeed.Value = AZSpeed * 90.0;
                        sliderELSpeed.Value = ELSpeed * 90.0;
                    }

                    ControlChanged(null, null);
                }
            }));
        }

        void msgAppeared(string str)
        {
            AddToList(str);
        }
        public void AddToList(string outstr)
        {
            if (joystickWindow==null)
                return;
            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate                    
                    {
                        joystickWindow.textBoxJoystickstate.Text = outstr;
                    }));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "55"); }
            }
            else
            {
                joystickWindow.textBoxJoystickstate.Text = outstr;

            }
        }
        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
                uart = UartConnect();
        }

        Uart UartConnect()
        {
            if (uart == null)
                uart = new Uart(this);

            if (uart.isOpen() == false)//если порт закрыт, старт
            {
                try
                {
                    uart.PortName = cbComPorts.Text;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + "\n\nПроверьте настройки порта", ex.Source, MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                try
                {
                    if (uart.Open())
                    {
                        cntStartErrors = 0; cntTimeOuts = 0; cntChkSums = 0; numOfPocket = 0; cntSuccess = 0;
                        cntOrderErrors = 0;
                        uart.received += uart_received;//подписка на событие приема received из класса UART
                        uart.showBufferWasSent += uart_bufferSent;
                        cbComPorts.IsEnabled = false;
                        buttonStart.Content = "Стоп";
                        buttonStart.Background = Brushes.LightGreen;

                        //основной режим, циклическая посылка пакетов
                        // создаем новый поток
                        uartThread = new Thread(new ThreadStart(InitSendCommand));
                        uartThread.Start(); // запускаем поток
                        tmrMUGSPErrors.Start();
                        //uartThread.Join();                        
                    }
                    else
                    {
                        MessageBox.Show("Ошибка открытия порта!", "MOSSimulator");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    frmLog.WriteLine(Encoding.Default.GetBytes("UartConnect() uart.isOpen() == false Exception error" + ex.ToString()), this, 0);
                    return null;
                }
            }
            else//порт открыт, остановка
            {
                try
                {
                    tmrMUGSPErrors.Stop();                    
                    //остановить рабочий поток посылки сообщений ДО закрытия последовательного порта uart !
                    if (uartThread != null)
                    {
                        uartThread.Abort();
                        uartThread.Join();
                    }
                    uart.DesetFalse();
                    cycleIndex = 0;
                    setTagUartReceived(true);
                    if (uart != null)
                        uart.Close();
                    uart = null;

                    ///
                    //MyJoystick.Close();
                    if (ldStatusWindow != null)
                        ldStatusWindow.Close();
                    if (tvk1StatusWindow != null)
                        tvk1StatusWindow.Close();
                    if (tvk2StatusWindow != null)
                        tvk2StatusWindow.Close();
                    if (tpvkStatusWindow != null)
                        tpvkStatusWindow.Close();
                    //DoEvents(); //Application.DoEvents();
                    ///

                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                    Debug.WriteLine("UartConnect() Exception error", ex.ToString());
                    
                    String str_exc = "UartConnect() Exception error"+ ex.ToString();
                    frmLog.WriteLine(Encoding.Default.GetBytes("UartConnect() uart.isOpen() == true Exception error" + ex.ToString()), this, 0);
                    uart = null;
                    return null;
                }


                lblSuccess.Content = String.Format("Усп. пакетов: {0}", cntSuccess.ToString());
                lblStatusUART.Content = String.Format("Статус обмена: {0}", "");
                lblStartErrors.Content = String.Format("Ошибки старт. байта: {0}", cntStartErrors.ToString());
                lblErrorChkSums.Content = String.Format("Ошибки контр. суммы: {0}", cntChkSums.ToString());
                lblTimeOuts.Content = String.Format("Превыш. времени ожидания: {0}", cntTimeOuts.ToString());
                lblNumPacks.Content = "Послано пакетов: " + numOfPocket.ToString();
                lblOrderErrors.Content = String.Format("Ошибки циклограммы: {0}", cntOrderErrors.ToString());                    

                buttonStart.Content = "Старт";
                buttonStart.Background = Brushes.LightGray;
                cbComPorts.IsEnabled = true;
                //uart = null;
            }

            return uart;
        }

        //пампинг событий
        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }

        //чтение данных из порта
        void uart_received(Cmd com_in)
        {
                //Dispatcher.BeginInvoke(new Action(delegate
                //{       //BeginInvoke т.к. прием по возбуждению события received в Uart
                switch (com_in.result)
                {
                    case CmdResult.TIMEOUT:
                        if (lblStatusUART.Foreground == Brushes.Red)
                            lblStatusUART.Foreground = Brushes.Black;
                        else
                            lblStatusUART.Foreground = Brushes.Red;
                        cntTimeOuts++;
                        break;
                    case CmdResult.BAD_CHKSUM1:
                        cntChkSums++;
                        break;
                    case CmdResult.BAD_CHKSUM2:
                        cntChkSums++;
                        break;
                    case CmdResult.BAD_START_BYTE:
                        cntStartErrors++;
                        break;
                    case CmdResult.SUCCESS:
                        {
                            cntSuccess++;
                        }
                        break;
                }

            Dispatcher.BeginInvoke(new Action(delegate
            {
                lblSuccess.Content = String.Format("Усп. пакетов: {0}", cntSuccess.ToString());
                lblStatusUART.Content = String.Format("Статус обмена: {0}", com_in.result.ToString());
                lblStartErrors.Content = String.Format("Ошибки старт. байта: {0}", cntStartErrors.ToString());
                lblErrorChkSums.Content = String.Format("Ошибки контр. суммы: {0}", cntChkSums.ToString());
                lblTimeOuts.Content = String.Format("Превыш. времени ожидания: {0}", cntTimeOuts.ToString());
                lblOrderErrors.Content = String.Format("Ошибки циклограммы: {0}", cntOrderErrors.ToString());
            }));

            //проверка на BAD_CHKSUM
            if (com_in.result == CmdResult.BAD_CHKSUM1 || com_in.result == CmdResult.BAD_CHKSUM2)
                return;
                //проверка на отсутствие блока данных                
                if (BitConverter.ToUInt16(com_in.LENGTH, 0) == 0)
                    return;

                //МУ ГСП
                //режим работы привода азимута
                if (MUGSPInfExchangeONOFF && (cycleIndex == 0 || cycleIndex == 2 || cycleIndex == 4 || cycleIndex == 6))
                {
                    if (com_in.LENGTH[0] * 2 != GSP_DATA_SIZE_IN)
                    {
                        cntOrderErrors++;
                        return;
                    }
                int index = com_in.DATA[0];                    

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    //tbErrors.Text = "";
                    switch ((int)com_in.DATA[0])
                    {
                        case 0x00:
                            tbModeAZ.Text = GearModeInStr[0];
                            break;
                        case 0x01:
                            tbModeAZ.Text = GearModeInStr[1];
                            break;
                        case 0x02:
                            tbModeAZ.Text = GearModeInStr[2];
                            break;
                        case 0x04:
                            tbModeAZ.Text = GearModeInStr[3];
                            break;
                        case 0x08:
                            tbModeAZ.Text = GearModeInStr[4];
                            break;
                        case 0x10:
                            tbModeAZ.Text = GearModeInStr[5];
                            break;
                        case 0x80:
                            tbModeAZ.Text = GearModeInStr[6];
                            break;
                    }

                    //режим работы привода тангажа
                    switch ((int)com_in.DATA[1])
                    {
                        case 0x00:
                            tbModeEL.Text = GearModeInStr[0];
                            break;
                        case 0x01:
                            tbModeEL.Text = GearModeInStr[1];
                            break;
                        case 0x02:
                            tbModeEL.Text = GearModeInStr[2];
                            break;
                        case 0x04:
                            tbModeEL.Text = GearModeInStr[3];
                            break;
                        case 0x08:
                            tbModeEL.Text = GearModeInStr[4];
                            break;
                        case 0x10:
                            tbModeEL.Text = GearModeInStr[5];
                            break;
                        case 0x80:
                            tbModeEL.Text = GearModeInStr[6];
                            break;
                    }

                    //входной сигнал привода азимута
                    byte[] byteArr = new byte[4];
                    byteArr[0] = com_in.DATA[2];
                    byteArr[1] = com_in.DATA[3];
                    byteArr[2] = com_in.DATA[4];
                    byteArr[3] = 0x00;

                    int val;

                    if ((byteArr[2] & 1 << 7) != 0)//отрицательное число
                    {
                        byteArr[2] ^= 1 << 7;//установить бит 7 в 0

                        byteArr[2] ^= 1 << 0;
                        byteArr[2] ^= 1 << 1;
                        byteArr[2] ^= 1 << 2;
                        byteArr[2] ^= 1 << 3;
                        byteArr[2] ^= 1 << 4;
                        byteArr[2] ^= 1 << 5;
                        byteArr[2] ^= 1 << 6;

                        byteArr[0] ^= 1 << 0;
                        byteArr[0] ^= 1 << 1;
                        byteArr[0] ^= 1 << 2;
                        byteArr[0] ^= 1 << 3;
                        byteArr[0] ^= 1 << 4;
                        byteArr[0] ^= 1 << 5;
                        byteArr[0] ^= 1 << 6;
                        byteArr[0] ^= 1 << 7;

                        byteArr[1] ^= 1 << 0;
                        byteArr[1] ^= 1 << 1;
                        byteArr[1] ^= 1 << 2;
                        byteArr[1] ^= 1 << 3;
                        byteArr[1] ^= 1 << 4;
                        byteArr[1] ^= 1 << 5;
                        byteArr[1] ^= 1 << 6;
                        byteArr[1] ^= 1 << 7;

                        val = -(BitConverter.ToInt32(byteArr, 0) + 1);
                        val = (int)(val * koeff_lsb_speed / 2.833333333);
                        tbAZAngle2.Text = val.ToString();
                    }
                    else
                    {
                        val = BitConverter.ToInt32(byteArr, 0);
                        val = (int)(val * koeff_lsb_speed/2.833333333);
                        tbAZAngle2.Text = val.ToString();
                    }

                    /*..................*/
                    //входной сигнал привода тангажа

                    byteArr[0] = com_in.DATA[5];
                    byteArr[1] = com_in.DATA[6];
                    byteArr[2] = com_in.DATA[7];
                    byteArr[3] = 0x00;

                    if ((byteArr[2] & 1 << 7) != 0)//отрицательное число
                    {
                        byteArr[2] ^= 1 << 7;//установить бит 7 в 0

                        byteArr[2] ^= 1 << 0;
                        byteArr[2] ^= 1 << 1;
                        byteArr[2] ^= 1 << 2;
                        byteArr[2] ^= 1 << 3;
                        byteArr[2] ^= 1 << 4;
                        byteArr[2] ^= 1 << 5;
                        byteArr[2] ^= 1 << 6;

                        byteArr[0] ^= 1 << 0;
                        byteArr[0] ^= 1 << 1;
                        byteArr[0] ^= 1 << 2;
                        byteArr[0] ^= 1 << 3;
                        byteArr[0] ^= 1 << 4;
                        byteArr[0] ^= 1 << 5;
                        byteArr[0] ^= 1 << 6;
                        byteArr[0] ^= 1 << 7;

                        byteArr[1] ^= 1 << 0;
                        byteArr[1] ^= 1 << 1;
                        byteArr[1] ^= 1 << 2;
                        byteArr[1] ^= 1 << 3;
                        byteArr[1] ^= 1 << 4;
                        byteArr[1] ^= 1 << 5;
                        byteArr[1] ^= 1 << 6;
                        byteArr[1] ^= 1 << 7;

                        val = -(BitConverter.ToInt32(byteArr, 0) + 1);
                        val = (int)(val * koeff_lsb_speed / 2.833333333);
                        tbELAngle2.Text = val.ToString();
                    }
                    else
                    {
                        val = BitConverter.ToInt32(byteArr, 0);
                        val = (int)(val * koeff_lsb_speed / 2.833333333);
                        tbELAngle2.Text = val.ToString();
                    }


                    //errors
                    byte[] byteArrErr = new byte[2];
                    byteArrErr[0] = com_in.DATA[8];
                    byteArrErr[1] = com_in.DATA[9];

                    //tbErrors.Text = BitConverter.ToUInt16(byteArrErr, 0).ToString();   
                    str_err = "";

                    byte errByte = com_in.DATA[8];
                    if ((errByte & 1 << 0) != 0)
                    {
                        str_err += "Ошибка датчика гироскопического азимута\r\n";
                    }

                    if ((errByte & 1 << 1) != 0)
                    {
                        str_err += "Ошибка датчика гироскопического тангажа\r\n";
                    }

                    if ((errByte & 1 << 2) != 0)
                    {
                        CountMUGSPErrDatchUglAz++;
                        str_err += "Ошибка датчика угла азимута, пакетов:" + CountMUGSPErrDatchUglAz.ToString() + "\r\n";
                    }
                    if ((errByte & 1 << 3) != 0)
                    {
                        CountMUGSPErrDatchUglTang++;
                        str_err += "Ошибка датчика угла тангажа, пакетов:" + CountMUGSPErrDatchUglTang.ToString() + "\r\n";
                    }
                    if ((errByte & 1 << 4) != 0)
                    {
                        str_err += "Ошибка АЦП датчиков тока\r\n";
                    }
                    if ((errByte & 1 << 5) != 0)
                    {
                        str_err += "Перегрузка по току УМ\r\n";
                    }
                    if ((errByte & 1 << 6) != 0)
                    {
                        str_err += "Перегрев привода азимута\r\n";
                    }
                    if ((errByte & 1 << 7) != 0)
                    {
                        str_err += "Перегрев привода тангажа\r\n";
                    }

                    errByte = com_in.DATA[9];
                    if ((errByte & 1 << 0) != 0)
                    {
                        str_err += "Активирован режим стартовых операций\r\n";
                    }
                    if ((errByte & 1 << 1) != 0)
                    {
                        str_err += "Ошибка АЦП датчиков температуры\r\n";
                    }
                    if ((errByte & 1 << 2) != 0)
                    {
                        CountMUGSPErrAzNeVDopuske++;
                        str_err += "Ошибка по азимуту не в допуске, пакетов:" + CountMUGSPErrAzNeVDopuske.ToString() + "\r\n";
                    }
                    if ((errByte & 1 << 3) != 0)
                    {
                        CountMUGSPErrTangNeVDopuske++;
                        str_err += "Ошибка по тангажу не в допуске, пакетов:" + CountMUGSPErrTangNeVDopuske.ToString() + "\r\n";
                    }
                    if ((errByte & 1 << 4) != 0)
                    {
                        str_err += "Превышение тока привода азимута\r\n";
                    }
                    if ((errByte & 1 << 5) != 0)
                    {
                        str_err += "Превышение тока привода тангажа\r\n";
                    }
                    if ((errByte & 1 << 6) != 0)
                    {
                        str_err += "Превышение температуры привода азимута\r\n";
                    }
                    if ((errByte & 1 << 7) != 0)
                    {
                        str_err += "Превышение температуры привода тангажа\r\n";
                    }

                    //tbErrors.Text = str_err;
                }));

                    ShowBuf(com_in.GetBufToSend(), false);
                    setTagUartReceived(true);
                }
                //END МУ ГСП

                //ТВК 1
                if (TVK1InfExchangeONOFF && cycleIndex == 1)
                {
                    if (com_in.LENGTH[0] * 2 != TVK1_DATA_SIZE_IN)
                    {
                        cntOrderErrors++;
                        return;
                    }
                    string strStatusTVK1 = "";                    

                    //состояние READY
                    byte[] byteArr = new byte[1];
                    byte[] byteArr3 = new byte[1];
                    byteArr[0] = com_in.DATA[0];
                    byteArr3[0] = com_in.DATA[2];

                    BitArray bitArray = new BitArray(byteArr);
                    BitArray bitArray3 = new BitArray(byteArr3);

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        textBoxTVK1VIDEO_IN_STATE.Text = "";
                        textBoxTVK1VIDEO_OUT_STATE_IN.Text = "";
                        if (!bitArray.Get(2))
                            textBoxTVK1VIDEO_IN_STATE.Text += "данные не принимаются \n";
                        else
                            textBoxTVK1VIDEO_IN_STATE.Text += "данные принимаются \n";
                        if (!bitArray.Get(3))
                            textBoxTVK1VIDEO_OUT_STATE_IN.Text += "данные не передаются\n";
                        else
                            textBoxTVK1VIDEO_OUT_STATE_IN.Text += "данные передаются\n";

                        //получить int из PQRS формата
                        if (CAM_FocusPosInq && com_in.DATA[2] == 0x90 && com_in.DATA[3] == 0x50 && com_in.DATA[8] == 0xFF)
                        {
                            byte p = com_in.DATA[7];
                            byte q = com_in.DATA[6];
                            byte r = com_in.DATA[5];
                            byte s = com_in.DATA[4];

                            BitArray bitArrayFocusPos = new BitArray(16);
                            
                            byte[] myByteP = new byte[1];
                            myByteP[0] = p;
                            BitArray bitArrFocus0 = new BitArray(myByteP);
                            byte[] myByteQ = new byte[1];
                            myByteQ[0] = q;
                            BitArray bitArrFocus1 = new BitArray(myByteQ);
                            byte[] myByteR = new byte[1];
                            myByteR[0] = r;
                            BitArray bitArrFocus2 = new BitArray(myByteR);
                            byte[] myByteS = new byte[1];
                            myByteS[0] = s;
                            BitArray bitArrFocus3 = new BitArray(myByteS);

                            bitArrayFocusPos.Set(0, bitArrFocus0.Get(0));
                            bitArrayFocusPos.Set(1, bitArrFocus0.Get(1));
                            bitArrayFocusPos.Set(2, bitArrFocus0.Get(2));
                            bitArrayFocusPos.Set(3, bitArrFocus0.Get(3));
                            bitArrayFocusPos.Set(4, bitArrFocus1.Get(0));
                            bitArrayFocusPos.Set(5, bitArrFocus1.Get(1));
                            bitArrayFocusPos.Set(6, bitArrFocus1.Get(2));
                            bitArrayFocusPos.Set(7, bitArrFocus1.Get(3));

                            bitArrayFocusPos.Set(8, bitArrFocus2.Get(0));
                            bitArrayFocusPos.Set(9, bitArrFocus2.Get(1));
                            bitArrayFocusPos.Set(10, bitArrFocus2.Get(2));
                            bitArrayFocusPos.Set(11, bitArrFocus2.Get(3));
                            bitArrayFocusPos.Set(12, bitArrFocus3.Get(0));
                            bitArrayFocusPos.Set(13, bitArrFocus3.Get(1));
                            bitArrayFocusPos.Set(14, bitArrFocus3.Get(2));
                            bitArrayFocusPos.Set(15, bitArrFocus3.Get(3));

                            int[] arrayPQRS = new int[1];
                            bitArrayFocusPos.CopyTo(arrayPQRS, 0);
                            int PQRS = arrayPQRS[0];
                            TVK1FocusPosition = PQRS;

                            //strStatusTVK1 += "Focus Position: " + Convert.ToString(TVK1ZoomPosition) + "\n";
                            CAM_FocusPosInq = false;
                        }

                        if (CAM_ZoomPosInq && com_in.DATA[2] == 0x90 && com_in.DATA[3] == 0x50 && com_in.DATA[8] == 0xFF)
                        {
                            byte p = com_in.DATA[7];
                            byte q = com_in.DATA[6];
                            byte r = com_in.DATA[5];
                            byte s = com_in.DATA[4];

                            BitArray bitArrayZoomPos = new BitArray(16);

                            byte[] myByteP = new byte[1];
                            myByteP[0] = p;
                            BitArray bitArrZoom0 = new BitArray(myByteP);
                            byte[] myByteQ = new byte[1];
                            myByteQ[0] = q;
                            BitArray bitArrZoom1 = new BitArray(myByteQ);
                            byte[] myByteR = new byte[1];
                            myByteR[0] = r;
                            BitArray bitArrZoom2 = new BitArray(myByteR);
                            byte[] myByteS = new byte[1];
                            myByteS[0] = s;
                            BitArray bitArrZoom3 = new BitArray(myByteS);

                            bitArrayZoomPos.Set(0, bitArrZoom0.Get(0));
                            bitArrayZoomPos.Set(1, bitArrZoom0.Get(1));
                            bitArrayZoomPos.Set(2, bitArrZoom0.Get(2));
                            bitArrayZoomPos.Set(3, bitArrZoom0.Get(3));
                            bitArrayZoomPos.Set(4, bitArrZoom1.Get(0));
                            bitArrayZoomPos.Set(5, bitArrZoom1.Get(1));
                            bitArrayZoomPos.Set(6, bitArrZoom1.Get(2));
                            bitArrayZoomPos.Set(7, bitArrZoom1.Get(3));

                            bitArrayZoomPos.Set(8, bitArrZoom2.Get(0));
                            bitArrayZoomPos.Set(9, bitArrZoom2.Get(1));
                            bitArrayZoomPos.Set(10, bitArrZoom2.Get(2));
                            bitArrayZoomPos.Set(11, bitArrZoom2.Get(3));
                            bitArrayZoomPos.Set(12, bitArrZoom3.Get(0));
                            bitArrayZoomPos.Set(13, bitArrZoom3.Get(1));
                            bitArrayZoomPos.Set(14, bitArrZoom3.Get(2));
                            bitArrayZoomPos.Set(15, bitArrZoom3.Get(3));

                            int[] arrayPQRS = new int[1];
                            bitArrayZoomPos.CopyTo(arrayPQRS, 0);
                            int PQRS = arrayPQRS[0];
                            TVK1ZoomPosition = PQRS;

                            CAM_ZoomPosInq = false;
                        }


                        if (com_in.DATA[2]==0x90&& com_in.DATA[3] == 0x50&& com_in.DATA[4] == 0x02&& com_in.DATA[5] == 0xFF)
                            TVK1AutoFocus = "Режим фокуса: автофокус\n";                        
                        if (com_in.DATA[2] == 0x90 && com_in.DATA[3] == 0x50 && com_in.DATA[4] == 0x03 && com_in.DATA[5] == 0xFF)
                            TVK1AutoFocus = "Режим фокуса: ручной\n";


                        strStatusTVK1 += TVK1AutoFocus;
                        strStatusTVK1 += "Zoom Position: " + Convert.ToString(TVK1ZoomPosition) + "\n";
                        textBoxTVK1ZOOM_IN_STATE.Text = Convert.ToString(TVK1ZoomPosition);
                        strStatusTVK1 += "Focus Position: " + Convert.ToString(TVK1FocusPosition) + "\n";
                        textBoxTVK1FOCUS_IN_STATE.Text = Convert.ToString(TVK1FocusPosition);
                        if (tvk1StatusWindow != null)
                            tvk1StatusWindow.fillData(strStatusTVK1);
                    }));
                    setTagUartReceived(true);
                }
                //ТВК 2
                if (TVK2InfExchangeONOFF && cycleIndex == 3)
                {
                    if (com_in.LENGTH[0] * 2 != TVK2_DATA_SIZE_IN)
                    {
                        cntOrderErrors++;
                        return;
                    }
                    string strStatusTVK2 = "";                    
                    //состояние READY
                    byte[] byteArr = new byte[1];
                    byteArr[0] = com_in.DATA[0];

                    BitArray bitArray = new BitArray(byteArr);

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        textBoxTVK2READY_IN.Text = "";
                        textBoxTVK2VIDEO_OUT_STATE_IN.Text = "";
                        if (!bitArray.Get(0))
                            textBoxTVK2READY_IN.Text += "Камера выкл. или не приним. видеоинформация\n";
                        else
                            textBoxTVK2READY_IN.Text += "Камера включена и данные принимаются верно\n";
                        //VIDEO_OUT_STATE
                        if (!bitArray.Get(1))
                            textBoxTVK2VIDEO_OUT_STATE_IN.Text += "Данные не передаются\n";
                        else
                            textBoxTVK2VIDEO_OUT_STATE_IN.Text += "Данные передаются\n";

                        
                        if (!bitArray.Get(2))
                            strStatusTVK2 += "Режим экспозиции: 0 - автоматический\n";
                        else
                            strStatusTVK2 += "Режим экспозиции: 1 - ручной\n";
                        if (!bitArray.Get(3))
                            strStatusTVK2 += "Режим контрастирования: 0 - автоматический\n";
                        else
                            strStatusTVK2 += "Режим контрастирования: 1 - ручной\n";
                        if (!bitArray.Get(4)&& !bitArray.Get(5))
                            strStatusTVK2 += "Режим захвата изображения: 0 - видео, вся матрица с биннингом\n";
                        if (bitArray.Get(4) && !bitArray.Get(5))
                            strStatusTVK2 += "Режим захвата изображения: 1 - видео, центральная часть матрицы\n";
                        if (!bitArray.Get(4) && bitArray.Get(5))
                            strStatusTVK2 += "Режим захвата изображения: 2 - фото, вся матрица пиксель в пиксель\n";
                        if (!bitArray.Get(6) && !bitArray.Get(7))
                            strStatusTVK2 += "Режим HDR (только для ручного режима управления экспозицией): 0 - выключен\n";
                        if (bitArray.Get(6) && !bitArray.Get(7))
                            strStatusTVK2 += "Режим HDR (только для ручного режима управления экспозицией): 1 - interleaved read-out\n";
                        if (!bitArray.Get(6) && bitArray.Get(7))
                            strStatusTVK2 += "Режим HDR (только для ручного режима управления экспозицией): 2 - multiple slope\n";

                        //получить CONTRAST_GAIN - число с фиксированной точкой 
                        double contrastGain;
                        int wholePart;
                        int fractionPart;
                        byte[] byteArrCONTRAST_GAIN = new byte[1];
                        byte[] byteArrCONTRAST_GAIN2 = new byte[1];
                        byte[] byteArrWholePartCONTRAST_GAIN = new byte[2];//2 байта для использования в BitConverter.ToUInt16()
                        byte[] byteArrFractionPartCONTRAST_GAIN = new byte[2];//2 байта т.к. 9 бит и для использования в BitConverter.ToUInt16()


                        byteArrCONTRAST_GAIN[0] = com_in.DATA[1];
                        byteArrCONTRAST_GAIN2[0] = com_in.DATA[2];
                        BitArray bitArrWholePartCONTRAST_GAIN = new BitArray(byteArrCONTRAST_GAIN2);
                        bitArrWholePartCONTRAST_GAIN.Set(7, false);

                        BitArray bitArrFractionPartCONTRAST_GAIN = new BitArray(9);
                        BitArray bitArr_ = new BitArray(byteArrCONTRAST_GAIN);

                        bitArrFractionPartCONTRAST_GAIN.Set(0, bitArr_.Get(0));
                        bitArrFractionPartCONTRAST_GAIN.Set(1, bitArr_.Get(1));
                        bitArrFractionPartCONTRAST_GAIN.Set(2, bitArr_.Get(2));
                        bitArrFractionPartCONTRAST_GAIN.Set(3, bitArr_.Get(3));
                        bitArrFractionPartCONTRAST_GAIN.Set(4, bitArr_.Get(4));
                        bitArrFractionPartCONTRAST_GAIN.Set(5, bitArr_.Get(5));
                        bitArrFractionPartCONTRAST_GAIN.Set(6, bitArr_.Get(6));
                        bitArrFractionPartCONTRAST_GAIN.Set(7, bitArr_.Get(7));
                        byte byte_=com_in.DATA[2];
                        if ((byte_ & 1<<0) != 0)
                            bitArrFractionPartCONTRAST_GAIN.Set(8, true);


                        //старшие 7 бит - целая часть
                        /*byteArrWholePartCONTRAST_GAIN[0] = byteArrCONTRAST_GAIN2[1];
                        byteArrWholePartCONTRAST_GAIN[1] = byteArrCONTRAST_GAIN2[2];
                        byteArrWholePartCONTRAST_GAIN[2] = byteArrCONTRAST_GAIN2[3];
                        byteArrWholePartCONTRAST_GAIN[3] = byteArrCONTRAST_GAIN2[4];
                        byteArrWholePartCONTRAST_GAIN[4] = byteArrCONTRAST_GAIN2[5];
                        byteArrWholePartCONTRAST_GAIN[5] = byteArrCONTRAST_GAIN2[6];
                        byteArrWholePartCONTRAST_GAIN[6] = byteArrCONTRAST_GAIN2[7];*/
                        //младшие 9 бит - дробная часть
                        /*byteArrFractionPartCONTRAST_GAIN[0] = byteArrCONTRAST_GAIN[0];
                        byteArrFractionPartCONTRAST_GAIN[1] = byteArrCONTRAST_GAIN[1];
                        byteArrFractionPartCONTRAST_GAIN[2] = byteArrCONTRAST_GAIN[2];
                        byteArrFractionPartCONTRAST_GAIN[3] = byteArrCONTRAST_GAIN[3];
                        byteArrFractionPartCONTRAST_GAIN[4] = byteArrCONTRAST_GAIN[4];
                        byteArrFractionPartCONTRAST_GAIN[5] = byteArrCONTRAST_GAIN[5];
                        byteArrFractionPartCONTRAST_GAIN[6] = byteArrCONTRAST_GAIN[6];
                        byteArrFractionPartCONTRAST_GAIN[7] = byteArrCONTRAST_GAIN[7];*/
                        //byteArrFractionPartCONTRAST_GAIN[1] = byteArrCONTRAST_GAIN2[0];

                        //int[] arrayPQRS = new int[1];
                        //bitArrayFocusPos.CopyTo(arrayPQRS, 0);

                        wholePart = BitConverter.ToUInt16(byteArrWholePartCONTRAST_GAIN, 0);
                        fractionPart = BitConverter.ToUInt16(byteArrFractionPartCONTRAST_GAIN, 0);

                        int[] array = new int[1];
                        bitArrWholePartCONTRAST_GAIN.CopyTo(array, 0);
                        wholePart = array[0];

                        bitArrFractionPartCONTRAST_GAIN.CopyTo(array, 0);
                        fractionPart = array[0];

                        contrastGain = wholePart + fractionPart/1000.0;
                        
                        strStatusTVK2 += "Цифровое усиление блока контрастирования: "+ Convert.ToString(contrastGain)+ "\n";
                        //END CONTRAST_GAIN

                        byte[] byteArrCONTRAST_OFFSET = new byte[2];
                        byteArrCONTRAST_OFFSET[0] = com_in.DATA[3];
                        byteArrCONTRAST_OFFSET[1] = com_in.DATA[4];
                        int contrastOffset = BitConverter.ToInt16(byteArrCONTRAST_OFFSET, 0);
                        strStatusTVK2 += "Цифровое смещение блока контрастирования: " + Convert.ToString(BitConverter.ToInt16(byteArrCONTRAST_OFFSET, 0)) + "\n";

                        byte[] byteArrCMV_OFFSET_BOT = new byte[2];
                        byteArrCMV_OFFSET_BOT[0] = com_in.DATA[5];
                        byteArrCMV_OFFSET_BOT[1] = com_in.DATA[6];
                        strStatusTVK2 += "Состояние параметра Offset_bot матрицы cmosis (регистр 87): " + Convert.ToString(BitConverter.ToInt16(byteArrCMV_OFFSET_BOT, 0)) + "\n";
                        //CMV_OFFSET_TOP
                        byte[] CMV_OFFSET_TOP = new byte[2];
                        CMV_OFFSET_TOP[0] = com_in.DATA[7];
                        CMV_OFFSET_TOP[1] = com_in.DATA[8];
                        strStatusTVK2 += "Состояние параметра Offset_bot матрицы cmosis (регистр 88): " + Convert.ToString(BitConverter.ToInt16(CMV_OFFSET_TOP, 0)) + "\n";
                        
                        //CMV_PGA_GAIN
                        byte[] byte10 = new byte[1];
                        byte[] arrCMV_PGA_GAIN = new byte[2];

                        byte10[0] = com_in.DATA[9];
                        BitArray bitArray10 = new BitArray(byte10);//создать битовый массив по байтовому массиву
                        BitArray bitArrayCMV_PGA_GAIN = new BitArray(8);

                        bitArrayCMV_PGA_GAIN.Set(0, bitArray10.Get(0));//получить значение нового битового массива по трем битам существующего
                        bitArrayCMV_PGA_GAIN.Set(1, bitArray10.Get(1));
                        bitArrayCMV_PGA_GAIN.Set(2, bitArray10.Get(2));

                        arrCMV_PGA_GAIN[0] = ConvertToByte(bitArrayCMV_PGA_GAIN);//сконвертировать в байтовый массив
                        strStatusTVK2 += "Состояние параметра PGA_gain матрицы cmosis (регистр 115): " + Convert.ToString(BitConverter.ToInt16(arrCMV_PGA_GAIN, 0)) + "\n";

                        if(bitArray10.Get(3))
                            strStatusTVK2 += "Состояние параметра PGA_div матрицы cmosis (регистр 115): true\n";
                        else
                            strStatusTVK2 += "Состояние параметра PGA_div матрицы cmosis (регистр 115): false\n";
                        //CMV_ADC_RANGE_MULT
                        byte[] arrByte10 = new byte[2];
                        BitArray bitArrayCMV_ADC_RANGE_MULT = new BitArray(8);

                        bitArrayCMV_ADC_RANGE_MULT.Set(0, bitArray10.Get(4));//получить значение нового битового массива по 2 битам существующего
                        bitArrayCMV_ADC_RANGE_MULT.Set(1, bitArray10.Get(5));

                        arrByte10[0] = ConvertToByte(bitArrayCMV_ADC_RANGE_MULT);//сконвертировать в байтовый массив
                        strStatusTVK2 += "Состояние параметра ADC_range_mult матрицы cmosis (регистр 116): " + Convert.ToString(BitConverter.ToInt16(arrByte10, 0)) + "\n";
                        //CMV_ADC_RANGE_MULT2
                        byte[] arrByte102 = new byte[2];
                        BitArray bitArrayCMV_ADC_RANGE_MULT2 = new BitArray(8);

                        bitArrayCMV_ADC_RANGE_MULT2.Set(0, bitArray10.Get(6));//получить значение нового битового массива по 2 битам существующего
                        bitArrayCMV_ADC_RANGE_MULT2.Set(1, bitArray10.Get(7));

                        arrByte102[0] = ConvertToByte(bitArrayCMV_ADC_RANGE_MULT2);//сконвертировать в байтовый массив
                        strStatusTVK2 += "Состояние параметра ADC_range_mult2 матрицы cmosis (регистр 116): " + Convert.ToString(BitConverter.ToInt16(arrByte102, 0)) + "\n";
                        //CMV_ADC_range
                        byte[] CMV_ADC_range = new byte[2];
                        CMV_OFFSET_TOP[0] = com_in.DATA[10];
                        
                        strStatusTVK2 += "Состояние параметра ADC_range матрицы cmosis (регистр 116): " + Convert.ToString(BitConverter.ToInt16(CMV_ADC_range, 0)) + "\n";
                        /*//EXPOSURE
                        byte[] EXPOSURE = new byte[3];
                        EXPOSURE[0] = com_in.DATA[11];
                        EXPOSURE[1] = com_in.DATA[12];
                        EXPOSURE[2] = com_in.DATA[13];

                        strStatusTVK2 += "Значение времени экспозиции: " + Convert.ToString(BitConverter.ToInt32(EXPOSURE, 0)) + "\n";
                        //HDR_EXPOSURE1
                        byte[] HDR_EXPOSURE1 = new byte[3];
                        EXPOSURE[0] = com_in.DATA[14];
                        EXPOSURE[1] = com_in.DATA[15];
                        EXPOSURE[1] = com_in.DATA[16];

                        strStatusTVK2 += "Значение времени экспозиции (в режимах HDR): " + Convert.ToString(BitConverter.ToInt32(HDR_EXPOSURE1, 0)) + "\n";
                        //HDR_EXPOSURE2
                        byte[] HDR_EXPOSURE2 = new byte[3];
                        HDR_EXPOSURE2[0] = com_in.DATA[17];
                        HDR_EXPOSURE2[1] = com_in.DATA[18];
                        HDR_EXPOSURE2[2] = com_in.DATA[19];

                        strStatusTVK2 += "Значение времени экспозиции (в режиме HDR multiple slope): " + Convert.ToString(BitConverter.ToInt32(HDR_EXPOSURE2, 0)) + "\n";
                        //////////*/
                        /*byte[] byte21 = new byte[1];
                        byte[] byte22 = new byte[1];
                        byte[] byteCMV_VTFL2 = new byte[2];//2 байта для использования в BitConverter.ToUInt16()
                        byte[] byteCMV_VTFL3 = new byte[2];//2 байта для использования в BitConverter.ToUInt16()
                        byte[] byteCMV_NUMBER_SLOPES = new byte[2];//2 байта для использования в BitConverter.ToUInt16()                  

                        byte21[0] = com_in.DATA[20];
                        byte22[0] = com_in.DATA[21];
                        
                        byteCMV_VTFL2[0] = byte21[0];
                        byteCMV_VTFL2[1] = byte21[1];
                        byteCMV_VTFL2[2] = byte21[2];
                        byteCMV_VTFL2[3] = byte21[3];
                        byteCMV_VTFL2[4] = byte21[4];
                        byteCMV_VTFL2[5] = byte21[5];
                        byteCMV_VTFL2[6] = byte21[6];

                        byteCMV_VTFL3[0] = byte22[7];
                        byteCMV_VTFL3[1] = byte22[8];
                        byteCMV_VTFL3[2] = byte22[9];
                        byteCMV_VTFL3[3] = byte22[10];
                        byteCMV_VTFL3[4] = byte22[11];
                        byteCMV_VTFL3[5] = byte22[12];
                        byteCMV_VTFL3[6] = byte22[13];
                        
                        byteCMV_NUMBER_SLOPES[0] = byte22[14];
                        byteCMV_NUMBER_SLOPES[1] = byte22[15];

                        strStatusTVK2 += "Значение параметра Vtfl2 матрицы cmosis (регистр 106): " + Convert.ToString(BitConverter.ToInt16(byteCMV_VTFL2, 0)) + "\n";
                        strStatusTVK2 += "Значение параметра Vtfl3 матрицы cmosis (регистр 106): " + Convert.ToString(BitConverter.ToInt16(byteCMV_VTFL3, 0)) + "\n";
                        strStatusTVK2 += "Значение параметра Number_slopes матрицы cmosis (регистр 79): " + Convert.ToString(BitConverter.ToInt16(byteCMV_NUMBER_SLOPES, 0)) + "\n";   */
                        ////////////

                        if (tvk2StatusWindow != null)
                        tvk2StatusWindow.fillData(strStatusTVK2);
                    }));
                    setTagUartReceived(true);
                    if (tvk2StatusWindow != null)
                        Thread.Sleep(10);//для недопущения подвисания в окне состояния 
            }
                //ТПВК
                if (TPVKInfExchangeONOFF && cycleIndex == 5)
                {
                    if (com_in.LENGTH[0] * 2 != TPVK_DATA_SIZE_IN)
                    {
                        cntOrderErrors++;
                        return;
                    }
                    string strStatusTPVK = "";
                    
                    byte[] byteArr = new byte[1];
                    byte[] byteArr2 = new byte[1];
                    //состояние READY
                    byte[] byteArr3 = new byte[1];
                    byte[] byteArr4 = new byte[1];
                    byte[] byteArr56 = new byte[2];
                    byte[] byteArr78 = new byte[2];
                    byte[] byteArr9 = new byte[2];
                    byte[] byteArr10 = new byte[2];
                    byte[] byteArr11 = new byte[2];
                    byte[] byteArr12 = new byte[2];
                    byte[] byteArr13 = new byte[1];

                    byteArr[0] = com_in.DATA[0];
                    byteArr2[0] = com_in.DATA[1];
                    byteArr3[0] = com_in.DATA[2];
                    byteArr4[0] = com_in.DATA[3];
                    byteArr56[0] = com_in.DATA[4];
                    byteArr56[1] = com_in.DATA[5];
                    byteArr78[0] = com_in.DATA[6];
                    byteArr78[1] = com_in.DATA[7];
                    byteArr9[0] = com_in.DATA[8];
                    byteArr10[0] = com_in.DATA[9];                    
                    byteArr11[0] = com_in.DATA[10];
                    byteArr12[0] = com_in.DATA[11];
                    byteArr13[0] = com_in.DATA[12];

                    BitArray bitArray = new BitArray(byteArr);
                    BitArray bitArray2 = new BitArray(byteArr2);
                    BitArray bitArray3 = new BitArray(byteArr3);
                    BitArray bitArray4 = new BitArray(byteArr4);
                    BitArray bitArray11 = new BitArray(byteArr11);
                    BitArray bitArray12 = new BitArray(byteArr12);
                    BitArray bitArray13 = new BitArray(byteArr13);


                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        strStatusTPVK = "";
                        textBoxTPVKREADY_IN.Text = "";
                        textBoxTPVKPOWER_STATE_IN.Text = "";
                        textBoxTPVKVIDEO_OUT_STATE_IN.Text = "";
                        textBoxTPVKZOOM_IN.Text = "";
                        textBoxTPVKFOCUS_IN.Text = "";
                        textBoxTPVKGAIN_IN.Text = "";
                        textBoxTPVKLEVEL_IN.Text = "";
                        textBoxTPVKENHANCE_IN.Text = "";
                        textBoxTPVKEXPOSURE_IN.Text = "";                 
                        
                        if (!bitArray3.Get(0))
                            textBoxTPVKREADY_IN.Text = "Питание отключено\n";
                        else
                            textBoxTPVKREADY_IN.Text = "Питание включено\n";
                        if (!bitArray.Get(0))
                            textBoxTPVKPOWER_STATE_IN.Text = "Питание отключено\n";
                        else
                            textBoxTPVKPOWER_STATE_IN.Text = "Питание включено\n";
                        if (!bitArray.Get(2))
                            textBoxTPVKVIDEO_OUT_STATE_IN.Text = "Данные не передаются\n";
                        else
                            textBoxTPVKVIDEO_OUT_STATE_IN.Text = "Данные передаются\n";

                        TPVKFocus_IN = BitConverter.ToInt16(byteArr10, 0);
                        textBoxTPVKFOCUS_IN.Text = TPVKFocus_IN.ToString();
                        TPVKZoom_IN = BitConverter.ToInt16(byteArr11, 0);
                        textBoxTPVKZOOM_IN.Text = TPVKZoom_IN.ToString();

                        TPVKExposure_IN = BitConverter.ToInt16(byteArr9, 0);
                        if(TPVKExposure_IN==0)
                            textBoxTPVKEXPOSURE_IN.Text = "Холодный режим";
                        if (TPVKExposure_IN == 1)
                            textBoxTPVKEXPOSURE_IN.Text = "Теплый режим";
                        if (TPVKExposure_IN == 2)
                            textBoxTPVKEXPOSURE_IN.Text = "Горячий режим";

                        textBoxTPVKGAIN_IN.Text = (BitConverter.ToInt16(byteArr78, 0)).ToString();
                        textBoxTPVKLEVEL_IN.Text = (BitConverter.ToInt16(byteArr56, 0)).ToString();

                        if (!bitArray.Get(1))
                            strStatusTPVK += "Состояние приемника видеоинформации от камеры ТПВ: " + "данные не принимаются\n";
                        else
                            strStatusTPVK += "Состояние приемника видеоинформации от камеры ТПВ: " + "данные принимаются\n";
                        if (!bitArray.Get(3))
                            strStatusTPVK += "Состояние канала управления камерой ТПВ: " + "нет связи\n";
                        else
                            strStatusTPVK += "Состояние канала управления камерой ТПВ: " + "есть связь\n";

                        if (!bitArray2.Get(0))
                            strStatusTPVK += "Текущий режим работы: " + "Standby mode (low consumption)\n";
                        else
                            strStatusTPVK += "Текущий режим работы: " + "Operational mode\n";
                        if (!bitArray2.Get(1))
                            strStatusTPVK += "Текущая полярность: " + "WHITE HOT\n";
                        else
                            strStatusTPVK += "Текущая полярность: " + "BLACK HOT\n";
                        if (!bitArray2.Get(5))
                            strStatusTPVK += "Состояние калибровки: " + "нормальная работа камеры\n";
                        else
                            strStatusTPVK += "Состояние калибровки: " + "камера находится в режиме калибровки\n";

                        if (!bitArray2.Get(6))
                            strStatusTPVK += "Текущий источник синхронизации: " + "внутренний генератор камеры\n";
                        else
                            strStatusTPVK += "Текущий источник синхронизации: " + "внешний сигнал\n";
                        if (!bitArray2.Get(7))
                            strStatusTPVK += "Текущее состояние автоматической калибровки: " + "отключена\n";
                        else
                            strStatusTPVK += "Текущее состояние автоматической калибровки: " + "интервалы проведения калибровки выбираются камерой\n";

                        if (!bitArray3.Get(1))
                            strStatusTPVK += "Текущее состояние механизма увеличения: " + "механизм неподвижен\n";
                        else
                            strStatusTPVK += "Текущее состояние механизма увеличения: " + "механизм изменяет положение\n";
                        if (!bitArray3.Get(2))
                            strStatusTPVK += "Текущее состояние механизма фокусировки: " + "механизм неподвижен\n";
                        else
                            strStatusTPVK += "Текущее состояние механизма фокусировки: " + "механизм изменяет положение\n";
                        if (!bitArray3.Get(2))
                            strStatusTPVK += "Текущее состояние охладителя: " + "охладитель находится в режиме выхода на рабочую температуру или режиме Standby\n";
                        else
                            strStatusTPVK += "Текущее состояние охладителя: " + "рабочая температура достигнута, охладитель находится в режиме поддержания рабочей температуры\n";

                        if (!bitArray4.Get(2) && !bitArray4.Get(3))
                            strStatusTPVK += "Текущее состояние прицельной марки: " + "выключена\n";
                        if (!bitArray4.Get(2) && bitArray4.Get(3))
                            strStatusTPVK += "Текущее состояние прицельной марки: " + "чернаяа \n";
                        if (bitArray4.Get(2) && !bitArray4.Get(3))
                            strStatusTPVK += "Текущее состояние прицельной марки: " + "серая \n";
                        if (bitArray4.Get(2) && bitArray4.Get(3))
                            strStatusTPVK += "Текущее состояние прицельной марки: " + "белая \n";

                        if (!bitArray4.Get(6))
                            strStatusTPVK += "Текущий режим автоматического выбора времени накопления сенсора: " + "выключен\n";
                        else
                            strStatusTPVK += "Текущий режим автоматического выбора времени накопления сенсора: " + "включен\n";

                        if (BitConverter.ToInt16(byteArr12, 0) ==0)
                        {
                            strStatusTPVK += "Текущее значение уровня улучшения изображения: " + "улучшение отключено\n";
                            textBoxTPVKENHANCE_IN.Text= "улучшение отключено";
                        }
                        if (BitConverter.ToInt16(byteArr12, 0) == 1)
                        {
                            strStatusTPVK += "Текущее значение уровня улучшения изображения: " + "минимальное улучшение изображения\n";
                            textBoxTPVKENHANCE_IN.Text = "минимальное улучшение изображения";
                        }
                        if (bitArray12.Get(0) && bitArray12.Get(1) && bitArray12.Get(2) && bitArray12.Get(3))
                        {
                            strStatusTPVK += "Текущее значение уровня улучшения изображения: " + "максимальное улучшение изображения\n";
                            textBoxTPVKENHANCE_IN.Text = "максимальное улучшение изображения";
                        }

                        if (bitArray12.Get(0) && !bitArray12.Get(4) && !bitArray12.Get(5) && !bitArray12.Get(6))
                        {
                            strStatusTPVK += "Режим контраста: режим малого контраста\n";
                            textBoxTPVKnhancementContrastModesState_IN.Text = "режим малого контраста";
                        }
                        if (bitArray12.Get(0) && bitArray12.Get(4) && bitArray12.Get(5) && bitArray12.Get(6))
                        {
                            strStatusTPVK += "Режим контраста: режим высокого контраста\n";
                            textBoxTPVKnhancementContrastModesState_IN.Text = "режим высокого контраста";
                        }

                        

                        if (!bitArray13.Get(0))
                            strStatusTPVK += "Признак ошибки механизма увеличения: " + "механизм работает нормально\n";
                        else
                            strStatusTPVK += "Признак ошибки механизма увеличения: " + "обнаружена ошибка в работе механизма\n";
                        if (!bitArray13.Get(1))
                            strStatusTPVK += "Признак ошибки механизма фокусировки: " + "механизм работает нормально\n";
                        else
                            strStatusTPVK += "Признак ошибки механизма фокусировки: " + "обнаружена ошибка в работе механизма\n";


                        if (tpvkStatusWindow != null)
                            tpvkStatusWindow.fillData(strStatusTPVK);
                    }));
                    setTagUartReceived(true);
                    if (tpvkStatusWindow != null)
                        Thread.Sleep(10);//для недопущения подвисания в окне состояния               
            }

                //ЛД
                if (LDInfExchangeONOFF && cycleIndex == 7)
                {
                    if (com_in.LENGTH[0] * 2 < LD_DATA_SIZE_IN || com_in.LENGTH[0] * 2 > 72)
                    {
                        cntOrderErrors++;
                        return;
                    }
                //состояние SOST
                byte[] byteArr = new byte[1];
                    byte[] byteArr2 = new byte[1];
                    byteArr[0] = com_in.DATA[1];
                    byteArr2[0] = com_in.DATA[2];

                    BitArray bitArray = new BitArray(byteArr);
                    BitArray bitArray2 = new BitArray(byteArr2);
                    string strStatusLD = "";

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    textBoxLDSOST_IN.Text = "";
                    if (!bitArray.Get(0) && !bitArray.Get(1) && !bitArray.Get(2))
                        textBoxLDSOST_IN.Text += "Подготовка к работе\n";
                    if (bitArray.Get(0) && !bitArray.Get(1) && !bitArray.Get(2))
                        textBoxLDSOST_IN.Text += "Готов к работе\n";
                    if (!bitArray.Get(0) && bitArray.Get(1) && !bitArray.Get(2))
                        textBoxLDSOST_IN.Text += "Идет цикл измерения дальности\n";
                    if (bitArray.Get(0) && bitArray.Get(1) && !bitArray.Get(2))
                        textBoxLDSOST_IN.Text += "Дальномер неисправен\n";

                    strStatusLD = "";
                    if (!bitArray.Get(4) && !bitArray.Get(5) && !bitArray.Get(6) && !bitArray.Get(7))
                        strStatusLD += "измерения дальности не проводились\n";
                    if (bitArray.Get(4) && !bitArray.Get(5) && !bitArray.Get(6) && !bitArray.Get(7))
                        strStatusLD += "штатное завершение цикла ИД\n";
                    if (!bitArray.Get(4) && bitArray.Get(5) && !bitArray.Get(6) && !bitArray.Get(7))
                        strStatusLD += "много целей\n";
                    if (bitArray.Get(4) && bitArray.Get(5) && !bitArray.Get(6) && !bitArray.Get(7))
                        strStatusLD += "промах\n";
                    if (!bitArray.Get(4) && !bitArray.Get(5) && bitArray.Get(6) && !bitArray.Get(7))
                        strStatusLD += "нет старта\n";
                    if (bitArray.Get(4) && !bitArray.Get(5) && bitArray.Get(6) && !bitArray.Get(7))
                        strStatusLD += "цель в стробе\n";

                    if (bitArray2.Get(0))
                        strStatusLD += "нет готовности БВВ\n";
                    if (bitArray2.Get(1))
                        strStatusLD += "нет запуска БВВ\n";
                    if (!bitArray2.Get(2))
                        strStatusLD += "Режим БВВ (затвор): 0 – активный\n";
                    else
                        strStatusLD += "Режим БВВ (затвор): 1 – пассивный\n";

                    if (!bitArray2.Get(3))
                        strStatusLD += "Серия БВВ: 0 – одиночный(Р max)\n";
                    else
                        strStatusLD += "Серия БВВ: 1 – серия\n";
                    if (!bitArray2.Get(4))
                        strStatusLD += "Блокировка ЛД:0 – нет\n";
                    else
                        strStatusLD += "Блокировка ЛД:1 – есть\n";
                    if (!bitArray2.Get(5))
                        strStatusLD += "Блокировка ФПУ: 0 – нет\n";
                    else
                        strStatusLD += "Блокировка ФПУ: 1 – есть\n";
                    if (!bitArray2.Get(6))
                        strStatusLD += "Тип ФПУ:0 – ФПУ - 35\n";
                    else
                        strStatusLD += "Тип ФПУ: 1 – ФПУ - 21ВТ\n";
                    if (bitArray2.Get(7))
                        strStatusLD += "Цель меньше Dmin:1 – есть\n";
                    else
                        strStatusLD += "Цель меньше Dmin:0 – нет\n";

                    byte[] byteArr3 = new byte[4];
                    byteArr3[0] = com_in.DATA[3];
                    byteArr3[1] = com_in.DATA[4];
                    byteArr3[2] = com_in.DATA[5];
                    int valTIME_MSU = BitConverter.ToInt32(byteArr3, 0);

                    strStatusLD += "TIME_MSU: " + valTIME_MSU.ToString() + "\n";

                    byte[] byteArr4 = new byte[2];
                    byteArr4[0] = com_in.DATA[6];
                    byteArr4[1] = com_in.DATA[7];

                    short valNUM_TARGET = BitConverter.ToInt16(byteArr4, 0);
                    strStatusLD += "NUM_TARGET: " + valNUM_TARGET.ToString() + "\n";

                    //заполнить массив дальностей до целей ЛД                    
                    if (com_in.DATA.Length > 8 && valNUM_TARGET > 0 && valNUM_TARGET < 33)
                    {
                        byte[] byteArr5 = new byte[2];
                        short[] arrDist = new short[valNUM_TARGET];
                        List<LDTableTargetsDistances> result = new List<LDTableTargetsDistances>(valNUM_TARGET);
                        for (int i = 0; i < valNUM_TARGET; i++)
                        {
                            byteArr5[0] = com_in.DATA[6 + i*2+2];
                            byteArr5[1] = com_in.DATA[7 + i*2+2];
                            arrDist[i] = BitConverter.ToInt16(byteArr5, 0);
                            result.Add(new LDTableTargetsDistances(i + 1, arrDist[i].ToString()));

                        }
                        dataGridLDTargetsDistances.ItemsSource = result;
                    }

                    if (ldStatusWindow != null)
                        ldStatusWindow.fillData(strStatusLD);
                }));
                    setTagUartReceived(true);
                Thread.Sleep(1);//для недопущения подвисания в dataGrid
            }
                //ЛД END
                //now SEND COMMAND immediately ! (not using tmrExchange)
                //InitSendCommand();
        }

        public void ControlChanged(object sender, EventArgs e)
        {
            UpdateStructure_OUT();
        }

        /*private void comboBoxGearModeAZ_DropDownClosed(object sender, EventArgs e)
        {
            //GearMode
            int ind = comboBoxGearModeAZ.SelectedIndex;

            if (ind==2 || ind == 3 || ind == 4 || ind == 5)
            {
                sliderAZAngle.IsEnabled = true;
                numAZAngle.IsEnabled = true;
                buttonResetGSPAngleControls.IsEnabled = true;

                if (ind==2 || ind == 3)
                {
                    sliderAZAngle.Maximum = 300;
                    sliderAZAngle.Minimum = -300;
                    numAZAngle.Maximum = 300;
                    numAZAngle.Minimum = -300;
                }
                if (ind == 4 || ind == 5)
                {
                    sliderAZAngle.Maximum = 180-360/Math.Pow(2,24);
                    sliderAZAngle.Minimum = -180;
                    numAZAngle.Maximum = 180 - 360 / Math.Pow(2, 24);
                    numAZAngle.Minimum = -180;
                }
            }
            else
            {
                sliderAZAngle.IsEnabled = false;
                numAZAngle.IsEnabled = false;
                buttonResetGSPAngleControls.IsEnabled = false;
            }

            ControlChanged(sender, e);
        }*/

        void uart_bufferSent(byte[] buf)
        {
            ShowBuf(buf, true);
        }

        private void sliderELAngle_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderELAngle.Value = (int)numELAngle.Value;
            ControlChanged(sender, e);
        }

        private void sliderELAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numELAngle.Value = (int)sliderELAngle.Value;
        }

        private void sliderAZAngle_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderAZAngle.Value = (int)numAZAngle.Value;
            ControlChanged(sender, e);
        }

        private void sliderAZAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numAZAngle.Value = (int)sliderAZAngle.Value;
        }

        /*private void comboBoxGearModeEL_DropDownClosed(object sender, EventArgs e)
        {
            int ind = comboBoxGearModeEL.SelectedIndex;

            if (ind == 2 || ind == 3 || ind == 4 || ind == 5)
            {
                sliderELAngle.IsEnabled = true;
                numELAngle.IsEnabled = true;
                buttonResetGSPSpeedControls.IsEnabled = true;

                if (ind == 2 || ind == 3)
                {
                    sliderELAngle.Maximum = 300;
                    sliderELAngle.Minimum = -300;
                    numELAngle.Maximum = 300;
                    numELAngle.Minimum = -300;
                }
                if (ind == 4 || ind == 5)
                {
                    sliderELAngle.Maximum = 180 - 360 / Math.Pow(2, 24);
                    sliderELAngle.Minimum = -180;
                    numELAngle.Maximum = 180 - 360 / Math.Pow(2, 24);
                    numELAngle.Minimum = -180;
                }
            }
            else
            {
                sliderELAngle.IsEnabled = false;
                numELAngle.IsEnabled = false;
                buttonResetGSPSpeedControls.IsEnabled = false;
            }

            ControlChanged(sender, e);
        }*/

        private void formMainWindow_Loaded(object sender, RoutedEventArgs e)
        {            
            sliderAZAngle.Value = 0;
            sliderELAngle.Value = 0;

            MyJoystick = new MyJoystick();
            MyJoystick.PeriodWorking = 50;
            MyJoystick.msgAppeared += msgAppeared;
            MyJoystick.myJoystickStateReceived += myJoystickStateReceived;
            MyJoystick.StartWorking();
        }

        private void numAZAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderAZAngle.Value = (int)numAZAngle.Value;
            ControlChanged(sender, e);
        }

        private void numELAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderELAngle.Value = (int)numELAngle.Value;
            ControlChanged(sender, e);
        }
        private void cbStartOperation_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void cbStartOperation_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void cbResetGSP_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void cbResetGSP_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        /*private void btnJoystickUse_Click(object sender, RoutedEventArgs e)
        {
            checkBoxJoystickUse.IsChecked = !checkBoxJoystickUse.IsChecked;

            int indAZ;
            comboBoxGearModeAZ.SelectedIndex = 2;
            indAZ = comboBoxGearModeAZ.SelectedIndex;            

            int indEL;
            comboBoxGearModeEL.SelectedIndex = 2;
            indEL = comboBoxGearModeAZ.SelectedIndex;

            if ((bool)checkBoxJoystickUse.IsChecked)
            {
                sliderAZAngle.IsEnabled = true;
                numAZAngle.IsEnabled = true;
                buttonResetGSPAngleControls.IsEnabled = true;
                sliderELAngle.IsEnabled = true;
                numELAngle.IsEnabled = true;
                buttonResetGSPSpeedControls.IsEnabled = true;

                if (indAZ == 2 || indAZ == 3)
                {
                    sliderAZAngle.Maximum = 300;
                    sliderAZAngle.Minimum = -300;
                    numAZAngle.Maximum = 300;
                    numAZAngle.Minimum = -300;
                    sliderELAngle.Maximum = 300;
                    sliderELAngle.Minimum = -300;
                    numELAngle.Maximum = 300;
                    numELAngle.Minimum = -300;
                }
            }
            else
            {
                sliderAZAngle.IsEnabled = false;
                numAZAngle.IsEnabled = false;
                buttonResetGSPAngleControls.IsEnabled = false;
                sliderELAngle.IsEnabled = false;
                numELAngle.IsEnabled = false;
                buttonResetGSPSpeedControls.IsEnabled = false;
            }

        }*/

        /*private void numJoystickKValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ControlChanged(sender, e);
        }*/

        private void checkBoxTVK1_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTVK2_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVK_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxLD_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTVK2_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVK_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxLD_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void buttonResetEL_Click(object sender, RoutedEventArgs e)
        {
            numELAngle.Value = 0;
            ControlChanged(sender, e);
        }

        private void Joystick_Click(object sender, RoutedEventArgs e)
        {            
            if (joystickWindow != null)
            {
                joystickWindow.Close();
                joystickWindow = new JoystickWindow(this);
            }
            else
                joystickWindow = new JoystickWindow(this);

            joystickWindow.ShowDialog();            
        }

        private void checkBoxLDONOFF_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxLDONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            //cycleIndex = 0;
            //setTagUartReceived(true);
            arrayActiveDevicesInCycle[7] = false;
            activeDevicesArr[4] = false;

            if (cycleIndex == 7)//анализ на именно этот дивайс (т.к. приложение многопоточное и значение cycleIndex может уже измениться в другом потоке)
            {
                int nextDeviceIndex = getNextActiveDeviceInCycle(cycleIndex);
                if (nextDeviceIndex != -1)//если есть еще активные дивайсы
                {
                    cycleIndex = nextDeviceIndex;
                    //setTagUartReceived(true);  -- НЕ изменять tag на true ! т.к. из информ. обмена выключается модуль, но он не должен мешать работе оставшихся !
                }
                else
                    cycleIndex = 0;
            }

            ControlChanged(sender, e);
        }

        private void comboBoxLDCommands_DropDownClosed(object sender, EventArgs e)
        {
            //ControlChanged(sender, e);
        }

        private void radioButtonLDRegimVARUOnStart_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonLDRegimVARUBeforeStart_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxLDSynchronisationONOFF_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void sliderLDBrightnessLIghtVyvIzl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numLDBrightnessLIghtVyvIzl.Value = (int)sliderLDBrightnessLIghtVyvIzl.Value;
        }

        private void sliderLDBrightnessLIghtVyvIzl_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderLDBrightnessLIghtVyvIzl.Value = (int)numLDBrightnessLIghtVyvIzl.Value;
            ControlChanged(sender, e);
        }

        private void sliderLDBrightnessLIghtFPU_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderLDBrightnessLIghtFPU.Value = (int)numLDBrightnessLIghtFPU.Value;
            ControlChanged(sender, e);
        }

        private void sliderLDBrightnessLIghtFPU_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numLDBrightnessLIghtFPU.Value = (int)sliderLDBrightnessLIghtFPU.Value;
        }

        private void numLDBrightnessLIghtVyvIzl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderLDBrightnessLIghtVyvIzl.Value = (int)numLDBrightnessLIghtVyvIzl.Value;
            ControlChanged(sender, e);
        }

        private void numLDBrightnessLIghtFPU_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderLDBrightnessLIghtFPU.Value = (int)numLDBrightnessLIghtFPU.Value;
            ControlChanged(sender, e);
        }

        private void checkBoxLDSynchronisationONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxLDBlokirovkaIzluch_Unchecked(object sender, RoutedEventArgs e)
        {
            //comboBoxLDCommands.ItemsSource = collection;
            comboBoxLDCommands.Items.Clear();

            comboBoxLDCommands.Items.Add("Нет команды");
            //comboBoxLDCommands.Items.Add("Измерить дальность с фильтром");
            //comboBoxLDCommands.Items.Add("Измерить дальность без фильтра");
            //comboBoxLDCommands.Items.Add("Технол. цикл с включ. блок. излучения");
            if ((bool)checkBoxLDBlokirovkaFPU.IsChecked)
                comboBoxLDCommands.Items.Add("Технол. цикл с  включ. блок. ФПУ");
            else
            {
                comboBoxLDCommands.Items.Add("Измерить дальность с фильтром");
                comboBoxLDCommands.Items.Add("Измерить дальность без фильтра");
            }
            comboBoxLDCommands.SelectedIndex = 0;
            ControlChanged(sender, e);
        }

        private void checkBoxLDBlokirovkaIzluch_Checked(object sender, RoutedEventArgs e)
        {
            //ItemCollection collection = comboBoxLDCommands.Items;
            //comboBoxLDCommands.Items.Remove((ComboBoxItem)comboBoxLDCommands.Items.GetItemAt(1));
            //comboBoxLDCommands.Items.Remove((ComboBoxItem)comboBoxLDCommands.Items.GetItemAt(1));
            //comboBoxLDCommands.Items.Remove((ComboBoxItem)comboBoxLDCommands.Items.GetItemAt(2));

            comboBoxLDCommands.Items.Clear();
            
            comboBoxLDCommands.Items.Add("Нет команды");
            if (!(bool)checkBoxLDBlokirovkaFPU.IsChecked)
                comboBoxLDCommands.Items.Add("Технол. цикл с включ. блок. излучения");
            comboBoxLDCommands.SelectedIndex = 0;
            ControlChanged(sender, e);
        }

        private void checkBoxLDBlokirovkaFPU_Checked(object sender, RoutedEventArgs e)
        {
            /*comboBoxLDCommands.Items.Remove((ComboBoxItem)comboBoxLDCommands.Items.GetItemAt(1));
            comboBoxLDCommands.Items.Remove((ComboBoxItem)comboBoxLDCommands.Items.GetItemAt(1));
            comboBoxLDCommands.Items.Remove((ComboBoxItem)comboBoxLDCommands.Items.GetItemAt(1));*/
            comboBoxLDCommands.Items.Clear();

            comboBoxLDCommands.Items.Add("Нет команды");
            if (!(bool)checkBoxLDBlokirovkaIzluch.IsChecked)
                comboBoxLDCommands.Items.Add("Технол. цикл с  включ. блок. ФПУ");
            comboBoxLDCommands.SelectedIndex = 0;
            ControlChanged(sender, e);
        }

        private void checkBoxLDBlokirovkaFPU_Unchecked(object sender, RoutedEventArgs e)
        {
            comboBoxLDCommands.Items.Clear();

            comboBoxLDCommands.Items.Add("Нет команды");
            //comboBoxLDCommands.Items.Add("Измерить дальность с фильтром");
            //comboBoxLDCommands.Items.Add("Измерить дальность без фильтра");
            if ((bool)checkBoxLDBlokirovkaIzluch.IsChecked)
                comboBoxLDCommands.Items.Add("Технол. цикл с включ. блок. излучения");
            else
            {
                comboBoxLDCommands.Items.Add("Измерить дальность с фильтром");
                comboBoxLDCommands.Items.Add("Измерить дальность без фильтра");
            }
            //comboBoxLDCommands.Items.Add("Технол. цикл с  включ. блок. ФПУ");
            comboBoxLDCommands.SelectedIndex = 0;
            ControlChanged(sender, e);
        }

        public static Boolean IsDisposed(Window window)
        {
            return new System.Windows.Interop.WindowInteropHelper(window).Handle == IntPtr.Zero;
        }

        private void buttonLDGetState_Click(object sender, RoutedEventArgs e)
        { 
            if (ldStatusWindow == null || IsDisposed(ldStatusWindow)== true)
               ldStatusWindow = new StatusWindow();
            ldStatusWindow.Title = "Состояние ЛД";
            
            ldStatusWindow.Show();            
        }

        private void buttonLDSettingsExtendState_Click(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void buttonResetGSPAngleControls_Click(object sender, RoutedEventArgs e)
        {
            numAZAngle.Value = 0;
            numELAngle.Value = 0;
            ControlChanged(sender, e);
        }

        private void sliderAZ_TouchLeave(object sender, TouchEventArgs e)
        {

        }

        private void sliderAZ_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void sliderEL_TouchLeave(object sender, TouchEventArgs e)
        {

        }

        private void numAZAngle_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }

        private void numELAngle_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }

        private void numAZ_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }

        private void buttonResetGSPSpeedControls_Click(object sender, RoutedEventArgs e)
        {
            if (!(bool)checkBoxJoystickUse.IsChecked)
            {
                numAZSpeed.Value = 0;
                numELSpeed.Value = 0;
                ControlChanged(sender, e);
            }
        }

        private void sliderAZSpeed_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderAZSpeed.Value = (int)numAZSpeed.Value;
            ControlChanged(sender, e);
        }

        private void sliderAZSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numAZSpeed.Value = sliderAZSpeed.Value;
        }

        private void checkBoxJoystickUse_Checked(object sender, RoutedEventArgs e)
        {
            sliderAZSpeed.IsEnabled = false;
            sliderELSpeed.IsEnabled = false;
            radioButtonGSPSpeedControl1X.IsEnabled = true;
            radioButtonGSPSpeedControl2X.IsEnabled = true;
            radioButtonGSPSpeedControl4X.IsEnabled = true;
            /*checkBoxJoystickUse.IsChecked = !checkBoxJoystickUse.IsChecked;

            int indAZ;
            comboBoxGearModeAZ.SelectedIndex = 2;
            indAZ = comboBoxGearModeAZ.SelectedIndex;

            int indEL;
            comboBoxGearModeEL.SelectedIndex = 2;
            indEL = comboBoxGearModeAZ.SelectedIndex;

            if ((bool)checkBoxJoystickUse.IsChecked)
            {
                sliderAZAngle.IsEnabled = true;
                numAZAngle.IsEnabled = true;
                buttonResetGSPAngleControls.IsEnabled = true;
                sliderELAngle.IsEnabled = true;
                numELAngle.IsEnabled = true;
                buttonResetGSPSpeedControls.IsEnabled = true;

                if (indAZ == 2 || indAZ == 3)
                {
                    sliderAZAngle.Maximum = 300;
                    sliderAZAngle.Minimum = -300;
                    numAZAngle.Maximum = 300;
                    numAZAngle.Minimum = -300;
                    sliderELAngle.Maximum = 300;
                    sliderELAngle.Minimum = -300;
                    numELAngle.Maximum = 300;
                    numELAngle.Minimum = -300;
                }
            }
            else
            {
                sliderAZAngle.IsEnabled = false;
                numAZAngle.IsEnabled = false;
                buttonResetGSPAngleControls.IsEnabled = false;
                sliderELAngle.IsEnabled = false;
                numELAngle.IsEnabled = false;
                buttonResetGSPSpeedControls.IsEnabled = false;
            }*/
        }

        private void checkBoxGSPAngleAutoPereh_Checked(object sender, RoutedEventArgs e)
        {
            gearMode = GearMode.MOVE_SET_ANGLE_AUTO_STAB_AIM;            
            if (checkBoxMUGSPOFF != null)
                checkBoxMUGSPOFF.IsChecked = false;
            if (checkBoxMUGSPDoNotChangeReg != null)
                checkBoxMUGSPDoNotChangeReg.IsChecked = false;
            if (checkBoxGSPAngleseekAngle != null)
                checkBoxGSPAngleseekAngle.IsChecked = false;
            if (checkBoxMUGSPStabilizedAim != null)
                checkBoxMUGSPStabilizedAim.IsChecked = false;
            if (checkBoxGSPSpeedControlsNonStabilized != null)
                checkBoxGSPSpeedControlsNonStabilized.IsChecked = false;
            if (checkBoxJoystickUse != null)
            {
                checkBoxJoystickUse.IsEnabled = false;
                checkBoxJoystickUse.IsChecked = false;
            }
            sliderAZSpeed.IsEnabled = false;
            sliderELSpeed.IsEnabled = false;
            sliderAZSpeedCompensation.IsEnabled = false;
            sliderELSpeedCompensation.IsEnabled = false;

            numAZSpeed.IsEnabled = false;
            numELSpeed.IsEnabled = false;
            numAZSpeedCompensation.IsEnabled = false;
            numELSpeedCompensation.IsEnabled = false;

            sliderAZAngle.IsEnabled = true;
            sliderELAngle.IsEnabled = true;
            numAZAngle.IsEnabled = true;
            numELAngle.IsEnabled = true;
            buttonResetGSPAngleControls.IsEnabled = true;
            radioButtonGSPSpeedControl1X.IsEnabled = false;
            radioButtonGSPSpeedControl2X.IsEnabled = false;
            radioButtonGSPSpeedControl4X.IsEnabled = false;

            ControlChanged(sender, e);
        }

        private void checkBoxGSPAngleAutoPereh_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxGSPAngleseekAngle_Checked(object sender, RoutedEventArgs e)
        {
            gearMode = GearMode.MOVE_SET_ANGLE_SEEK_ANGLE;
            if (checkBoxMUGSPOFF != null)
                checkBoxMUGSPOFF.IsChecked = false;
            if (checkBoxMUGSPDoNotChangeReg != null)
                checkBoxMUGSPDoNotChangeReg.IsChecked = false;
            if (checkBoxGSPAngleAutoPereh != null)
                checkBoxGSPAngleAutoPereh.IsChecked = false;
            if (checkBoxMUGSPStabilizedAim != null)
                checkBoxMUGSPStabilizedAim.IsChecked = false;
            if (checkBoxGSPSpeedControlsNonStabilized != null)
                checkBoxGSPSpeedControlsNonStabilized.IsChecked = false;
            if (checkBoxJoystickUse != null)
            {
                checkBoxJoystickUse.IsEnabled = false;
                checkBoxJoystickUse.IsChecked = false;
            }

            sliderAZSpeed.IsEnabled = false;
            sliderELSpeed.IsEnabled = false;
            sliderAZSpeedCompensation.IsEnabled = false;
            sliderELSpeedCompensation.IsEnabled = false;

            numAZSpeed.IsEnabled = false;
            numELSpeed.IsEnabled = false;
            numAZSpeedCompensation.IsEnabled = false;
            numELSpeedCompensation.IsEnabled = false;

            sliderAZAngle.IsEnabled = true;
            sliderELAngle.IsEnabled = true;
            numAZAngle.IsEnabled = true;
            numELAngle.IsEnabled = true;
            buttonResetGSPAngleControls.IsEnabled = true;
            radioButtonGSPSpeedControl1X.IsEnabled = false;
            radioButtonGSPSpeedControl2X.IsEnabled = false;
            radioButtonGSPSpeedControl4X.IsEnabled = false;

            ControlChanged(sender, e);
        }

        private void checkBoxmMUGSPInfExchangeONOFF_Checked(object sender, RoutedEventArgs e)
        {
            cycleIndex = 0;
            setTagUartReceived(true);
            arrayActiveDevicesInCycle[0] = true;
            arrayActiveDevicesInCycle[2] = true;
            arrayActiveDevicesInCycle[4] = true;
            arrayActiveDevicesInCycle[6] = true;
            activeDevicesArr[0] = true;
            ControlChanged(sender, e);
        }

        private void checkBoxmLDInfExchangeONOFF_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxmTVK2InfExchangeONOFF_Checked(object sender, RoutedEventArgs e)
        {
            //cycleIndex = 0;
            setTagUartReceived(true);
            arrayActiveDevicesInCycle[3] = true;
            activeDevicesArr[2] = true;
            ControlChanged(sender, e);
        }

        private void checkBoxmTVK1InfExchangeONOFF_Checked(object sender, RoutedEventArgs e)
        {
            //cycleIndex = 0;
            setTagUartReceived(true);
            arrayActiveDevicesInCycle[1] = true;
            activeDevicesArr[1] = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKInfExchangeONOFF_Checked(object sender, RoutedEventArgs e)
        {
            //cycleIndex = 0;
            setTagUartReceived(true);
            arrayActiveDevicesInCycle[5] = true;
            activeDevicesArr[3] = true;
            ControlChanged(sender, e);
        }

        /*private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^\\d+(^\\.^\\d+)?");
            e.Handled = regex.IsMatch(e.Text);
        }*/

        /// <summary>
        /// Функция показа принимаемого и отправляемого пакета
        /// </summary>
        /// <param name="buf">Байтовый буфер на отображение</param>
        /// <param name="bOut">Признак выбора выводимого поля. Входящий или исходящий пакет. true - исходящий, false - входящий</param>
        public void ShowBuf(byte[] buf, bool bOut)
        {
            /*TextBox txt = bOut ? txtOutBuf : txtInBuf;
            //string str = StringHelper.ReverseString(BitConverter.ToString(buf));
            string str = BitConverter.ToString(buf);
            try
            {
                txt.Text = str;
                if (_frmLog.IsHandleCreated) _frmLog.AddToList(buf, bOut);
            }
            catch { txtInBuf.Text = "DataError"; }*/
        }

        private void checkBoxMUGSPDoNotChangeReg_Checked(object sender, RoutedEventArgs e)
        {
            gearMode = GearMode.DONOTCHANGE_MODE;
            if (checkBoxMUGSPOFF != null)
                checkBoxMUGSPOFF.IsChecked = false;
            if (checkBoxGSPAngleAutoPereh != null)
                checkBoxGSPAngleAutoPereh.IsChecked = false;
            if (checkBoxGSPAngleseekAngle != null)
                checkBoxGSPAngleseekAngle.IsChecked = false;
            if (checkBoxMUGSPStabilizedAim != null)
                checkBoxMUGSPStabilizedAim.IsChecked = false;
            if (checkBoxGSPSpeedControlsNonStabilized != null)
                checkBoxGSPSpeedControlsNonStabilized.IsChecked = false;
            if (checkBoxJoystickUse != null)
                checkBoxJoystickUse.IsEnabled = false;
            sliderAZSpeed.IsEnabled = false;
            sliderELSpeed.IsEnabled = false;
            sliderAZSpeedCompensation.IsEnabled = false;
            sliderELSpeedCompensation.IsEnabled = false;

            numAZSpeed.IsEnabled = false;
            numELSpeed.IsEnabled = false;
            numAZSpeedCompensation.IsEnabled = false;
            numELSpeedCompensation.IsEnabled = false;
            radioButtonGSPSpeedControl1X.IsEnabled = false;
            radioButtonGSPSpeedControl2X.IsEnabled = false;
            radioButtonGSPSpeedControl4X.IsEnabled = false;

            ControlChanged(sender, e);
        }

        private void checkBoxMUGSPOFF_Checked(object sender, RoutedEventArgs e)
        {
            gearMode = GearMode.OFF;
            if (checkBoxMUGSPDoNotChangeReg != null)
                checkBoxMUGSPDoNotChangeReg.IsChecked = false;
            if (checkBoxGSPAngleAutoPereh != null)
                checkBoxGSPAngleAutoPereh.IsChecked = false;
            if (checkBoxGSPAngleseekAngle != null)
                checkBoxGSPAngleseekAngle.IsChecked = false;
            if (checkBoxMUGSPStabilizedAim != null)
                checkBoxMUGSPStabilizedAim.IsChecked = false;
            if (checkBoxGSPSpeedControlsNonStabilized != null)
                checkBoxGSPSpeedControlsNonStabilized.IsChecked = false;
            if (checkBoxJoystickUse != null)
                checkBoxJoystickUse.IsEnabled = false;
            sliderAZSpeed.IsEnabled = false;
            sliderELSpeed.IsEnabled = false;
            sliderAZSpeedCompensation.IsEnabled = false;
            sliderELSpeedCompensation.IsEnabled = false;

            numAZSpeed.IsEnabled = false;
            numELSpeed.IsEnabled = false;
            numAZSpeedCompensation.IsEnabled = false;
            numELSpeedCompensation.IsEnabled = false;
            radioButtonGSPSpeedControl1X.IsEnabled = false;
            radioButtonGSPSpeedControl2X.IsEnabled = false;
            radioButtonGSPSpeedControl4X.IsEnabled = false;

            ControlChanged(sender, e);
        }

        private void checkBoxMUGSPOFF_Click(object sender, RoutedEventArgs e)
        {
            checkBoxMUGSPOFF.IsChecked = true;
        }

        private void checkBoxMUGSPDoNotChangeReg_Click(object sender, RoutedEventArgs e)
        {
            checkBoxMUGSPDoNotChangeReg.IsChecked = true;
        }

        private void checkBoxGSPAngleAutoPereh_Click(object sender, RoutedEventArgs e)
        {
            checkBoxGSPAngleAutoPereh.IsChecked = true;
        }

        private void checkBoxGSPAngleseekAngle_Click(object sender, RoutedEventArgs e)
        {
            checkBoxGSPAngleseekAngle.IsChecked = true;
        }

        private void checkBoxMUGSPStabilizedAim_Checked(object sender, RoutedEventArgs e)
        {
            gearMode = GearMode.STAB_AIM;
            if (checkBoxMUGSPOFF != null)
                checkBoxMUGSPOFF.IsChecked = false;
            if (checkBoxMUGSPDoNotChangeReg != null)
                checkBoxMUGSPDoNotChangeReg.IsChecked = false;
            if (checkBoxGSPAngleAutoPereh != null)
                checkBoxGSPAngleAutoPereh.IsChecked = false;
            if (checkBoxGSPAngleseekAngle != null)
                checkBoxGSPAngleseekAngle.IsChecked = false;
            if (checkBoxGSPSpeedControlsNonStabilized != null)
                checkBoxGSPSpeedControlsNonStabilized.IsChecked = false;

            sliderAZAngle.IsEnabled = false;
            sliderELAngle.IsEnabled = false;
            numAZAngle.IsEnabled = false;
            numELAngle.IsEnabled = false;            

            sliderAZSpeed.IsEnabled = true;
            sliderELSpeed.IsEnabled = true;
            numAZSpeed.IsEnabled = true;
            numELSpeed.IsEnabled = true;

            sliderAZSpeedCompensation.IsEnabled = true;
            sliderELSpeedCompensation.IsEnabled = true;
            numAZSpeedCompensation.IsEnabled = true;
            numELSpeedCompensation.IsEnabled = true;

            buttonResetGSPSpeedControls.IsEnabled = true;
            checkBoxJoystickUse.IsEnabled = true;
//             radioButtonGSPSpeedControl1X.IsEnabled = true;
//             radioButtonGSPSpeedControl2X.IsEnabled = true;
//             radioButtonGSPSpeedControl4X.IsEnabled = true;

            ControlChanged(sender, e);
        }

        private void checkBoxMUGSPStabilizedAim_Click(object sender, RoutedEventArgs e)
        {
            checkBoxMUGSPStabilizedAim.IsChecked = true;
        }

        private void checkBoxGSPSpeedControlsNonStabilized_Checked(object sender, RoutedEventArgs e)
        {
            gearMode = GearMode.NONSTAB_AIM;
            if (checkBoxMUGSPOFF != null)
                checkBoxMUGSPOFF.IsChecked = false;
            if (checkBoxMUGSPDoNotChangeReg != null)
                checkBoxMUGSPDoNotChangeReg.IsChecked = false;
            if (checkBoxGSPAngleAutoPereh != null)
                checkBoxGSPAngleAutoPereh.IsChecked = false;
            if (checkBoxGSPAngleseekAngle != null)
                checkBoxGSPAngleseekAngle.IsChecked = false;
            if (checkBoxMUGSPStabilizedAim != null)
                checkBoxMUGSPStabilizedAim.IsChecked = false;

            sliderAZSpeed.IsEnabled = true;
            sliderELSpeed.IsEnabled = true;
            numAZSpeed.IsEnabled = true;
            numELSpeed.IsEnabled = true;

            sliderAZSpeedCompensation.IsEnabled = true;
            sliderELSpeedCompensation.IsEnabled = true;
            numAZSpeedCompensation.IsEnabled = true;
            numELSpeedCompensation.IsEnabled = true;

            buttonResetGSPSpeedControls.IsEnabled = true;
            checkBoxJoystickUse.IsEnabled = true;
//             radioButtonGSPSpeedControl1X.IsEnabled = true;
//             radioButtonGSPSpeedControl2X.IsEnabled = true;
//             radioButtonGSPSpeedControl4X.IsEnabled = true;

            ControlChanged(sender, e);
        }

        private void checkBoxGSPSpeedControlsNonStabilized_Click(object sender, RoutedEventArgs e)
        {
            checkBoxGSPSpeedControlsNonStabilized.IsChecked = true;
        }   
        
        private void sliderELSpeed_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderELSpeed.Value = (int)numELSpeed.Value;
            ControlChanged(sender, e);
        }

        private void sliderELSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numELSpeed.Value = sliderELSpeed.Value;
        }

        private void numAZSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderAZSpeed.Value = (int)numAZSpeed.Value;
            ControlChanged(sender, e);
        }

        private void numELSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderELSpeed.Value = (int)numELSpeed.Value;
            ControlChanged(sender, e);
        }

        private void formMainWindow_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //остановка потока обмена и закрытие SerialPort (uart)
            if (uart!= null && uart.isOpen())
            {
                uart.DesetFalse();
                //uartThread.Abort();
                //uartThread.Join();
                cycleIndex = 0;
                setTagUartReceived(true);
                if(uart!=null)
                    uart.Close();

                buttonStart.Content = "Старт";
                buttonStart.Background = Brushes.LightGray;
                cbComPorts.IsEnabled = true;
                uart = null;
            }
            //if (sets != null)
            //    SaveSettings();
            if (uartThread != null)
            {
                uartThread.Abort();
                uartThread.Join();
            }

            MyJoystick.Close();
            if(ldStatusWindow!=null)
                ldStatusWindow.Close();
            if (tvk1StatusWindow != null)
                tvk1StatusWindow.Close();
            if (tvk2StatusWindow != null)
                tvk2StatusWindow.Close();
            if (tpvkStatusWindow != null)
                tpvkStatusWindow.Close();
            frmLog.Close_();
            DoEvents(); //Application.DoEvents();
        }

        private void radioButtonGSPSpeedControl1X_Checked(object sender, RoutedEventArgs e)
        {
            NumJoystickK = 1;
        }

        private void radioButtonGSPSpeedControl2X_Checked(object sender, RoutedEventArgs e)
        {
            NumJoystickK = 2;
        }

        private void radioButtonGSPSpeedControl4X_Checked(object sender, RoutedEventArgs e)
        {
            NumJoystickK = 4;
        }

        private void sliderAZSpeedCompensation_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderAZSpeedCompensation.Value = (int)numAZSpeedCompensation.Value;
            ControlChanged(sender, e);
        }

        private void sliderAZSpeedCompensation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numAZSpeedCompensation.Value = (int)sliderAZSpeedCompensation.Value;
        }

        private void sliderELSpeedCompensation_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderELSpeedCompensation.Value = (int)numELSpeedCompensation.Value;
            ControlChanged(sender, e);
        }

        private void sliderELSpeedCompensation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numELSpeedCompensation.Value = (int)sliderELSpeedCompensation.Value;
        }

        private void numAZSpeedCompensation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderAZSpeedCompensation.Value = (int)numAZSpeedCompensation.Value;
            ControlChanged(sender, e);
        }

        private void numELSpeedCompensation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderELSpeedCompensation.Value = (int)numELSpeedCompensation.Value;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK2ONOFF_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonCAPTURE_MODE0_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonCAPTURE_MODE1_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonCAPTURE_MODE2_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonContrastModeAuto_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonContrastModeManual_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonExpoModeAuto_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonExpoModeManual_Checked(object sender, RoutedEventArgs e)
        {
            sliderTVK2Exposure.IsEnabled = true;
            numTVK2Exposure.IsEnabled = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK2VIDEO_OUT_EN_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonHDRModeOff_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonHDRMode1_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonHDRMode2_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxmLDInfExchangeONOFF(object sender, RoutedEventArgs e)
        {
            //cycleIndex = 0;
            setTagUartReceived(true);
            arrayActiveDevicesInCycle[7] = true;
            activeDevicesArr[4] = true;
            ControlChanged(sender, e);
        }

        private void sliderTVK2CONTRAST_GAIN_TouchLeave(object sender, TouchEventArgs e)
        {
            cycleIndex = 0;
            setTagUartReceived(true);
            sliderTVK2CONTRAST_GAIN.Value = (int)numTVK2CONTRAST_GAIN.Value;
            arrayActiveDevicesInCycle[0] = true;
            ControlChanged(sender, e);
        }

        private void sliderTVK2CONTRAST_GAIN_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numTVK2CONTRAST_GAIN.Value = (int)sliderTVK2CONTRAST_GAIN.Value;
        }

        private void sliderTVK2CONTRAST_OFFSET_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTVK2CONTRAST_OFFSET.Value = (int)numTVK2CONTRAST_OFFSET.Value;
            ControlChanged(sender, e);
        }

        private void sliderTVK2CONTRAST_OFFSET_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numTVK2CONTRAST_OFFSET.Value = (int)sliderTVK2CONTRAST_OFFSET.Value;
        }

        private void sliderTVK2Exposure_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTVK2Exposure.Value = (int)numTVK2Exposure.Value;
            ControlChanged(sender, e);
        }

        private void sliderTVK2Exposure_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(numTVK2Exposure!=null)
                numTVK2Exposure.Value = (int)sliderTVK2Exposure.Value;
        }

        private void numTVK2CONTRAST_GAIN_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTVK2CONTRAST_GAIN.Value = (int)numTVK2CONTRAST_GAIN.Value;
            ControlChanged(sender, e);
        }

        private void numATVK2CONTRAST_OFFSET_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ControlChanged(sender, e);
        }

        private void numTVK2Exposure_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTVK2Exposure.Value = (int)numTVK2Exposure.Value;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKVIDEO_OUT_EN_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void numTVK2CONTRAST_OFFSET_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTVK2CONTRAST_OFFSET.Value = (int)numTVK2CONTRAST_OFFSET.Value;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1VIDEO_OUT_EN_Checked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxMUGSPStabilizedAim_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxJoystickUse_Unchecked(object sender, RoutedEventArgs e)
        {
            sliderAZSpeed.IsEnabled = true;
            sliderELSpeed.IsEnabled = true;
            radioButtonGSPSpeedControl1X.IsEnabled = false;
            radioButtonGSPSpeedControl2X.IsEnabled = false;
            radioButtonGSPSpeedControl4X.IsEnabled = false;
            ControlChanged(sender, e);
        }

        private void buttonLDApply_Click(object sender, RoutedEventArgs e)
        {
            LDCommandSend = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1ONOFF_Checked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1CAM_FocusAuto_Checked(object sender, RoutedEventArgs e)
        {
            sliderTVK1Focus.IsEnabled = false;
            numTVK1Focus.IsEnabled = false;
            if (buttonFocusLeft!=null)
                buttonFocusLeft.IsEnabled = false;
            if (buttonFocusRight != null)
                buttonFocusRight.IsEnabled = false;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = true;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1VIDEO_IN_EN_Checked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void sliderTVK1Zoom_TouchLeave(object sender, TouchEventArgs e)
        {
            //sliderTVK1Zoom.Value = (int)numTVK1Zoom.Value;
            //ControlChanged(sender, e);
        }

        private void sliderTVK1Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            /*if(numTVK1Zoom!=null)
                numTVK1Zoom.Value = (int)sliderTVK1Zoom.Value;         

            ControlChanged(sender, e);*/
            //не реализовывать, т.к. нам нужно отслеживание только по отпусканию мыши или изменению numTVK1Zoom
        }

        private void numTVK1Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTVK1Zoom.Value = (int)numTVK1Zoom.Value;
            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = true;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void numTVK1Focus_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTVK1Focus.Value = (int)numTVK1Focus.Value;
            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = true;
            camAutoFocusSend = false;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void buttonZoomLeft_Click(object sender, RoutedEventArgs e)
        {
        }

        private void buttonZoomRight_Click(object sender, RoutedEventArgs e)
        {
        }

        private void buttonZoomLeft_TouchDown(object sender, TouchEventArgs e)
        {

        }

        private void buttonZoomLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomLeft_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomLeft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomLeft_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomLeft_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomLeft_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomRight_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomRight_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void buttonZoomLeft_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = true;
            camZoomDirectSend = false;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void buttonZoomRight_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = true;
            camZoomDirectSend = false;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void buttonFocusLeft_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = true;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;

            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1ONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void buttonZoomRight_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            camZoomTeleVariableSend = true;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            TVK1DataChanged = true;
            //sliderTVK1Zoom.Value+=100;
            ControlChanged(sender, e);
        }

        private void buttonZoomLeft_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = true;
            camZoomStopSend = false;
            camZoomDirectSend = false;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;            

            TVK1DataChanged = true;
            //sliderTVK1Zoom.Value -= 1;
            ControlChanged(sender, e);
        }

        private void sliderTVK1Zoom_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (numTVK1Zoom != null)
                numTVK1Zoom.Value = (int)sliderTVK1Zoom.Value;
            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = true;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;
            TVK1DataChanged = true;
            ControlChanged(sender, e);                       
        }

        private void checkBoxTVK1RESET_Checked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1RESET_Unchecked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void buttonFocusRight_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            camFocusFarVariableSend = false;
            camFocusNearVariableSend = true;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;

            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void buttonFocusLeft_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            camFocusFarVariableSend = true;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void sliderTVK1Focus_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (numTVK1Focus != null)
                numTVK1Focus.Value = (int)sliderTVK1Focus.Value;
            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = true;
            camAutoFocusSend = false;            

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;

            TVK1DataChanged = true;

            ControlChanged(sender, e);
        }

        private void checkBoxTVK1CAM_FocusAuto_Unchecked(object sender, RoutedEventArgs e)
        {
            sliderTVK1Focus.IsEnabled = true;
            numTVK1Focus.IsEnabled = true;
            buttonFocusLeft.IsEnabled = true;
            buttonFocusRight.IsEnabled = true;

            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = false;
            camFocusDirectSend = false;
            camAutoFocusSend = true;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1VIDEO_OUT_EN_Unchecked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTVK1VIDEO_IN_EN_Unchecked(object sender, RoutedEventArgs e)
        {
            TVK1DataChanged = true;
            ControlChanged(sender, e);
        }

        private void numTVK1ZoomTeleVariableP_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ControlChanged(sender, e);
        }

        private void numTVK1FocusFarNearVariableP_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxmMUGSPInfExchangeONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            arrayActiveDevicesInCycle[0] = false;
            arrayActiveDevicesInCycle[2] = false;
            arrayActiveDevicesInCycle[4] = false;
            arrayActiveDevicesInCycle[6] = false;
            activeDevicesArr[0] = false;
            //cycleIndex = 0;
            //setTagUartReceived(true);
            if (cycleIndex == 0 || cycleIndex == 2 || cycleIndex == 4 || cycleIndex == 6)//анализ на именно этот дивайс (т.к. приложение многопоточное и значение cycleIndex может уже измениться в другом потоке)
            {
                int nextDeviceIndex = getNextActiveDeviceInCycle(cycleIndex);
                if (nextDeviceIndex != -1)//если есть еще активные дивайсы
                {
                    cycleIndex = nextDeviceIndex;
                    //setTagUartReceived(true);  -- НЕ изменять tag на true ! т.к. из информ. обмена выключается модуль, но он не должен мешать работе оставшихся !
                }
                else
                    cycleIndex = 0;
            }

            ControlChanged(sender, e);
        }

        private void checkBoxmTVK2InfExchangeONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            //cycleIndex = 0;
            //setTagUartReceived(true);
            arrayActiveDevicesInCycle[3] = false;
            activeDevicesArr[2] = false;

            if (cycleIndex == 3)//анализ на именно этот дивайс (т.к. приложение многопоточное и значение cycleIndex может уже измениться в другом потоке)
            {
                int nextDeviceIndex = getNextActiveDeviceInCycle(cycleIndex);
                if (nextDeviceIndex != -1)//если есть еще активные дивайсы
                {
                    cycleIndex = nextDeviceIndex;
                    //setTagUartReceived(true);  -- НЕ изменять tag на true ! т.к. из информ. обмена выключается модуль, но он не должен мешать работе оставшихся !
                }
                else
                    cycleIndex = 0;
            }

            ControlChanged(sender, e);
        }

        private void checkBoxTVK2ONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTVK2VIDEO_OUT_EN_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxmTVK1InfExchangeONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            //setTagUartReceived(true);
            arrayActiveDevicesInCycle[1] = false;
            activeDevicesArr[1] = false;

            if (cycleIndex == 1)//анализ на именно этот дивайс (т.к. приложение многопоточное и значение cycleIndex может уже измениться в другом потоке)
            {
                int nextDeviceIndex = getNextActiveDeviceInCycle(cycleIndex);
                if (nextDeviceIndex != -1)//если есть еще активные дивайсы
                {
                    cycleIndex = nextDeviceIndex;
                    //setTagUartReceived(true);  -- НЕ изменять tag на true ! т.к. из информ. обмена выключается модуль, но он не должен мешать работе оставшихся !
                }
                else
                    cycleIndex = 0;
            }

            ControlChanged(sender, e);
        }

        private void checkBoxTPVKInfExchangeONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            //setTagUartReceived(true);
            arrayActiveDevicesInCycle[5] = false;
            activeDevicesArr[3] = false;

            if (cycleIndex == 5)//анализ на именно этот дивайс (т.к. приложение многопоточное и значение cycleIndex может уже измениться в другом потоке)
            {
                int nextDeviceIndex = getNextActiveDeviceInCycle(cycleIndex);
                if (nextDeviceIndex != -1)//если есть еще активные дивайсы
                {
                    cycleIndex = nextDeviceIndex;
                    //setTagUartReceived(true);  -- НЕ изменять tag на true ! т.к. из информ. обмена выключается модуль, но он не должен мешать работе оставшихся !
                }
                else
                    cycleIndex = 0;
            }
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKONOFF_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKONOFF_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKVIDEO_OUT_EN_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKMarkaOnOff_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKMarkaOnOff_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKAutoexposition_Unchecked(object sender, RoutedEventArgs e)
        {
            comboBoxTPVKExpositionMode.IsEnabled = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKAutoexposition_Checked(object sender, RoutedEventArgs e)
        {
            comboBoxTPVKExpositionMode.IsEnabled = false;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKAutoCalibration_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKImageEnhance_Unchecked(object sender, RoutedEventArgs e)
        {
            sliderTPVKEnhancementEdge.IsEnabled = false;
            numTPVKEnhancementEdge.IsEnabled = false;
            sliderTPVKEnhancementContrastModes.IsEnabled = false;
            numTPVKEnhancementContrastModes.IsEnabled = false;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKImageEnhance_Checked(object sender, RoutedEventArgs e)
        {
            sliderTPVKEnhancementEdge.IsEnabled = true;
            numTPVKEnhancementEdge.IsEnabled = true;
            sliderTPVKEnhancementContrastModes.IsEnabled = true;
            numTPVKEnhancementContrastModes.IsEnabled = true;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKAutoCalibration_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonExpoModeManual_Unchecked(object sender, RoutedEventArgs e)
        {
            sliderTVK2Exposure.IsEnabled = false;
            numTVK2Exposure.IsEnabled = false;
            ControlChanged(sender, e);
        }

        private void dataGridLDTargetsDistances_Loaded(object sender, RoutedEventArgs e)
        {
            List<LDTableTargetsDistances> result = new List<LDTableTargetsDistances>(3);
            //result.Add(new LDTableTargetsDistances(1, 13456));
            dataGridLDTargetsDistances.ItemsSource = result;
        }

        private void buttonTVK1StateRequest_Click(object sender, RoutedEventArgs e)
        {
            if (tvk1StatusWindow == null || IsDisposed(tvk1StatusWindow) == true)
                tvk1StatusWindow = new StatusWindow();
            tvk1StatusWindow.Title = "Состояние ТВК 1";

            tvk1StatusWindow.Show();
        }

        private void buttonTVK2StateRequest_Click(object sender, RoutedEventArgs e)
        {
            if (tvk2StatusWindow == null || IsDisposed(tvk2StatusWindow) == true)
                tvk2StatusWindow = new StatusWindow();
            tvk2StatusWindow.Title = "Состояние ТВК 2";

            tvk2StatusWindow.Show();
        }

        private void buttonTPVKStateRequest_Click(object sender, RoutedEventArgs e)
        {
            if (tpvkStatusWindow == null || IsDisposed(tpvkStatusWindow) == true)
                tpvkStatusWindow = new StatusWindow();
            tpvkStatusWindow.Title = "Состояние ТПВК";

            tpvkStatusWindow.Show();
        }

        private void buttonFocusRight_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            camFocusFarVariableSend = false;
            camFocusNearVariableSend = false;
            camFocusStopSend = true;
            camFocusDirectSend = false;
            camAutoFocusSend = false;

            camZoomTeleVariableSend = false;
            camZoomWideVariableSend = false;
            camZoomStopSend = false;
            camZoomDirectSend = false;
            TVK1DataChanged = true;

            ControlChanged(sender, e);
        }

        private void tmrExchange_Tick(object sender, EventArgs e)
        {
            return;//not use tmrExchange for send commands now on the tmrExchange tick since we are using uart.receive event
            if (!Dispatcher.CheckAccess())
            {
                //BeginInvoke(new delInitSendCommand(StartTimer));
                Dispatcher.Invoke(new delInitSendCommand(InitSendCommand));
            }
        }
        
        private void tmrMUGSPErrors_Tick(object sender, EventArgs e)
        {            
            if (!Dispatcher.CheckAccess())
            {                
                Dispatcher.Invoke(new delUpdateMUGSPErrorsWindow(UpdateMUGSPErrorsWindow));
            }
        }

        private int getNextQueueIndex(int currIndex)
        {
            int Index = -1;
            if (currIndex == 4)
                return -1;

            for (int i = currIndex+1; i < 5; i++)
                if (activeDevices[i]==true)
                    Index=i;

            return Index;
        }

        private void checkBoxTPVKVIDEO_OUT_EN_Checked_1(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKVIDEO_OUT_EN_Unchecked_1(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKONOFF_Checked_1(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKONOFF_Unchecked_1(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKVIDEO_OUT_EN_Unchecked_2(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void sliderLDStrob_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderLDStrob.Value = (int)numLDStrob.Value;
            ControlChanged(sender, e);
        }

        private void sliderLDStrob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numLDStrob.Value = (int)sliderLDStrob.Value;
        }

        private void numLDStrob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderLDStrob.Value = (int)numLDStrob.Value;
            ControlChanged(sender, e);
        }

        private int getNextActiveDeviceInCycle(int currIndex)
        {
            int Index = -1;
            bool bFound = false;
            for (int i = currIndex + 1; i < 8; i++)
                if (arrayActiveDevicesInCycle[i] == true)
                {
                    bFound = true;
                    Index = i;
                }
            if(bFound==false)
                for (int i = 0; i < currIndex; i++)
                    if (arrayActiveDevicesInCycle[i] == true)
                    {
                        bFound = true;
                        Index = i;
                    }

            return Index;
        }

        private void chbLogFileSave_Checked(object sender, RoutedEventArgs e)
        {
            LogFileSave = true;
            frmLog.Open();
            ControlChanged(sender, e);
        }

        private void chbLogFileSave_Unchecked(object sender, RoutedEventArgs e)
        {
            LogFileSave = false;
            frmLog.Close_();
            ControlChanged(sender, e);
        }

        private void UpdateMUGSPErrorsWindow()
        {
            if(MUGSPInfExchangeONOFF)
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    tbErrors.Text = str_err;                    
                    str_err = "";
                    CountMUGSPErrDatchUglAz = 0;
                    CountMUGSPErrDatchUglTang = 0;
                    CountMUGSPErrAzNeVDopuske = 0;
                    CountMUGSPErrTangNeVDopuske = 0;
                }));
        }

        private void sliderTPVKZoom_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKZoom.Value = (int)numTPVKZoom.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (numTPVKZoom != null)
            {
                numTPVKZoom.Value = (int)sliderTPVKZoom.Value;
                ControlChanged(sender, e);
            }
        }

        private void sliderTPVKFocus_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            numTPVKFocus.Value = (int)sliderTPVKFocus.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKFocus_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKFocus.Value = (int)numTPVKFocus.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKZoom.Value = (int)numTPVKZoom.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKFocus_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKFocus.Value = (int)numTPVKFocus.Value;
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKCalibration_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void checkBoxTPVKCalibration_Unchecked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKModeVkl_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKModeDejurn_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKPolarityWhite_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKPolarityBlack_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKContrastRuch_Checked(object sender, RoutedEventArgs e)
        {
            sliderTPVKGain.IsEnabled = true;
            numTPVKGain.IsEnabled = true;
            sliderTPVKLevel.IsEnabled = true;
            numTPVKLevel.IsEnabled = true;
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKContrastAuto_Checked(object sender, RoutedEventArgs e)
        {
            sliderTPVKGain.IsEnabled = false;
            numTPVKGain.IsEnabled = false;
            numTPVKGain.Value = 0;
            sliderTPVKLevel.IsEnabled = false;
            numTPVKLevel.IsEnabled = false;
            numTPVKLevel.Value = 0;
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKPolarityWhite1_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKMarkBlack_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKMarkGray_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKMarkWhite_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void sliderTPVKDigitalZoom_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKDigitalZoom.Value = (int)numTPVKDigitalZoom.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKGain_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKGain.Value = (int)numTPVKGain.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKLevel_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKLevel.Value = (int)numTPVKLevel.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKDigitalZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (numTPVKDigitalZoom != null)
                numTPVKDigitalZoom.Value = (int)sliderTPVKDigitalZoom.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKGain_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(numTPVKGain!=null)
                numTPVKGain.Value = (int)sliderTPVKGain.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (numTPVKLevel != null)
                numTPVKLevel.Value = (int)sliderTPVKLevel.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKDigitalZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKDigitalZoom.Value = (int)numTPVKDigitalZoom.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKGain_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKGain.Value = (int)numTPVKGain.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKLevel.Value = (int)numTPVKLevel.Value;
            ControlChanged(sender, e);
        }

        private void comboBoxTPVKExpositionMode_DropDownClosed(object sender, EventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void radioButtonTPVKMarkOff_Checked(object sender, RoutedEventArgs e)
        {
            ControlChanged(sender, e);
        }

        private void sliderTPVKEnhancementEdge_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKEnhancementEdge.Value = (int)numTPVKEnhancementEdge.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKEnhancementEdge_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (numTPVKEnhancementEdge != null)
                numTPVKEnhancementEdge.Value = (int)sliderTPVKEnhancementEdge.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKEnhancementEdge_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKEnhancementEdge.Value = (int)numTPVKEnhancementEdge.Value;
            ControlChanged(sender, e);
        }

        private void numTPVKEnhancementContrastModes_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            sliderTPVKEnhancementContrastModes.Value = (int)numTPVKEnhancementContrastModes.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKEnhancementContrastModes_TouchLeave(object sender, TouchEventArgs e)
        {
            sliderTPVKEnhancementContrastModes.Value = (int)numTPVKEnhancementContrastModes.Value;
            ControlChanged(sender, e);
        }

        private void sliderTPVKEnhancementContrastModes_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (numTPVKEnhancementContrastModes != null)
                numTPVKEnhancementContrastModes.Value = (int)sliderTPVKEnhancementContrastModes.Value;
            ControlChanged(sender, e);
        }

        private void buttonTPVKCalibration_Click(object sender, RoutedEventArgs e)
        {
            TPVKCalibrationEngaged = true;
            ControlChanged(sender, e);
        }

        /// <summary>
        /// заполнение отправляемого пакета
        /// </summary>
        private void InitSendCommand()
        {
            int timeMUGSP = Environment.TickCount;
            int timeTVK1 = Environment.TickCount;
            int timeTVK2 = Environment.TickCount;
            int timeTPVK = Environment.TickCount;
            int timeLD = Environment.TickCount;

            while (true)
            {
                 if (uart == null)
                     return;
                //numOfPocket++;

                //МУ ГСП
                if ((Environment.TickCount > timeMUGSP + 100) && MUGSPInfExchangeONOFF && !getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 0)
                    setTagUartReceived(true);

                if (MUGSPInfExchangeONOFF && getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 0)
                 {
                     timeMUGSP = Environment.TickCount;
                     com.DiscardDataBuf();
                     byte[] buf_chksum_header = new byte[4];
                     byte[] buf_chksum_all = new byte[16];//полная длина пакета 18 - 2 байта (длина чексуммы 2)

                     com.START = st_out.START;
                     com.ADDRESS = st_out.ADDRESS;
                     com.LENGTH[0] = 5;
                     com.LENGTH[1] = 0;

                     buf_chksum_header[0] = com.START;
                     buf_chksum_header[1] = com.ADDRESS;
                     buf_chksum_header[2] = com.LENGTH[0];
                     buf_chksum_header[3] = com.LENGTH[1];

                     ushort chksm = (ushort)CheckSumRFC1071(buf_chksum_header, 4);
                     com.CHECKSUM1 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_header, 4));

                     com.DATA[0] = st_out.MODE_AZ;
                     com.DATA[1] = st_out.MODE_EL;
                     com.DATA[2] = st_out.INPUT_AZ[0];
                     com.DATA[3] = st_out.INPUT_AZ[1];
                     com.DATA[4] = st_out.INPUT_AZ[2];
                     com.DATA[5] = st_out.INPUT_EL[0];
                     com.DATA[6] = st_out.INPUT_EL[1];
                     com.DATA[7] = st_out.INPUT_EL[2];

                     sbyte byte9 = 0;

                     if (st_out.ECO_MODE)
                         byte9 |= 1 << 0;
                     else
                         byte9 &= ~(1 << 0);

                     if (st_out.RESET)
                         byte9 |= 1 << 1;
                     else
                         byte9 &= ~(1 << 1);


                     com.DATA[8] = (byte)byte9;

                     com.DATA[9] = st_out.RESERVE2;

                     buf_chksum_all[0] = com.START;
                     buf_chksum_all[1] = com.ADDRESS;
                     buf_chksum_all[2] = com.LENGTH[0];
                     buf_chksum_all[3] = com.LENGTH[1];

                     byte[] byteArray = BitConverter.GetBytes(chksm);

                     buf_chksum_all[4] = byteArray[1];
                     buf_chksum_all[5] = byteArray[0];

                     buf_chksum_all[6] = com.DATA[0];
                     buf_chksum_all[7] = com.DATA[1];
                     buf_chksum_all[8] = com.DATA[2];
                     buf_chksum_all[9] = com.DATA[3];
                     buf_chksum_all[10] = com.DATA[4];
                     buf_chksum_all[11] = com.DATA[5];
                     buf_chksum_all[12] = com.DATA[6];
                     buf_chksum_all[13] = com.DATA[7];
                     buf_chksum_all[14] = com.DATA[8];
                     buf_chksum_all[15] = com.DATA[9];

                     ushort chksm2 = (ushort)CheckSumRFC1071(buf_chksum_all, 16);
                     com.CHECKSUM2 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_all, 16));

                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                         lblNumPacks.Content = "Послано пакетов: " + numOfPocket.ToString();
                     }));

                     uart.SendCommand(com, this);

                     numOfPocket++;
                     setTagUartReceived(false);

                     //если была однократная посылка - остановить таймер передачи и НЕ закрывать uart  !
                     /*if (!(bool)chbCycleMode.IsChecked)
                     {
                         //uart.DesetFalse();  //--- оставить active в true

                         tmrExchange.Stop();
                         //uart.Close();

                         buttonStart.Content = "Старт";
                         buttonStart.Background = Brushes.LightGray;
                         cbComPorts.IsEnabled = true;
                         //uart = null;
                     }*/

                    currentDevice = Device.TVK1;
                 }
                //МУ ГСП END

                //ТВК1
                if ((Environment.TickCount > timeTVK1 + 100) && TVK1InfExchangeONOFF && !getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 1)                
                    setTagUartReceived(true);
                
                 if (TVK1InfExchangeONOFF && getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 1)
                 {
                     timeTVK1 = Environment.TickCount;
                     comTVK1.DiscardDataBuf();
                     byte[] buf_chksum_header = new byte[4];
                     byte[] buf_chksum_all = new byte[24];//полная длина пакета 26 - 2 байта (длина чексуммы 2)


                     comTVK1.START = st_out.START;
                     comTVK1.ADDRESS = 12;
                     comTVK1.LENGTH[0] = 9;//длина пакета 18 байт для камеры Sony (У + Cin + ТВК1 PACKET (16 байт))
                     comTVK1.LENGTH[1] = 0;

                     buf_chksum_header[0] = comTVK1.START;
                     buf_chksum_header[1] = comTVK1.ADDRESS;
                     buf_chksum_header[2] = comTVK1.LENGTH[0];
                     buf_chksum_header[3] = comTVK1.LENGTH[1];

                     ushort chksm = (ushort)CheckSumRFC1071(buf_chksum_header, 4);
                     comTVK1.CHECKSUM1 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_header, 4));

                     if (Cin == 255)
                         Cin = 0;

                     ////Управляющий байт блока управления камерой (байт 0)
                     //if (!camZoomTeleVariableSend && !camZoomWideVariableSend && !camZoomStopSend &&
                     //    !camFocusFarVariableSend && !camFocusNearVariableSend && !camFocusStopSend)
                     {
                         BitArray bitArray = new BitArray(8);
                         bitArray.SetAll(false);

                         if (st_outTVK1.POWER == true)
                         {
                             bitArray.Set(0, true);
                             //TVK1DataChanged = false;
                         }
                         else
                         {
                             bitArray.Set(0, false);
                             //TVK1DataChanged = false;
                         }
                         if (st_outTVK1.RESET == true)
                         {
                             bitArray.Set(1, true);
                             //TVK1DataChanged = false;
                         }
                         else
                         {
                             bitArray.Set(1, false);
                             //TVK1DataChanged = false;
                         }
                         if (st_outTVK1.VIDEO_IN_EN == true)
                         {
                             bitArray.Set(2, true);
                             //TVK1DataChanged = false;
                         }
                         else
                         {
                             bitArray.Set(2, false);
                             //TVK1DataChanged = false;
                         }
                         if (st_outTVK1.VIDEO_OUT_EN == true)
                         {
                             bitArray.Set(3, true);
                             //TVK1DataChanged = false;
                         }
                         else
                         {
                             bitArray.Set(3, false);
                             //TVK1DataChanged = false;
                         }

                         comTVK1.DATA[0] = ConvertToByte(bitArray);//Управляющий байт
                     }

                    if (CAM_ZoomPosInq)//послать запрос на получение значения зума
                    {
                        if (TVK1DataChanged)
                            comTVK1.DATA[1] = ++Cin;
                        comTVK1.DATA[2] = 0x81;
                        comTVK1.DATA[3] = 0x09;
                        comTVK1.DATA[4] = 0x04;
                        comTVK1.DATA[5] = 0x47;
                        comTVK1.DATA[6] = 0xFF;
                        TVK1DataChanged = false;
                        //установить в false при приеме, при подтверждении приема данных
                        //CAM_ZoomPosInq = false;
                        goto labelSendCommand;
                    }

                    if (camZoomTeleVariableSend)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x07;

                         //get p 4 bits value
                         byte iValZoomTeleVariableP = numTVK1ZoomTeleVariablePVal;
                         byte[] myBytes = new byte[1];
                         myBytes[0] = iValZoomTeleVariableP;
                         BitArray bitArrayZoom = new BitArray(myBytes);
                         BitArray byte0 = new BitArray(8);
                         //set p
                         byte0.Set(0, bitArrayZoom.Get(0));
                         byte0.Set(1, bitArrayZoom.Get(1));
                         byte0.Set(2, bitArrayZoom.Get(2));
                         byte0.Set(3, bitArrayZoom.Get(3));
                         //set 2
                         byte0.Set(4, false);
                         byte0.Set(5, true);
                         byte0.Set(6, false);
                         byte0.Set(7, false);

                         byte byteRes0 = ConvertToByte(byte0);

                         comTVK1.DATA[6] = byteRes0;

                         //comTVK1.DATA[6] = 0x02;
                         comTVK1.DATA[7] = 0xFF;
                         //TVK1DataChanged = false;
                         CAM_ZoomPosInq = true;//послать запрос на получение значения зума и не менять TVK1DataChanged на false тут, оно изменится при запросе
                         camZoomTeleVariableSend = false;//сбросить признак, посылать пакет только 1 раз
                         CinZoomPosInq = Cin;
                    }
                     if (camZoomWideVariableSend)
                     {                        
                        if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x07;
                         //get p 4 bits value
                         byte iValZoomTeleVariableP = numTVK1ZoomTeleVariablePVal;
                         byte[] myBytes = new byte[1];
                         myBytes[0] = iValZoomTeleVariableP;
                         BitArray bitArrayZoom = new BitArray(myBytes);
                         BitArray byte0 = new BitArray(8);
                         //set p
                         byte0.Set(0, bitArrayZoom.Get(0));
                         byte0.Set(1, bitArrayZoom.Get(1));
                         byte0.Set(2, bitArrayZoom.Get(2));
                         byte0.Set(3, bitArrayZoom.Get(3));
                         //set 3
                         byte0.Set(4, true);
                         byte0.Set(5, true);
                         byte0.Set(6, false);
                         byte0.Set(7, false);

                         byte byteRes0 = ConvertToByte(byte0);

                         comTVK1.DATA[6] = byteRes0;
                         //comTVK1.DATA[6] = 0x03;
                         comTVK1.DATA[7] = 0xFF;
                        //TVK1DataChanged = false;
                        CAM_ZoomPosInq = true;//послать запрос на получение значения зума и не менять TVK1DataChanged на false тут, оно изменится при запросе
                        camZoomWideVariableSend = false;//сбросить признак, посылать пакет только 1 раз
                        CinZoomPosInq = Cin;
                    }
                     if (camZoomStopSend)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x07;
                         comTVK1.DATA[6] = 0x00;
                         comTVK1.DATA[7] = 0xFF;
                         TVK1DataChanged = false;
                     }
                     if (camZoomDirectSend)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x47;

                         //get pqrs 4 bits values
                         int iValZoom = numTVK1ZoomVal;
                         int[] myInts = new int[1];
                         myInts[0] = iValZoom;
                         BitArray bitArrayZoom = new BitArray(myInts);
                         BitArray byte0 = new BitArray(8);
                         BitArray byte1 = new BitArray(8);
                         BitArray byte2 = new BitArray(8);
                         BitArray byte3 = new BitArray(8);
                         byte0.Set(0, bitArrayZoom.Get(0));
                         byte0.Set(1, bitArrayZoom.Get(1));
                         byte0.Set(2, bitArrayZoom.Get(2));
                         byte0.Set(3, bitArrayZoom.Get(3));
                         byte1.Set(0, bitArrayZoom.Get(4));
                         byte1.Set(1, bitArrayZoom.Get(5));
                         byte1.Set(2, bitArrayZoom.Get(6));
                         byte1.Set(3, bitArrayZoom.Get(7));
                         byte2.Set(0, bitArrayZoom.Get(8));
                         byte2.Set(1, bitArrayZoom.Get(9));
                         byte2.Set(2, bitArrayZoom.Get(10));
                         byte2.Set(3, bitArrayZoom.Get(11));
                         byte3.Set(0, bitArrayZoom.Get(12));
                         byte3.Set(1, bitArrayZoom.Get(13));
                         byte3.Set(2, bitArrayZoom.Get(14));
                         byte3.Set(3, bitArrayZoom.Get(15));
                         byte byteRes0 = ConvertToByte(byte0);
                         byte byteRes1 = ConvertToByte(byte1);
                         byte byteRes2 = ConvertToByte(byte2);
                         byte byteRes3 = ConvertToByte(byte3);

                         comTVK1.DATA[6] = byteRes3;
                         comTVK1.DATA[7] = byteRes2;
                         comTVK1.DATA[8] = byteRes1;
                         comTVK1.DATA[9] = byteRes0;
                         comTVK1.DATA[10] = 0xFF;
                         //TVK1DataChanged = false;
                         CAM_ZoomPosInq = true;//послать запрос на получение значения зума и не менять TVK1DataChanged на false тут, оно изменится при запросе
                         camZoomDirectSend = false;//сбросить признак, посылать пакет только 1 раз
                         CinZoomPosInq = Cin;
                    }

                    ///////////////////////////////////////////////////
                    if (numTVK1ZoomVal != TVK1ZoomPosition)//если устанавливаемое значение не совпадает с принятым - послать серию пакетов 3х
                    {
                        TVK1ZoomPacketsSeries = true;
                        TVK1ZoomMeditVal = numTVK1ZoomVal;//промежуточное значение зума при посылке 3х пакетов
                    }
                    //послать 3х пакета для Zoom
                    if (TVK1ZoomPrev != numTVK1ZoomVal && !TVK1ZoomPacketsSeries)//если изменение зума и НЕТ серии пакетов 3х
                    {
                        TVK1ZoomPacketsSeries = true;
                        TVK1ZoomMeditVal = numTVK1ZoomVal;//промежуточное значение зума при посылке 3х пакетов                        
                    }
                    //if (TVK1ZoomPrev == numTVK1ZoomVal && !TVK1ZoomPacketsSeries)//посылка 0 при одинаковых значениях и(или) окончании пакета 3х
                    //{
                    //    comTVK1.DATA[9] = 0;
                    //}

                    if (TVK1ZoomPacketsSeries)//пакет 3х
                    {
                        if (TVK1Zoom3Count > 2)
                        {
                            TVK1ZoomPrev = TVK1ZoomMeditVal;
                            TVK1ZoomPacketsSeries = false;
                            TVK1Zoom3Count = 0;
                        }
                        else
                        {
                            //comTVK1.DATA[9] = (byte)TVK1ZoomMeditVal;//посылка 3х пакета
                            camZoomDirectSend = true;//посылка 3х пакета
                            TVK1DataChanged = true;
                            TVK1Zoom3Count++;
                        }
                    }
                    ///////////////////////////////

                    if (CAM_FocusPosInq)//послать запрос на получение значения фокуса
                    {
                        if (TVK1DataChanged)
                            comTVK1.DATA[1] = ++Cin;
                        comTVK1.DATA[2] = 0x81;
                        comTVK1.DATA[3] = 0x09;
                        comTVK1.DATA[4] = 0x04;
                        comTVK1.DATA[5] = 0x48;
                        comTVK1.DATA[6] = 0xFF;
                        TVK1DataChanged = false;
                        //установить в false при приеме, при подтверждении приема данных
                        //CAM_FocusPosInq = false;
                        goto labelSendCommand;
                    }

                    if (camFocusFarVariableSend && !CAM_FocusPosInq)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x08;

                         //get p 4 bits value
                         byte iValFocusFarVariableP = numTVK1FocusFarNearVariablePVal;
                         byte[] myBytes = new byte[1];
                         myBytes[0] = iValFocusFarVariableP;
                         BitArray bitArrayZoom = new BitArray(myBytes);
                         BitArray byte0 = new BitArray(8);
                         //set p
                         byte0.Set(0, bitArrayZoom.Get(0));
                         byte0.Set(1, bitArrayZoom.Get(1));
                         byte0.Set(2, bitArrayZoom.Get(2));
                         byte0.Set(3, bitArrayZoom.Get(3));
                         //set 2
                         byte0.Set(4, false);
                         byte0.Set(5, true);
                         byte0.Set(6, false);
                         byte0.Set(7, false);

                         byte byteRes0 = ConvertToByte(byte0);

                         comTVK1.DATA[6] = byteRes0;
                         //comTVK1.DATA[6] = 0x02;
                         comTVK1.DATA[7] = 0xFF;
                         //TVK1DataChanged = false;
                         CAM_FocusPosInq = true;//послать запрос на получение значения фокуса и не менять TVK1DataChanged на false тут, оно изменится при запросе
                         camFocusFarVariableSend = false;//сбросить признак, посылать пакет только 1 раз
                         CinFocusPosInq = Cin-1;
                    }
                     if (camFocusNearVariableSend && !CAM_FocusPosInq)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x08;

                         //get p 4 bits value
                         byte iValFocusNearVariableP = numTVK1FocusFarNearVariablePVal;
                         byte[] myBytes = new byte[1];
                         myBytes[0] = iValFocusNearVariableP;
                         BitArray bitArrayZoom = new BitArray(myBytes);
                         BitArray byte0 = new BitArray(8);
                         //set p
                         byte0.Set(0, bitArrayZoom.Get(0));
                         byte0.Set(1, bitArrayZoom.Get(1));
                         byte0.Set(2, bitArrayZoom.Get(2));
                         byte0.Set(3, bitArrayZoom.Get(3));
                         //set 3
                         byte0.Set(4, true);
                         byte0.Set(5, true);
                         byte0.Set(6, false);
                         byte0.Set(7, false);

                         byte byteRes0 = ConvertToByte(byte0);

                         comTVK1.DATA[6] = byteRes0;
                         //comTVK1.DATA[6] = 0x03;
                         comTVK1.DATA[7] = 0xFF;
                         //TVK1DataChanged = false;
                         CAM_FocusPosInq = true;//послать запрос на получение значения фокуса и не менять TVK1DataChanged на false тут, оно изменится при запросе
                         camFocusNearVariableSend = false;//сбросить признак, посылать пакет только 1 раз
                         CinFocusPosInq = Cin-1;
                    }
                     if (camFocusStopSend)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x08;
                         comTVK1.DATA[6] = 0x00;
                         comTVK1.DATA[7] = 0xFF;
                         TVK1DataChanged = false;
                     }
                     if (camFocusDirectSend && !CAM_FocusPosInq)
                     {
                         if (TVK1DataChanged)
                             comTVK1.DATA[1] = ++Cin;
                         comTVK1.DATA[2] = 0x81;
                         comTVK1.DATA[3] = 0x01;
                         comTVK1.DATA[4] = 0x04;
                         comTVK1.DATA[5] = 0x48;

                         //get pqrs 4 bits values
                         int iValFocus = numTVK1FocusVal;
                         int[] myInts = new int[1];
                         myInts[0] = iValFocus;
                         BitArray bitArrayZoom = new BitArray(myInts);
                         BitArray byte0 = new BitArray(8);
                         BitArray byte1 = new BitArray(8);
                         BitArray byte2 = new BitArray(8);
                         BitArray byte3 = new BitArray(8);
                         byte0.Set(0, bitArrayZoom.Get(0));
                         byte0.Set(1, bitArrayZoom.Get(1));
                         byte0.Set(2, bitArrayZoom.Get(2));
                         byte0.Set(3, bitArrayZoom.Get(3));
                         byte1.Set(0, bitArrayZoom.Get(4));
                         byte1.Set(1, bitArrayZoom.Get(5));
                         byte1.Set(2, bitArrayZoom.Get(6));
                         byte1.Set(3, bitArrayZoom.Get(7));
                         byte2.Set(0, bitArrayZoom.Get(8));
                         byte2.Set(1, bitArrayZoom.Get(9));
                         byte2.Set(2, bitArrayZoom.Get(10));
                         byte2.Set(3, bitArrayZoom.Get(11));
                         byte3.Set(0, bitArrayZoom.Get(12));
                         byte3.Set(1, bitArrayZoom.Get(13));
                         byte3.Set(2, bitArrayZoom.Get(14));
                         byte3.Set(3, bitArrayZoom.Get(15));
                         byte byteRes0 = ConvertToByte(byte0);
                         byte byteRes1 = ConvertToByte(byte1);
                         byte byteRes2 = ConvertToByte(byte2);
                         byte byteRes3 = ConvertToByte(byte3);

                         comTVK1.DATA[6] = byteRes3;
                         comTVK1.DATA[7] = byteRes2;
                         comTVK1.DATA[8] = byteRes1;
                         comTVK1.DATA[9] = byteRes0;
                         comTVK1.DATA[10] = 0xFF;
                         //TVK1DataChanged = false;
                         CAM_FocusPosInq = true;//послать запрос на получение значения фокуса и не менять TVK1DataChanged на false тут, оно изменится при запросе
                         camFocusDirectSend = false;//сбросить признак, посылать пакет только 1 раз
                         CinFocusPosInq = Cin-1;
                    }
                    ///////////////////////////////////////////////////
                    if (numTVK1FocusVal != TVK1FocusPosition)//если устанавливаемое значение не совпадает с принятым - послать серию пакетов 3х
                    {
                        TVK1FocusPacketsSeries = true;
                        TVK1FocusMeditVal = numTVK1FocusVal;//промежуточное значение фокуса при посылке 3х пакетов
                    }
                    //послать 3х пакета для Focus
                    if (TVK1FocusPrev != numTVK1FocusVal && !TVK1FocusPacketsSeries)//если изменение фокуса и НЕТ серии пакетов 3х
                    {
                        TVK1FocusPacketsSeries = true;
                        TVK1FocusMeditVal = numTVK1FocusVal;//промежуточное значение фокуса при посылке 3х пакетов                        
                    }
                    if (TVK1FocusPacketsSeries)//пакет 3х
                    {
                        if (TVK1Focus3Count > 2)
                        {
                            TVK1FocusPrev = TVK1FocusMeditVal;
                            TVK1FocusPacketsSeries = false;
                            TVK1Focus3Count = 0;
                        }
                        else
                        {
                            //comTVK1.DATA[9] = (byte)TVK1FocusMeditVal;//посылка 3х пакета
                            camFocusDirectSend = true;//посылка 3х пакета
                            TVK1DataChanged = true;
                            TVK1Focus3Count++;
                        }
                    }
                    ///////////////////////////////
                    if (CAM_FocusModeInq)//послать запрос на получение значения режима фокуса
                    {
                        if (TVK1DataChanged)
                            comTVK1.DATA[1] = ++Cin;
                        comTVK1.DATA[2] = 0x81;
                        comTVK1.DATA[3] = 0x09;
                        comTVK1.DATA[4] = 0x04;
                        comTVK1.DATA[5] = 0x38;
                        comTVK1.DATA[6] = 0xFF;
                        TVK1DataChanged = false;
                        CAM_FocusModeInq = false;
                        goto labelSendCommand;
                    }

                    if (camAutoFocusSend)
                     {
                         if (checkBoxTVK1CAM_FocusAutoVal)
                         {
                             if (TVK1DataChanged)
                                 comTVK1.DATA[1] = ++Cin;
                             comTVK1.DATA[2] = 0x81;
                             comTVK1.DATA[3] = 0x01;
                             comTVK1.DATA[4] = 0x04;
                             comTVK1.DATA[5] = 0x38;
                             comTVK1.DATA[6] = 0x02;
                             comTVK1.DATA[7] = 0xFF;
                             //TVK1DataChanged = false;
                         }
                         else
                         {
                             if (TVK1DataChanged)
                                 comTVK1.DATA[1] = ++Cin;
                             comTVK1.DATA[2] = 0x81;
                             comTVK1.DATA[3] = 0x01;
                             comTVK1.DATA[4] = 0x04;
                             comTVK1.DATA[5] = 0x38;
                             comTVK1.DATA[6] = 0x03;
                             comTVK1.DATA[7] = 0xFF;
                             //TVK1DataChanged = false;
                         }
                         //TVK1DataChanged = false;
                         CAM_FocusModeInq = true;//послать запрос на получение значения режима фокуса и не менять TVK1DataChanged на false тут, оно изменится при запросе
                         camAutoFocusSend = false;//сбросить признак, посылать пакет только 1 раз
                    }

labelSendCommand:    buf_chksum_all[0] = comTVK1.START;
                     buf_chksum_all[1] = comTVK1.ADDRESS;
                     buf_chksum_all[2] = comTVK1.LENGTH[0];
                     buf_chksum_all[3] = comTVK1.LENGTH[1];

                     byte[] byteArray = BitConverter.GetBytes(chksm);

                     buf_chksum_all[4] = byteArray[1];
                     buf_chksum_all[5] = byteArray[0];

                     buf_chksum_all[6] = comTVK1.DATA[0];
                     buf_chksum_all[7] = comTVK1.DATA[1];
                     buf_chksum_all[8] = comTVK1.DATA[2];
                     buf_chksum_all[9] = comTVK1.DATA[3];
                     buf_chksum_all[10] = comTVK1.DATA[4];
                     buf_chksum_all[11] = comTVK1.DATA[5];
                     buf_chksum_all[12] = comTVK1.DATA[6];
                     buf_chksum_all[13] = comTVK1.DATA[7];
                     buf_chksum_all[14] = comTVK1.DATA[8];
                     buf_chksum_all[15] = comTVK1.DATA[9];
                     buf_chksum_all[16] = comTVK1.DATA[10];
                     buf_chksum_all[17] = comTVK1.DATA[11];
                     buf_chksum_all[18] = comTVK1.DATA[12];
                     buf_chksum_all[19] = comTVK1.DATA[13];
                     buf_chksum_all[20] = comTVK1.DATA[14];
                     buf_chksum_all[21] = comTVK1.DATA[15];
                     buf_chksum_all[22] = comTVK1.DATA[16];
                     buf_chksum_all[23] = comTVK1.DATA[17];

                     ushort chksm2 = (ushort)CheckSumRFC1071(buf_chksum_all, 24);
                     comTVK1.CHECKSUM2 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_all, 24));

                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                         lblNumPacks.Content = "Послано пакетов: " + numOfPocket.ToString();
                     }));

                     uart.SendCommand(comTVK1, this);

                     numOfPocket++;
                     setTagUartReceived(false);
                     currentDevice = Device.MU_GSP;
                 }
                //ТВК1 END

                //ТВК2
                if ((Environment.TickCount > timeTVK2 + 100) && TVK2InfExchangeONOFF && !getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 2)
                    setTagUartReceived(true);

                if (TVK2InfExchangeONOFF && getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 2)
                 {
                     timeTVK2 = Environment.TickCount;
                     comTVK2.DiscardDataBuf();
                     byte[] byteArray;
                     BitArray bitArray = new BitArray(8);
                     byte[] buf_chksum_header = new byte[4];
                     byte[] buf_chksum_all = new byte[28];//полная длина пакета 30 - 2 байта (длина чексуммы 2)


                     comTVK2.START = st_out.START;
                     comTVK2.ADDRESS = 13;
                     comTVK2.LENGTH[0] = 11;//длина пакета 22 байта для камеры ТВК2 (22/2 т.к измерение длины в word - 2 байта)
                     comTVK2.LENGTH[1] = 0;

                     buf_chksum_header[0] = comTVK2.START;
                     buf_chksum_header[1] = comTVK2.ADDRESS;
                     buf_chksum_header[2] = comTVK2.LENGTH[0];
                     buf_chksum_header[3] = comTVK2.LENGTH[1];

                     ushort chksm = (ushort)CheckSumRFC1071(buf_chksum_header, 4);
                     comTVK2.CHECKSUM1 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_header, 4));

                     bitArray.SetAll(false);

                     if (st_outTVK2.POWER)
                         bitArray.Set(0, true);
                     else
                         bitArray.Set(0, false);
                     if (st_outTVK2.VIDEO_OUT_EN)
                         bitArray.Set(1, true);
                     else
                         bitArray.Set(1, false);
                     if (st_outTVK2.EXPO_MODE)
                         bitArray.Set(2, true);
                     else
                         bitArray.Set(2, false);
                     if (st_outTVK2.CONTRAST_MODE)
                         bitArray.Set(3, true);
                     else
                         bitArray.Set(3, false);
                     if (st_outTVK2.CAPTURE_MODE == 0)
                     {
                         bitArray.Set(4, false);
                         bitArray.Set(5, false);
                     }
                     if (st_outTVK2.CAPTURE_MODE == 1)
                     {
                         bitArray.Set(4, true);
                         bitArray.Set(5, false);
                     }
                     if (st_outTVK2.CAPTURE_MODE == 2)
                     {
                         bitArray.Set(4, false);
                         bitArray.Set(5, true);
                     }

                     if (st_outTVK2.HDR_MODE == 0)
                     {
                         bitArray.Set(6, false);
                         bitArray.Set(7, false);
                     }
                     if (st_outTVK2.HDR_MODE == 1)
                     {
                         bitArray.Set(6, true);
                         bitArray.Set(7, false);
                     }
                     if (st_outTVK2.HDR_MODE == 2)
                     {
                         bitArray.Set(6, false);
                         bitArray.Set(7, true);
                     }

                     comTVK2.DATA[0] = ConvertToByte(bitArray);

                     byteArray = BitConverter.GetBytes(st_outTVK2.CONTRAST_GAIN);
                     comTVK2.DATA[1] = byteArray[0];
                     comTVK2.DATA[2] = byteArray[1];

                     byteArray = BitConverter.GetBytes(st_outTVK2.CONTRAST_OFFSET);
                     comTVK2.DATA[3] = byteArray[0];
                     comTVK2.DATA[4] = byteArray[1];

                     if (st_outTVK2.CAPTURE_MODE == 0)//binning mode
                     {
                         UInt16 val = 0x0591;
                         byteArray = BitConverter.GetBytes(val);
                         comTVK2.DATA[5] = byteArray[0];
                         comTVK2.DATA[6] = byteArray[1];
                         comTVK2.DATA[7] = byteArray[0];
                         comTVK2.DATA[8] = byteArray[1];

                         bitArray.SetAll(false);
                         bitArray.Set(3, true);//115 регистр = 0х0008
                         bitArray.Set(4, true);//116 регистр = 03
                         bitArray.Set(5, true);//116 регистр = 03
                         bitArray.Set(6, true);//100 регистр = 03
                         bitArray.Set(7, true);//100 регистр = 03

                         comTVK2.DATA[9] = ConvertToByte(bitArray);

                         val = 0x0082;//116 регистр = 82
                         byteArray = BitConverter.GetBytes(val);
                         comTVK2.DATA[10] = byteArray[0];

                         val = 0x6040;//(79 = 1 и 106 = 0x2040 регистры) = 0x6040 
                         byteArray = BitConverter.GetBytes(val);
                         comTVK2.DATA[20] = byteArray[0];
                         comTVK2.DATA[21] = byteArray[1];
                     }
                     if (st_outTVK2.CAPTURE_MODE == 1)//режим центральная часть матрицы
                     {
                         UInt16 val = 0x0776;
                         byteArray = BitConverter.GetBytes(val);
                         comTVK2.DATA[5] = byteArray[0];
                         comTVK2.DATA[6] = byteArray[1];
                         comTVK2.DATA[7] = byteArray[0];
                         comTVK2.DATA[8] = byteArray[1];

                         bitArray.SetAll(false);
                         bitArray.Set(0, true);//115 регистр = 0х0000
                         bitArray.Set(4, true);//116 регистр = 03
                         bitArray.Set(5, true);//116 регистр = 03

                         comTVK2.DATA[9] = ConvertToByte(bitArray);

                         val = 0x00e6;//116 регистр = e6
                         byteArray = BitConverter.GetBytes(val);
                         comTVK2.DATA[10] = byteArray[0];

                         val = 0x6040;
                         byteArray = BitConverter.GetBytes(val);
                         comTVK2.DATA[20] = byteArray[0];
                         comTVK2.DATA[21] = byteArray[1];
                     }
                    if (st_outTVK2.CAPTURE_MODE == 2)//pixel-pixel mode
                    {
                        UInt16 val = 1910;
                        byteArray = BitConverter.GetBytes(val);
                        comTVK2.DATA[5] = byteArray[0];
                        comTVK2.DATA[6] = byteArray[1];
                        comTVK2.DATA[7] = byteArray[0];
                        comTVK2.DATA[8] = byteArray[1];

                        bitArray.SetAll(false);
                        bitArray.Set(3, false);//115 регистр = 0х0000
                        bitArray.Set(4, true);//116 регистр = 03
                        bitArray.Set(5, true);//116 регистр = 03
                        bitArray.Set(6, true);//100 регистр = 03
                        bitArray.Set(7, true);//100 регистр = 03

                        comTVK2.DATA[9] = ConvertToByte(bitArray);

                        val = 0x0082;//116 регистр = 82
                        byteArray = BitConverter.GetBytes(val);
                        comTVK2.DATA[10] = byteArray[0];

                        val = 0x6040;//(79 = 1 и 106 = 0x2040 регистры) = 0x6040
                        byteArray = BitConverter.GetBytes(val);
                        comTVK2.DATA[20] = byteArray[0];
                        comTVK2.DATA[21] = byteArray[1];
                    }

                    byte[] byteArrayEXPOSURE = BitConverter.GetBytes(st_outTVK2.EXPOSURE);
                     comTVK2.DATA[11] = byteArrayEXPOSURE[0];
                     comTVK2.DATA[12] = byteArrayEXPOSURE[1];
                     comTVK2.DATA[13] = byteArrayEXPOSURE[2];
                     byte[] byteArrayHDR_EXPOSURE1 = BitConverter.GetBytes(st_outTVK2.HDR_EXPOSURE1);
                     comTVK2.DATA[14] = byteArrayHDR_EXPOSURE1[0];
                     comTVK2.DATA[15] = byteArrayHDR_EXPOSURE1[1];
                     comTVK2.DATA[16] = byteArrayHDR_EXPOSURE1[2];
                     byte[] byteArrayHDR_EXPOSURE2 = BitConverter.GetBytes(st_outTVK2.HDR_EXPOSURE2);
                     comTVK2.DATA[17] = byteArrayHDR_EXPOSURE2[0];
                     comTVK2.DATA[18] = byteArrayHDR_EXPOSURE2[1];
                     comTVK2.DATA[19] = byteArrayHDR_EXPOSURE2[2];


                     buf_chksum_all[0] = comTVK2.START;
                     buf_chksum_all[1] = comTVK2.ADDRESS;
                     buf_chksum_all[2] = comTVK2.LENGTH[0];
                     buf_chksum_all[3] = comTVK2.LENGTH[1];

                     byte[] byteArraychksm = BitConverter.GetBytes(chksm);

                     buf_chksum_all[4] = byteArraychksm[1];
                     buf_chksum_all[5] = byteArraychksm[0];

                     buf_chksum_all[6] = comTVK2.DATA[0];
                     buf_chksum_all[7] = comTVK2.DATA[1];
                     buf_chksum_all[8] = comTVK2.DATA[2];
                     buf_chksum_all[9] = comTVK2.DATA[3];
                     buf_chksum_all[10] = comTVK2.DATA[4];
                     buf_chksum_all[11] = comTVK2.DATA[5];
                     buf_chksum_all[12] = comTVK2.DATA[6];
                     buf_chksum_all[13] = comTVK2.DATA[7];
                     buf_chksum_all[14] = comTVK2.DATA[8];
                     buf_chksum_all[15] = comTVK2.DATA[9];
                     buf_chksum_all[16] = comTVK2.DATA[10];
                     buf_chksum_all[17] = comTVK2.DATA[11];
                     buf_chksum_all[18] = comTVK2.DATA[12];
                     buf_chksum_all[19] = comTVK2.DATA[13];
                     buf_chksum_all[20] = comTVK2.DATA[14];
                     buf_chksum_all[21] = comTVK2.DATA[15];
                     buf_chksum_all[22] = comTVK2.DATA[16];
                     buf_chksum_all[23] = comTVK2.DATA[17];
                     buf_chksum_all[24] = comTVK2.DATA[18];
                     buf_chksum_all[25] = comTVK2.DATA[19];
                     buf_chksum_all[26] = comTVK2.DATA[20];
                     buf_chksum_all[27] = comTVK2.DATA[21];

                     ushort chksm2 = (ushort)CheckSumRFC1071(buf_chksum_all, 28);
                     comTVK2.CHECKSUM2 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_all, 28));

                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                         lblNumPacks.Content = "Послано пакетов: " + numOfPocket.ToString();
                     }));

                     uart.SendCommand(comTVK2, this);

                     numOfPocket++;
                     setTagUartReceived(false);
                     currentDevice = Device.MU_GSP;
                 }
                //ТВК2 END

                //ТПВК
                if ((Environment.TickCount > timeTPVK + 100) && TPVKInfExchangeONOFF && !getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 3)
                    setTagUartReceived(true);

                if (TPVKInfExchangeONOFF && getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 3)
                 {
                     timeTPVK = Environment.TickCount;
                     comTPVK.DiscardDataBuf();
                     byte[] byteArray;
                     BitArray bitArray = new BitArray(8);
                     byte[] buf_chksum_header = new byte[4];
                     byte[] buf_chksum_all = new byte[18];//полная длина пакета 20 - 2 байта (длина чексуммы 2)
                     //18 т.к. добавление байта при нечетном количестве для подсчета контр. суммы


                     comTPVK.START = st_out.START;
                     comTPVK.ADDRESS = 14;
                     comTPVK.LENGTH[0] = 6;//длина пакета 11 байт для камеры ТПВК + один байт добавляется для четности при расчете контр. суммы
                     byte byte3 = 0;
                     byte3 |= 1 << 7;
                     comTPVK.LENGTH[1] = byte3;//b15 = 1, нечетное кол-во байт (11)

                     buf_chksum_header[0] = comTPVK.START;
                     buf_chksum_header[1] = comTPVK.ADDRESS;
                     buf_chksum_header[2] = comTPVK.LENGTH[0];
                     buf_chksum_header[3] = comTPVK.LENGTH[1];

                     ushort chksm = (ushort)CheckSumRFC1071(buf_chksum_header, 4);
                     comTPVK.CHECKSUM1 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_header, 4));

                     //////////
                     bitArray.SetAll(false);

                     if (st_outTPVK.POWER)
                         bitArray.Set(0, true);
                     else
                         bitArray.Set(0, false);
                     if (st_outTPVK.VIDEO_OUT_EN)
                         bitArray.Set(1, true);
                     else
                         bitArray.Set(1, false);


                     comTPVK.DATA[0] = ConvertToByte(bitArray);
  
                    BitArray bitArray2 = new BitArray(8);
                    bitArray2.SetAll(false);

                    comTPVK.DATA[1] = st_outTPVK.MODE_POLARITY_AUTO_CONTRAST_V_READOUT_H_READOUT_CALIBRATION_SYNC_SOURCE_AUTO_CALIBRATION;

                    ///////////////////////////////////////////////////
                    //послать 3х пакета для Калибровки
                    if (TPVKCalibrationEngaged && !TPVKCalibrationPacketsSeries)//если изменение калибровки и НЕТ серии пакетов 3х
                    {
                        TPVKCalibrationPacketsSeries = true;
                        //промежуточное значение калибровки при посылке 3х пакетов                        
                        TPVKCalibrationMeditVal = st_outTPVK.MODE_POLARITY_AUTO_CONTRAST_V_READOUT_H_READOUT_CALIBRATION_SYNC_SOURCE_AUTO_CALIBRATION;
                    }
                    if (!TPVKCalibrationEngaged && !TPVKZoomPacketsSeries)//посылка 0 при отсутствии калибровки (TPVKCalibrationEngaged==false) и(или) окончании пакета 3х
                    {
                        byte[] myBytes = new byte[1];
                        myBytes[0] = st_outTPVK.MODE_POLARITY_AUTO_CONTRAST_V_READOUT_H_READOUT_CALIBRATION_SYNC_SOURCE_AUTO_CALIBRATION;
                        BitArray bitArrayCalibration = new BitArray(myBytes);
                        bitArrayCalibration.Set(5, false);
                        comTPVK.DATA[1] = ConvertToByte(bitArrayCalibration);
                    }

                    if (TPVKCalibrationPacketsSeries)//пакет 3х
                    {
                        if (TPVKCalibration3Count > 2)
                        {
                            TPVKCalibrationPacketsSeries = false;
                            TPVKCalibrationEngaged = false;
                            TPVKCalibration3Count = 0;
                        }
                        else
                        {
                            comTPVK.DATA[1] = (byte)TPVKCalibrationMeditVal;//посылка 3х пакета
                            TPVKCalibration3Count++;
                        }
                    }
                    ///////////////////////////////

                    //if (TPVKCalibrationEngaged)
                    //    TPVKCalibrationEngaged = false;

                    comTPVK.DATA[2] = st_outTPVK.IMAGE_POSITION_LAYING_MARK_DIGITAL_ZOOM_AUTO_EXPOSURE;

                    byteArray = BitConverter.GetBytes(st_outTPVK.LEVEL);
                    comTPVK.DATA[3] = byteArray[1];//турки странные люди, у них байты перевернуты
                    comTPVK.DATA[4] = byteArray[0];

                    byteArray = BitConverter.GetBytes(st_outTPVK.GAIN);
                    comTPVK.DATA[5] = byteArray[1];//турки странные люди, у них байты перевернуты
                    comTPVK.DATA[6] = byteArray[0];

                    comTPVK.DATA[7] = st_outTPVK.EXPOSURE;
                    //comTPVK.DATA[8] = st_outTPVK.FOCUS;

                    ///////////////////////////////////////////////////
                    if (st_outTPVK.FOCUS != TPVKFocus_IN)//если устанавливаемое значение не совпадает с принятым - послать серию пакетов 3х
                    {
                        TPVKFocusPacketsSeries = true;
                        TPVKFocusMeditVal = st_outTPVK.FOCUS;//промежуточное значение фокуса при посылке 3х пакетов
                    }
                    //послать 3х пакета для FOCUS
                    if (TPVKFocusPrev != st_outTPVK.FOCUS && !TPVKFocusPacketsSeries)//если изменение фокуса и НЕТ серии пакетов 3х
                    {
                        TPVKFocusPacketsSeries = true;
                        TPVKFocusMeditVal = st_outTPVK.FOCUS;//промежуточное значение фокуса при посылке 3х пакетов                        
                    }
                    if (TPVKFocusPrev == st_outTPVK.FOCUS && !TPVKFocusPacketsSeries)//посылка 0 при одинаковых значениях и(или) окончании пакета 3х
                    {
                        comTPVK.DATA[8] = 0;
                    }

                    if (TPVKFocusPacketsSeries)//пакет 3х
                    {
                        if (TPVKFocus3Count > 2)
                        {
                            TPVKFocusPrev = TPVKFocusMeditVal;
                            TPVKFocusPacketsSeries = false;
                            TPVKFocus3Count = 0;
                        }
                        else
                        {
                            comTPVK.DATA[8] = (byte)TPVKFocusMeditVal;//посылка 3х пакета
                            TPVKFocus3Count++;
                        }
                    }
                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                    ///////////////////////////////////////////////////
                    if (st_outTPVK.ZOOM != TPVKZoom_IN)//если устанавливаемое значение не совпадает с принятым - послать серию пакетов 3х
                    {
                        TPVKZoomPacketsSeries = true;
                        TPVKZoomMeditVal = st_outTPVK.ZOOM;//промежуточное значение зума при посылке 3х пакетов
                    }
                    //послать 3х пакета для Zoom
                    if (TPVKZoomPrev != st_outTPVK.ZOOM && !TPVKZoomPacketsSeries)//если изменение зума и НЕТ серии пакетов 3х
                    {
                        TPVKZoomPacketsSeries = true;                        
                        TPVKZoomMeditVal = st_outTPVK.ZOOM;//промежуточное значение зума при посылке 3х пакетов                        
                    }
                    if (TPVKZoomPrev == st_outTPVK.ZOOM && !TPVKZoomPacketsSeries)//посылка 0 при одинаковых значениях и(или) окончании пакета 3х
                    {
                        comTPVK.DATA[9] = 0;
                    }

                    if (TPVKZoomPacketsSeries)//пакет 3х
                    {
                        if (TPVKZoom3Count > 2)
                        {
                            TPVKZoomPrev = TPVKZoomMeditVal;
                            TPVKZoomPacketsSeries = false;
                            TPVKZoom3Count = 0;
                        }
                        else
                        {
                            comTPVK.DATA[9] = (byte)TPVKZoomMeditVal;//посылка 3х пакета
                            TPVKZoom3Count++;
                        }
                    }
                    ///////////////////////////////

                    comTPVK.DATA[10] = st_outTPVK.ENHANCE;                     

                     buf_chksum_all[0] = comTPVK.START;
                     buf_chksum_all[1] = comTPVK.ADDRESS;
                     buf_chksum_all[2] = comTPVK.LENGTH[0];
                     buf_chksum_all[3] = comTPVK.LENGTH[1];

                     byte[] byteArray2 = BitConverter.GetBytes(chksm);

                     buf_chksum_all[4] = byteArray2[1];
                     buf_chksum_all[5] = byteArray2[0];

                     buf_chksum_all[6] = comTPVK.DATA[0];
                     buf_chksum_all[7] = comTPVK.DATA[1];
                     buf_chksum_all[8] = comTPVK.DATA[2];
                     buf_chksum_all[9] = comTPVK.DATA[3];
                     buf_chksum_all[10] = comTPVK.DATA[4];
                     buf_chksum_all[11] = comTPVK.DATA[5];
                     buf_chksum_all[12] = comTPVK.DATA[6];
                     buf_chksum_all[13] = comTPVK.DATA[7];
                     buf_chksum_all[14] = comTPVK.DATA[8];
                     buf_chksum_all[15] = comTPVK.DATA[9];
                     buf_chksum_all[16] = comTPVK.DATA[10];
                     buf_chksum_all[17] = 0;//добавить нулевой байт для расчета контрольной суммы, т.к. кол-во байт DATA[11]  нечетное = 11

                     ushort chksm2 = (ushort)CheckSumRFC1071(buf_chksum_all, 18);
                     comTPVK.CHECKSUM2 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_all, 18));

                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                         lblNumPacks.Content = "Послано пакетов: " + numOfPocket.ToString();
                     }));

                     uart.SendCommand(comTPVK, this);
                     numOfPocket++;
                     setTagUartReceived(false);
                     currentDevice = Device.MU_GSP;
                }

                //ЛД
                if ((Environment.TickCount > timeLD + 100) && LDInfExchangeONOFF && !getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 4)
                    setTagUartReceived(true);

                if (LDInfExchangeONOFF && getTagUartReceived() && arrayCycleDeviceOrder[cycleIndex] == 4)
                 {
                     timeLD = Environment.TickCount;
                     comLD.DiscardDataBuf();
                     byte[] buf_chksum_header = new byte[4];
                     byte[] buf_chksum_all = new byte[10];//полная длина пакета 12 - 2 байта (длина чексуммы 2)


                     comLD.START = st_out.START;
                     comLD.ADDRESS = 15;
                     comLD.LENGTH[0] = 2;//длина пакета 4 байт для ЛД
                     comLD.LENGTH[1] = 0;

                     buf_chksum_header[0] = comLD.START;
                     buf_chksum_header[1] = comLD.ADDRESS;
                     buf_chksum_header[2] = comLD.LENGTH[0];
                     buf_chksum_header[3] = comLD.LENGTH[1];

                     ushort chksm = (ushort)CheckSumRFC1071(buf_chksum_header, 4);
                     comLD.CHECKSUM1 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_header, 4));

                     comLD.DATA[0] = Convert.ToByte(st_outLD.POWER);
                     if (LDCommandSend)//выполнить однократно команду отличную от 00h - нет команды
                     {
                         comLD.DATA[1] = st_outLD.COMMAND;
                         st_outLD.COMMAND = 0x00;
                         LDCommandSend = false;
                     }

                     sbyte byte3 = 0;
                     if (st_outLD.BLOCK_LD)
                         byte3 |= 1 << 0;
                     else
                         byte3 &= ~(1 << 0);
                     if (st_outLD.BLOCK_FPU)
                         byte3 |= 1 << 1;
                     else
                         byte3 &= ~(1 << 1);
                     if (Convert.ToBoolean(st_outLD.REGIM_VARU))
                         byte3 |= 1 << 2;
                     else
                         byte3 &= ~(1 << 2);
                     comLD.DATA[2] = (byte)byte3;


                     BitArray bitArray = new BitArray(8);
                     bitArray.SetAll(false);

                     if ((st_outLD.YARK_VYV_LD & 1 << 0) != 0)
                         bitArray.Set(0, true);
                     else
                         bitArray.Set(0, false);
                     if ((st_outLD.YARK_VYV_LD & 1 << 1) != 0)
                         bitArray.Set(1, true);
                     else
                         bitArray.Set(1, false);
                     if ((st_outLD.YARK_VYV_LD & 1 << 2) != 0)
                         bitArray.Set(2, true);
                     else
                         bitArray.Set(2, false);
                     if ((st_outLD.YARK_VYV_LD & 1 << 3) != 0)
                         bitArray.Set(3, true);
                     else
                         bitArray.Set(3, false);

                     if ((st_outLD.YARK_VYV_FPU & 1 << 0) != 0)
                         bitArray.Set(4, true);
                     else
                         bitArray.Set(4, false);
                     if ((st_outLD.YARK_VYV_FPU & 1 << 1) != 0)
                         bitArray.Set(5, true);
                     else
                         bitArray.Set(5, false);
                     if ((st_outLD.YARK_VYV_FPU & 1 << 2) != 0)
                         bitArray.Set(6, true);
                     else
                         bitArray.Set(6, false);
                     if ((st_outLD.YARK_VYV_FPU & 1 << 3) != 0)
                         bitArray.Set(7, true);
                     else
                         bitArray.Set(7, false);

                     comLD.DATA[3] = ConvertToByte(bitArray);

                     buf_chksum_all[0] = comLD.START;
                     buf_chksum_all[1] = comLD.ADDRESS;
                     buf_chksum_all[2] = comLD.LENGTH[0];
                     buf_chksum_all[3] = comLD.LENGTH[1];

                     byte[] byteArray = BitConverter.GetBytes(chksm);

                     buf_chksum_all[4] = byteArray[1];
                     buf_chksum_all[5] = byteArray[0];

                     buf_chksum_all[6] = comLD.DATA[0];
                     buf_chksum_all[7] = comLD.DATA[1];
                     buf_chksum_all[8] = comLD.DATA[2];
                     buf_chksum_all[9] = comLD.DATA[3];

                     ushort chksm2 = (ushort)CheckSumRFC1071(buf_chksum_all, 10);
                     comLD.CHECKSUM2 = (ushort)IPAddress.HostToNetworkOrder((short)CheckSumRFC1071(buf_chksum_all, 10));

                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                         lblNumPacks.Content = "Послано пакетов: " + numOfPocket.ToString();
                     }));

                     uart.SendCommand(comLD, this);
                     numOfPocket++;
                     setTagUartReceived(false); //tagUartReceived устанавливаем в false при ПЕРЕДАЧЕ пакета и ЖДЕМ приема пакета !
                     currentDevice = Device.MU_GSP;
                 }//ЛД END             

                 //наращивать счетчик циклограммы в любом случае, даже если дивайс не участвует в инф. обмене
                 //tagUartReceived == true в самом начале циклограммы, а также ПО ПРИЕМУ пакета из uart
                 //хотя тут происходит гонка и tagUartReceived может быть как true так и false? устанавливается из разных потоков
                 //при ПЕРВОМ запуске tagUartReceived == true, далее пользователь нажимает ВКЛ ИНФ ОБМ С ЛД (ТВК1...), происходит передача пакета и пошел цикл...
                 //cycleIndex - это ТЕКУЩЕЕ устройство, с которым работает программа. и для него нужно ждать ответного пакета обязательно.

                 //если есть принятый пакет (tagUartReceived == true) - наращиваем cycleIndex и находим соответсвующее устройство в этом же методе InitSendCommand в цикле while(true) {}.
                 //после чего шлем пакет в InitSendCommand и
                 //устанавливаем tagUartReceived = false и ждем ответного пакета конкретно для этого устройства - cycleIndex НЕ наращивается до приема пакета (tagUartReceived == true)!!!
                 //tagUartReceived устанавливаем в false при ПЕРЕДАЧЕ пакета.
                 //tagUartReceived устанавливаем в true при ПРИЕМЕ пакета.
                 //далее цикл повторяется.  

                 tagUartReceivedInterm = getTagUartReceived();
                        
                 if (getTagUartReceived() == true)
                         cycleIndex++;

                 if (cycleIndex > 7)
                         cycleIndex = 0;             
                }        
        }

        //если есть принятый пакет (tagUartReceived == true) - наращиваем cycleIndex и находим соответсвующее устройство в этом же методе InitSendCommand в цикле while(true) {}.
        //после чего шлем пакет в InitSendCommand и
        //устанавливаем tagUartReceived = false и ждем ответного пакета конкретно для этого устройства - cycleIndex НЕ наращивается до приема пакета (tagUartReceived == true)!!!
        //tagUartReceived устанавливаем в false при ПЕРЕДАЧЕ пакета.
        //tagUartReceived устанавливаем в true при ПРИЕМЕ пакета.
        void setTagUartReceived(bool val)
        {
            lock (locker)
            {
                tagUartReceived = val;
            }
        }
        bool getTagUartReceived()
        {
            lock (locker)
            {
                return tagUartReceived;
            }
        }

        bool isThereAnotherActiveDevice(int currIndex)
        {
            for (int i = 0; i < 5; i++)
                if (i != currIndex && arrayActiveDevicesInCycle[i] == true)
                    return true;
            return false;
        }

        byte ConvertToByte(BitArray bits)
        {
            if (bits.Count != 8)
            {
                throw new ArgumentException("bits");
            }
            byte[] bytes = new byte[1];
            bits.CopyTo(bytes, 0);
            return bytes[0];
        }

        private int GetConvertedDiscreteValue(double val)
        {
            ushort fVal;
            int iVal;
            double remain;

            iVal = (int)val;
            remain = (val - (double)iVal) * 100;
            fVal = Convert.ToUInt16(remain);

            return fVal;
        }

        private int GetRoundedValue(double val)
        {
            if (val > 0)
                return (int)Math.Ceiling(val);
            else
                return (int)Math.Floor(val);
        }

        public void UpdateStructure_OUT()
        {
            if (st_out == null)
                return;

            numTVK1ZoomTeleVariablePVal = (byte)numTVK1ZoomTeleVariableP.Value;
            numTVK1ZoomVal = (int)numTVK1Zoom.Value;
            numTVK1FocusFarNearVariablePVal = (byte)numTVK1FocusFarNearVariableP.Value;
            numTVK1FocusVal = (int)numTVK1Focus.Value;
            checkBoxTVK1CAM_FocusAutoVal = (bool)checkBoxTVK1CAM_FocusAuto.IsChecked;

            double AZValueConvD;
            double ELValueConvD;
            int AZValueConvRoundedI;
            int ELValueConvRoundedI;

            byte[] byteArrayAZ = new byte[3];
            byte[] byteArrayEL = new byte[3];

            //int AZValue=0 , ELValue = 0;

            //update array active devices
            if ((bool)checkBoxmMUGSPInfExchangeONOFF.IsChecked)
                activeDevices[0] = true;
            else
                activeDevices[0] = false;
            if ((bool)checkBoxmTVK1InfExchangeONOFF.IsChecked)
                activeDevices[1] = true;
            else
                activeDevices[1] = false;
            if ((bool)checkBoxmTVK2InfExchangeONOFF.IsChecked)
                activeDevices[2] = true;
            else
                activeDevices[2] = false;
            if ((bool)checkBoxTPVKInfExchangeONOFF.IsChecked)
                activeDevices[3] = true;
            else
                activeDevices[3] = false;
            if ((bool)checkBoxLDInfExchangeONOFF.IsChecked)
                activeDevices[4] = true;
            else
                activeDevices[4] = false;
            //////////////

            MUGSPInfExchangeONOFF = (bool)checkBoxmMUGSPInfExchangeONOFF.IsChecked ? true : false;
            TVK1InfExchangeONOFF = (bool)checkBoxmTVK1InfExchangeONOFF.IsChecked ? true : false;
            TVK2InfExchangeONOFF = (bool)checkBoxmTVK2InfExchangeONOFF.IsChecked ? true : false;
            TPVKInfExchangeONOFF = (bool)checkBoxTPVKInfExchangeONOFF.IsChecked ? true : false;
            LDInfExchangeONOFF = (bool)checkBoxLDInfExchangeONOFF.IsChecked ? true : false;

            /* МУ ГСП section */
            st_out.START = 0x5a;
            st_out.ADDRESS = 11;
            st_out.LENGTH[0] = 10;
            st_out.LENGTH[1] = 0;
            
            //data
            st_out.MODE_AZ = (byte)GearModeValues[(int)gearMode];

            //(АЗИМУТ) AZ value, conversion to 3 bytes array BEGIN 
            //int ind = comboBoxGearModeAZ.SelectedIndex;
            int ind = (int)gearMode;

            if (ind == 2 || ind == 3)//для скорости
            {
                if(!(bool)checkBoxJoystickUse.IsChecked)//управление НЕ джойстиком
                {
                    //numAZSpeed хранит скорость наведения в градусах в секунду, поэтому домножать на 300 не надо
                    //(0.2197265625 / 3600) = 0.00006103515625
                    AZValueConvD = (double)numAZSpeed.Value / 0.00006103515625;//из градусов --> получаем значение в lsb (1.01 град/с = 16547.84 lsb)
                    AZValueConvRoundedI = GetRoundedValue(AZValueConvD);//получить округленное целое число из градусов в lsb (1.01 град/с --> 16548)
                                                                        //на вход GetRoundedValue() подаем значение в lsb (16548) !

                    //numAZSpeedCompensation (+- 500) хранится в lsb, поэтому умножаем на дискрету градус в секунду (0.2197265625 / 3600), чтобы перевести в градусы в секунду -- в новой версии НЕ НАДО !
                    //деление на (0.2197265625 / 3600) в конце было -- в новой версии НЕ НАДО !
                    AZValue = (int)(AZValueConvRoundedI + GetRoundedValue((double)numAZSpeedCompensation.Value));//компенсация только для скорости !

                    if (AZValue < 0 && (AZValue < -4915200))
                        AZValue = -4915200;
                    if (AZValue > 0 && (AZValue > 4915200))
                        AZValue = 4915200;
                }                
                else//если управление джойстиком
                {
                    if(!MUGSPSpeedNavFixed && NumJoystickK == 1)
                    {   //(умножение на 300 переменной AZSpeed т.к. ее значение 0..1  -- получаем значение в градусах в сек) и только при управлении джойстиком !
                        //и еще делим AZSpeed на / 0.00006103515625 чтобы получить в lsb
                        //деление на (0.2197265625 / 3600 * 60) в конце было -- в новой версии НЕ НАДО !  --> делим только на 60
                        AZValue = (int)((GetRoundedValue(AZSpeed * 300 / 0.00006103515625) + GetRoundedValue((double)numAZSpeedCompensation.Value)) / 60);//компенсация только для скорости !
                        if (AZValue < 0 && (AZValue < -81920))
                            AZValue = -81920;
                        if (AZValue > 0 && (AZValue > 81920))
                            AZValue = 81920;
                    }
                    if (!MUGSPSpeedNavFixed && NumJoystickK == 2)
                    {//(умножение на 300 переменной AZSpeed т.к. ее значение 0..1  -- получаем значение в градусах в сек) и только при управлении джойстиком !
                        //и еще делим AZSpeed на / 0.00006103515625 чтобы получить в lsb
                        //деление на (0.2197265625 / 3600 * 15) в конце было -- в новой версии НЕ НАДО !  --> делим только на 15
                        AZValue = (int)((GetRoundedValue(AZSpeed * 300 / 0.00006103515625) + GetRoundedValue((double)numAZSpeedCompensation.Value)) / 15);//компенсация только для скорости !
                        if (AZValue < 0 && (AZValue <-327680))
                            AZValue = -327680;
                        if (AZValue > 0 && (AZValue > 327680))
                            AZValue = 327680;
                    }
                    if (!MUGSPSpeedNavFixed && NumJoystickK == 4)
                    {//(умножение на 300 переменной AZSpeed т.к. ее значение 0..1  -- получаем значение в градусах в сек) и только при управлении джойстиком !
                        //и еще делим AZSpeed на / 0.00006103515625 чтобы получить в lsb
                        //деление на (0.2197265625 / 3600 * 15) в конце было -- в новой версии НЕ НАДО !  --> делим только на 3.333
                        AZValue = (int)((GetRoundedValue(AZSpeed * 300 / 0.00006103515625) + GetRoundedValue((double)numAZSpeedCompensation.Value)) / 3.333333);//компенсация только для скорости !
                        if (AZValue < 0 && (AZValue < -1474560))
                            AZValue = -1474560;
                        if (AZValue > 0 && (AZValue > 1474560))
                            AZValue = 1474560;
                    }
                }            
            }

            if (ind == 4 || ind == 5)//для угла наведения
            {
                AZValue = GetRoundedValue((double)numAZAngle.Value / (360 / Math.Pow(2, 24)));
                if (AZValue < 0 && (AZValue < -8388608))
                    AZValue = -8388608;
                if (AZValue > 0 && (AZValue > 8388607))
                    AZValue = 8388607;
            }

            byteArrayAZ = BitConverter.GetBytes(AZValue);//AZValue хранит угол или скорость в зависимости от режима

            st_out.INPUT_AZ[0] = byteArrayAZ[0];
            st_out.INPUT_AZ[1] = byteArrayAZ[1];
            st_out.INPUT_AZ[2] = byteArrayAZ[2];

            /////(АЗИМУТ) AZ value, conversion to 3 bytes array END

            //(ТАНГАЖ) EL value,conversion to 3 bytes array BEGIN
            if (ind == 2 || ind == 3)//для скорости
            {
                if (!(bool)checkBoxJoystickUse.IsChecked)//управление НЕ джойстиком
                {
                    //numELSpeed хранит скорость наведения в градусах в секунду, поэтому домножать на 300 не надо
                    //(0.2197265625 / 3600) = 0.00006103515625
                    ELValueConvD = (double)numELSpeed.Value / 0.00006103515625;//из градусов --> получаем значение в lsb (1.01 град/с = 16547.84 lsb)
                    ELValueConvRoundedI = GetRoundedValue(ELValueConvD);//получить округленное целое число из градусов в lsb (1.01 град/с --> 16548)
                                                                        //на вход GetRoundedValue() подаем значение в lsb (16548) !

                    //numAZSpeedCompensation (+- 500) хранится в lsb, поэтому умножаем на дискрету градус в секунду (0.2197265625 / 3600), чтобы перевести в градусы в секунду -- в новой версии НЕ НАДО !
                    //деление на (0.2197265625 / 3600) в конце было -- в новой версии НЕ НАДО !
                    ELValue = (int)(ELValueConvRoundedI + GetRoundedValue((double)numELSpeedCompensation.Value));//компенсация только для скорости !

                    if (ELValue < 0 && (ELValue < -4915200))
                        ELValue = -4915200;
                    if (ELValue > 0 && (ELValue > 4915200))
                        ELValue = 4915200;
                }
                else//если управление джойстиком
                {
                    if (!MUGSPSpeedNavFixed && NumJoystickK == 1)
                    {   //(умножение на 300 переменной ELSpeed т.к. ее значение 0..1  -- получаем значение в градусах в сек) и только при управлении джойстиком !
                        //и еще делим ELSpeed на / 0.00006103515625 чтобы получить в lsb
                        //деление на (0.2197265625 / 3600 * 60) в конце было -- в новой версии НЕ НАДО !  --> делим только на 60
                        ELValue = (int)((GetRoundedValue(ELSpeed * 300 / 0.00006103515625) + GetRoundedValue((double)numELSpeedCompensation.Value)) / 60);//компенсация только для скорости !
                        if (ELValue < 0 && (ELValue < -81920))
                            ELValue = -81920;
                        if (ELValue > 0 && (ELValue > 81920))
                            ELValue = 81920;
                    }
                    if (!MUGSPSpeedNavFixed && NumJoystickK == 2)
                    {//(умножение на 300 переменной ELSpeed т.к. ее значение 0..1  -- получаем значение в градусах в сек) и только при управлении джойстиком !
                        //и еще делим ELSpeed на / 0.00006103515625 чтобы получить в lsb
                        //деление на (0.2197265625 / 3600 * 15) в конце было -- в новой версии НЕ НАДО !  --> делим только на 15
                        ELValue = (int)((GetRoundedValue(ELSpeed * 300 / 0.00006103515625) + GetRoundedValue((double)numELSpeedCompensation.Value)) / 15);//компенсация только для скорости !
                        if (ELValue < 0 && (ELValue < -327680))
                            ELValue = -327680;
                        if (ELValue > 0 && (ELValue > 327680))
                            ELValue = 327680;
                    }
                    if (!MUGSPSpeedNavFixed && NumJoystickK == 4)
                    {//(умножение на 300 переменной ELSpeed т.к. ее значение 0..1  -- получаем значение в градусах в сек) и только при управлении джойстиком !
                        //и еще делим ELSpeed на / 0.00006103515625 чтобы получить в lsb
                        //деление на (0.2197265625 / 3600 * 15) в конце было -- в новой версии НЕ НАДО !  --> делим только на 3.333
                        ELValue = (int)((GetRoundedValue(ELSpeed * 300 / 0.00006103515625) + GetRoundedValue((double)numELSpeedCompensation.Value)) / 3.333333);//компенсация только для скорости !
                        if (ELValue < 0 && (ELValue < -1474560))
                            ELValue = -1474560;
                        if (ELValue > 0 && (ELValue > 1474560))
                            ELValue = 1474560;
                    }
                }
            }

            if (ind == 4 || ind == 5)//для угла наведения
            {
                ELValue = GetRoundedValue((double)numELAngle.Value / (360 / Math.Pow(2, 24)));
                if (ELValue < 0 && (ELValue < -8388608))
                    ELValue = -8388608;
                if (ELValue > 0 && (ELValue > 8388607))
                    ELValue = 8388607;
            }

            byteArrayEL = BitConverter.GetBytes(ELValue);//ELValue хранит угол или скорость в зависимости от режима            

            st_out.INPUT_EL[0] = byteArrayEL[0];
            st_out.INPUT_EL[1] = byteArrayEL[1];
            st_out.INPUT_EL[2] = byteArrayEL[2];

            ///////(ТАНГАЖ) EL value,conversion to 3 bytes array END

            //st_out.MODE_EL = (byte)GearModeValues[comboBoxGearModeEL.SelectedIndex];
            st_out.MODE_EL = (byte)GearModeValues[(int)gearMode];

            st_out.ECO_MODE = (bool)cbStartOperation.IsChecked;
            st_out.RESET = (bool)cbResetGSP.IsChecked;            
            /* МУ ГСП section END */

            /* LD section */
            if (st_outLD == null)
                return;

            st_outLD.START = 0x5a;
            st_outLD.ADDRESS = 15;
            st_outLD.LENGTH[0] = LD_DATA_SIZE_OUT;
            st_outLD.LENGTH[1] = 0;

            //data
            st_outLD.POWER = (bool)checkBoxLDONOFF.IsChecked;
            int indexLDCommand = getindexLDCommand(comboBoxLDCommands.SelectionBoxItem.ToString());
            st_outLD.COMMAND = (byte)LDCommand[indexLDCommand];
            st_outLD.BLOCK_LD = (bool)checkBoxLDBlokirovkaIzluch.IsChecked;
            st_outLD.BLOCK_FPU = (bool)checkBoxLDBlokirovkaFPU.IsChecked; ;
            st_outLD.REGIM_VARU = Convert.ToByte(!(bool)radioButtonLDRegimVARUOnStart.IsChecked);
            st_outLD.YARK_VYV_LD = (byte)numLDBrightnessLIghtVyvIzl.Value;
            st_outLD.YARK_VYV_FPU = (byte)numLDBrightnessLIghtFPU.Value;
            /* LD section END */

            /* TVK2 section */
            if (st_outTVK2 == null)
                return;

            st_outTVK2.START = 0x5a;
            st_outTVK2.ADDRESS = 13;
            st_outTVK2.LENGTH[0] = TVK2_DATA_SIZE_OUT;
            st_outTVK2.LENGTH[1] = 0;

            //data
            st_outTVK2.POWER = (bool)checkBoxTVK2ONOFF.IsChecked;
            st_outTVK2.VIDEO_OUT_EN = (bool)checkBoxTVK2VIDEO_OUT_EN.IsChecked;
            if ((bool)radioButtonExpoModeAuto.IsChecked)
                st_outTVK2.EXPO_MODE = false;
            else
                st_outTVK2.EXPO_MODE = true;
            if ((bool)radioButtonContrastModeAuto.IsChecked)
                st_outTVK2.CONTRAST_MODE = false;
            else
                st_outTVK2.CONTRAST_MODE = true;
            if ((bool)radioButtonCAPTURE_MODE0.IsChecked)
                st_outTVK2.CAPTURE_MODE = 0;
            if ((bool)radioButtonCAPTURE_MODE1.IsChecked)
                st_outTVK2.CAPTURE_MODE = 1;
            if ((bool)radioButtonCAPTURE_MODE2.IsChecked)
                st_outTVK2.CAPTURE_MODE = 2;

            if ((bool)radioButtonHDRModeOff.IsChecked)
                st_outTVK2.HDR_MODE = 0;
            if ((bool)radioButtonHDRMode1.IsChecked)
                st_outTVK2.HDR_MODE = 1;
            if ((bool)radioButtonHDRMode2.IsChecked)
                st_outTVK2.HDR_MODE = 2;

            //get CONTRAST_GAIN 16 bits UINT value
            //high 7 bits -  the whole part
            //low 9 bits - fraction
            ushort fVal;
            byte iVal;
            double val, remain;
            val = (double)numTVK2CONTRAST_GAIN.Value;
            iVal = (byte)numTVK2CONTRAST_GAIN.Value;
            remain = (val -(double)iVal)*1000;
            fVal = Convert.ToUInt16(remain);

            byte[] myBytes = new byte[1];
            myBytes[0] = iVal;
            BitArray bitArray = new BitArray(myBytes);

            int[] myInts = new int[1];
            myInts[0] = fVal;
            BitArray bitArray2 = new BitArray(myInts);

            BitArray bitArrayCONTRAST_GAIN = new BitArray(16);

            bitArrayCONTRAST_GAIN.SetAll(false);
            bitArrayCONTRAST_GAIN.Set(9, bitArray.Get(0));
            bitArrayCONTRAST_GAIN.Set(10, bitArray.Get(1));
            bitArrayCONTRAST_GAIN.Set(11, bitArray.Get(2));
            bitArrayCONTRAST_GAIN.Set(12, bitArray.Get(3));
            bitArrayCONTRAST_GAIN.Set(13, bitArray.Get(4));
            bitArrayCONTRAST_GAIN.Set(14, bitArray.Get(5));
            bitArrayCONTRAST_GAIN.Set(15, bitArray.Get(6));

            bitArrayCONTRAST_GAIN.Set(0, bitArray2.Get(0));
            bitArrayCONTRAST_GAIN.Set(1, bitArray2.Get(1));
            bitArrayCONTRAST_GAIN.Set(2, bitArray2.Get(2));
            bitArrayCONTRAST_GAIN.Set(3, bitArray2.Get(3));
            bitArrayCONTRAST_GAIN.Set(4, bitArray2.Get(4));
            bitArrayCONTRAST_GAIN.Set(5, bitArray2.Get(5));
            bitArrayCONTRAST_GAIN.Set(6, bitArray2.Get(6));
            bitArrayCONTRAST_GAIN.Set(7, bitArray2.Get(7));
            bitArrayCONTRAST_GAIN.Set(8, bitArray2.Get(8));

            ushort[] arrUshorts = new ushort[1];
            int[] arrInts = new int[1];
            bitArrayCONTRAST_GAIN.CopyTo(arrInts, 0);
            arrUshorts[0] = (ushort)arrInts[0];

            st_outTVK2.CONTRAST_GAIN = arrUshorts[0];
            //END get CONTRAST_GAIN 16 bits UINT value

            st_outTVK2.CONTRAST_OFFSET = (short)numTVK2CONTRAST_OFFSET.Value;

            if ((bool)radioButtonExpoModeManual.IsChecked)
                st_outTVK2.EXPOSURE = (uint)numTVK2Exposure.Value;

            if ((bool)radioButtonExpoModeManual.IsChecked && (bool)radioButtonHDRMode1.IsChecked )
                st_outTVK2.HDR_EXPOSURE1 = (uint)numTVK2Exposure.Value;
            if ((bool)radioButtonExpoModeManual.IsChecked && (bool)radioButtonHDRMode2.IsChecked)
                st_outTVK2.HDR_EXPOSURE2 = (uint)numTVK2Exposure.Value;

            /* TVK2 section END */


            /* TVK1 section */
            if (st_outTVK1 == null)
                return;

            st_outTVK1.START = 0x5a;
            st_outTVK1.ADDRESS = 12;
            st_outTVK1.LENGTH[0] = TVK1_DATA_SIZE_OUT;
            st_outTVK1.LENGTH[1] = 0;

            //data
            st_outTVK1.POWER = (bool)checkBoxTVK1ONOFF.IsChecked;
            st_outTVK1.RESET = (bool)checkBoxTVK1RESET.IsChecked;
            st_outTVK1.VIDEO_OUT_EN = (bool)checkBoxTVK1VIDEO_OUT_EN.IsChecked;
            st_outTVK1.VIDEO_IN_EN = (bool)checkBoxTVK1VIDEO_IN_EN.IsChecked;
            /* TVK1 section END */

            /* TPVK section */
            if (st_outTPVK == null)
                return;

            st_outTPVK.START = 0x5a;
            st_outTPVK.ADDRESS = 14;
            st_outTPVK.LENGTH[0] = TPVK_DATA_SIZE_OUT;
            st_outTPVK.LENGTH[1] = 0;

            //data
            st_outTPVK.POWER = (bool)checkBoxTPVKONOFF.IsChecked;
            st_outTPVK.VIDEO_OUT_EN = (bool)checkBoxTPVKVIDEO_OUT_EN.IsChecked;

            BitArray bitArray0 = new BitArray(8);
            bitArray0.SetAll(false);
            if ((bool)radioButtonTPVKModeVkl.IsChecked)
                bitArray0.Set(0, true);
            if ((bool)radioButtonTPVKModeDejurn.IsChecked)
                bitArray0.Set(0, false);
            if ((bool)radioButtonTPVKPolarityWhite.IsChecked)
                bitArray0.Set(1, false);
            if ((bool)radioButtonTPVKPolarityBlack.IsChecked)
                bitArray0.Set(1, true);
            if ((bool)radioButtonTPVKContrastAuto.IsChecked)
                bitArray0.Set(2, true);
            if ((bool)radioButtonTPVKContrastRuch.IsChecked)
                bitArray0.Set(2, false);
            if (TPVKCalibrationEngaged)//КАЛИБРОВКА
                bitArray0.Set(5, true);
            else
                bitArray0.Set(5, false);
            if ((bool)checkBoxTPVKAutoCalibration.IsChecked)
                bitArray0.Set(7, true);
            else
                bitArray0.Set(7, false);

            st_outTPVK.MODE_POLARITY_AUTO_CONTRAST_V_READOUT_H_READOUT_CALIBRATION_SYNC_SOURCE_AUTO_CALIBRATION = ConvertToByte(bitArray0);

            BitArray bitArray3 = new BitArray(8);
            bitArray3.SetAll(false);

            bitArray3.Set(0, true);
            bitArray3.Set(1, true);
            if ((bool)radioButtonTPVKMarkOff.IsChecked)
            {
                bitArray3.Set(2, false);
                bitArray3.Set(3, false);
            }
            if ((bool)radioButtonTPVKMarkBlack.IsChecked)
            {
                bitArray3.Set(2, true);
                bitArray3.Set(3, false);
            }
            if ((bool)radioButtonTPVKMarkGray.IsChecked)
            {
                bitArray3.Set(2, false);
                bitArray3.Set(3, true);
            }
            if ((bool)radioButtonTPVKMarkWhite.IsChecked)
            {
                bitArray3.Set(2, true);
                bitArray3.Set(3, true);
            }
            
            if (numTPVKDigitalZoom.Value==1)
            {
                bitArray3.Set(4, false);
                bitArray3.Set(5, false);
            }
            if (numTPVKDigitalZoom.Value ==2)
            {
                bitArray3.Set(4, true);
                bitArray3.Set(5, false);
            }
            if (numTPVKDigitalZoom.Value ==3)
            {
                bitArray3.Set(4, false);
                bitArray3.Set(5, true);
            }
            if (numTPVKDigitalZoom.Value ==4)
            {
                bitArray3.Set(4, true);
                bitArray3.Set(5, true);
            }

            if ((bool)checkBoxTPVKAutoexposition.IsChecked)
                bitArray3.Set(6, true);
            else
                bitArray3.Set(6, false);

            st_outTPVK.IMAGE_POSITION_LAYING_MARK_DIGITAL_ZOOM_AUTO_EXPOSURE = ConvertToByte(bitArray3);

            st_outTPVK.LEVEL = (ushort)numTPVKLevel.Value;
            st_outTPVK.GAIN = (ushort)numTPVKGain.Value;

            if (comboBoxTPVKExpositionMode.SelectedIndex == 0)
                st_outTPVK.EXPOSURE = 0;
            if (comboBoxTPVKExpositionMode.SelectedIndex == 1)
                st_outTPVK.EXPOSURE = 1;
            if (comboBoxTPVKExpositionMode.SelectedIndex == 2)
                st_outTPVK.EXPOSURE = 2;

            st_outTPVK.ZOOM = (byte)numTPVKZoom.Value;
            st_outTPVK.FOCUS = (byte)numTPVKFocus.Value;

            BitArray bitArray4 = new BitArray(8);
            bitArray4.SetAll(false);


            if ((bool)checkBoxTPVKImageEnhance.IsChecked)
                bitArray4.Set(0, true);
            else
                bitArray4.Set(0, false);

            if (numTPVKEnhancementEdge.Value == 0)
            {
                bitArray4.Set(1, false);
                bitArray4.Set(2, false);
                bitArray4.Set(3, false);
            }
            if (numTPVKEnhancementEdge.Value == 1)
            {
                bitArray4.Set(1, true);
                bitArray4.Set(2, false);
                bitArray4.Set(3, false);
            }
            if (numTPVKEnhancementEdge.Value == 2)
            {
                bitArray4.Set(1, false);
                bitArray4.Set(2, true);
                bitArray4.Set(3, false);
            }
            if (numTPVKEnhancementEdge.Value == 3)
            {
                bitArray4.Set(1, true);
                bitArray4.Set(2, true);
                bitArray4.Set(3, false);
            }
            if (numTPVKEnhancementEdge.Value == 4)
            {
                bitArray4.Set(1, false);
                bitArray4.Set(2, false);
                bitArray4.Set(3, true);
            }
            if (numTPVKEnhancementEdge.Value == 5)
            {
                bitArray4.Set(1, true);
                bitArray4.Set(2, false);
                bitArray4.Set(3, true);
            }
            if (numTPVKEnhancementEdge.Value == 6)
            {
                bitArray4.Set(1, false);
                bitArray4.Set(2, true);
                bitArray4.Set(3, true);
            }
            if (numTPVKEnhancementEdge.Value == 7)
            {
                bitArray4.Set(1, true);
                bitArray4.Set(2, true);
                bitArray4.Set(3, true);
            }

            //
            if (numTPVKEnhancementContrastModes.Value == 0)
            {
                bitArray4.Set(4, false);
                bitArray4.Set(5, false);
                bitArray4.Set(6, false);
            }
            if (numTPVKEnhancementContrastModes.Value == 1)
            {
                bitArray4.Set(4, true);
                bitArray4.Set(5, false);
                bitArray4.Set(6, false);
            }
            if (numTPVKEnhancementContrastModes.Value == 2)
            {
                bitArray4.Set(4, false);
                bitArray4.Set(5, true);
                bitArray4.Set(6, false);
            }
            if (numTPVKEnhancementContrastModes.Value == 3)
            {
                bitArray4.Set(4, true);
                bitArray4.Set(5, true);
                bitArray4.Set(6, false);
            }
            if (numTPVKEnhancementContrastModes.Value == 4)
            {
                bitArray4.Set(4, false);
                bitArray4.Set(5, false);
                bitArray4.Set(6, true);
            }
            if (numTPVKEnhancementContrastModes.Value == 5)
            {
                bitArray4.Set(4, true);
                bitArray4.Set(5, false);
                bitArray4.Set(6, true);
            }
            if (numTPVKEnhancementContrastModes.Value == 6)
            {
                bitArray4.Set(4, false);
                bitArray4.Set(5, true);
                bitArray4.Set(6, true);
            }
            if (numTPVKEnhancementContrastModes.Value == 7)
            {
                bitArray4.Set(4, true);
                bitArray4.Set(5, true);
                bitArray4.Set(6, true);
            }
            st_outTPVK.ENHANCE = ConvertToByte(bitArray4);
            /* TPVK section END */

        }

        private long CheckSumRFC1071(byte[] buf, int Lenght)
        {
            long CheckSum = 0;
            for (int i = 0; i < Lenght; i += 2)
                CheckSum += (uint)((buf[i] << 8) + buf[i + 1]);
            while ((CheckSum & 0xFFFF0000) != 0)// Если получается неверная контр. сумма, первым делом проверить While
                CheckSum = ((CheckSum & 0xFFFF) + (CheckSum >> 16));
            CheckSum = ~CheckSum;
            return (CheckSum & 0x0000FFFF);
        }

        public int getindexLDCommand(string Item)
        {
            switch (Item)
            {
                case "Нет команды":
                    return 0;
                case "Измерить дальность с фильтром":
                    return 1;
                case "Измерить дальность без фильтра":
                    return 2;
                case "Технол. цикл с включ. блок. излучения":
                    return 3;
                case "Технол. цикл с  включ. блок. ФПУ":
                    return 4;
                default:
                    return 0;                    
            }
        }

    }

    class LDTableTargetsDistances
    {
        public LDTableTargetsDistances(int Id, string val)
        {
            this.Номер = Id;
            this.Расстояние = val;
        }
        public int Номер { get; set; }
        public string Расстояние { get; set; }
    }
}
