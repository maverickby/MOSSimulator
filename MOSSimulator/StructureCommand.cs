using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*структуры, используются для ПОСЫЛКИ команд*/
namespace MOSSimulator
{
    class StructureCommand
    {
        public byte START;
        public byte ADDRESS;
        public byte[] LENGTH;
        public bool EVEN;
        public ushort CHECKSUM1;
        public byte[] DATA;
        public ushort CHECKSUM2;

        public byte MODE_AZ;
        public byte MODE_EL;
        public byte[] INPUT_AZ;
        public byte[] INPUT_EL;
        public bool ECO_MODE;
        public bool RESET;
        public byte ECOM_RESET_RESERVE1;
        public byte RESERVE2;

        const int GSP_DATA_SIZE = 10;
        const int GSP_PACKET_SIZE = 18;

        public StructureCommand()
        {
            START = 0x5a;
            ADDRESS = 11;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[GSP_DATA_SIZE];
            CHECKSUM2 = 0;

            MODE_AZ = 0x01;
            MODE_EL = 0x01;
            INPUT_AZ = new byte[3];
            INPUT_AZ[0] = 0;
            INPUT_AZ[1] = 0;
            INPUT_AZ[2] = 0;
            INPUT_EL = new byte[3];
            INPUT_EL[0] = 0;
            INPUT_EL[1] = 0;
            INPUT_EL[2] = 0;
            ECO_MODE = false;
            RESET = false;
            RESERVE2 = 0;
        }
    }

    /*структуры ЛД, используется для ПОСЫЛКИ команд*/
    class StructureCommandLD
    {
        public byte START;
        public byte ADDRESS;
        public byte[] LENGTH;
        public bool EVEN;
        public ushort CHECKSUM1;
        public byte[] DATA;
        public ushort CHECKSUM2;

        public bool POWER;
        public byte COMMAND;
        public bool BLOCK_LD;
        public bool BLOCK_FPU;
        public byte REGIM_VARU;
        public byte YARK_VYV_LD;
        public byte YARK_VYV_FPU;

        const int LD_DATA_SIZE = 4;
        const int LD_PACKET_SIZE = 12;

        public StructureCommandLD()
        {
            START = 0x5a;
            ADDRESS = 15;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[LD_DATA_SIZE];
            CHECKSUM2 = 0;

            POWER = false;
            COMMAND = 0x00;
            BLOCK_LD = false;
            BLOCK_FPU = false;
            REGIM_VARU = 0x00;
            YARK_VYV_LD = 0x00;
            YARK_VYV_FPU = 0x00;
        }
    }

    class StructureCommandTVK2
    {
        public byte START;
        public byte ADDRESS;
        public byte[] LENGTH;
        public bool EVEN;
        public ushort CHECKSUM1;
        public byte[] DATA;
        public ushort CHECKSUM2;

        public bool POWER;
        public bool VIDEO_OUT_EN;
        public bool EXPO_MODE;
        public bool CONTRAST_MODE;
        public byte CAPTURE_MODE;
        public byte HDR_MODE;
        public ushort CONTRAST_GAIN;
        public short CONTRAST_OFFSET;
        public ushort CMV_OFFSET_BOT;
        public ushort CMV_OFFSET_TOP;
        public byte CMV_PGA_GAIN;
        public bool CMV_PGA_DIV;
        public byte CMV_ADC_RANGE_MULT;
        public byte CMV_ADC_RANGE_MULT2;
        public byte CMV_ADC_range;
        public uint EXPOSURE;
        public uint HDR_EXPOSURE1;
        public uint HDR_EXPOSURE2;
        public byte CMV_VTFL2;
        public byte CMV_VTFL3;
        public byte CMV_NUMBER_SLOPES;

        const int TVK2_DATA_SIZE = 22;
        const int TVK2_PACKET_SIZE = 30;

        public StructureCommandTVK2()
        {
            START = 0x5a;
            ADDRESS = 13;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[TVK2_DATA_SIZE];
            CHECKSUM2 = 0;

            POWER = false;
            VIDEO_OUT_EN = false;
            EXPO_MODE = false;
            CONTRAST_MODE = false;
            CAPTURE_MODE = 0;
        }
    }

    class StructureCommandTVK1
    {
        public byte START;
        public byte ADDRESS;
        public byte[] LENGTH;
        public bool EVEN;
        public ushort CHECKSUM1;
        public byte[] DATA;
        public ushort CHECKSUM2;

        public bool POWER;
        public bool RESET;
        public bool VIDEO_IN_EN;
        public bool VIDEO_OUT_EN;
        //public byte[] DATA_CAMERA;

        const int TVK1_DATA_SIZE = 18;
        const int TVK1_PACKET_SIZE = 26;

        public StructureCommandTVK1()
        {
            START = 0x5a;
            ADDRESS = 12;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[TVK1_DATA_SIZE];
            CHECKSUM2 = 0;

            POWER = false;
            RESET = false;
            VIDEO_IN_EN = false;
            VIDEO_OUT_EN = false;
        }
    }

    class StructureCommandTPVK
    {
        public byte START;
        public byte ADDRESS;
        public byte[] LENGTH;
        public bool EVEN;
        public ushort CHECKSUM1;
        public byte[] DATA;
        public ushort CHECKSUM2;

        public bool POWER;

        public byte START_CODE;
        public byte MODE_POLARITY__AUTO_CALIBRATION;
        public byte IMAGE_POSITION_LAYING_MARK_DIGITAL_ZOOM_AUTO_EXPOSURE;
        public ushort LEVEL;
        public ushort GAIN;
        public byte EXPOSURE;
        public byte FOCUS;
        public byte ZOOM;
        public byte ENHANCE;
        public ushort CS;

        const int TPVK_DATA_SIZE = 13;
        const int TPVK_PACKET_SIZE = 21;

        public StructureCommandTPVK()
        {
            START = 0x5a;
            ADDRESS = 12;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[TPVK_DATA_SIZE];
            CHECKSUM2 = 0;

            POWER = false;
        }
    }

}
