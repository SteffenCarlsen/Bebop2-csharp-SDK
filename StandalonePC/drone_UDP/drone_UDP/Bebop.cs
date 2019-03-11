using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Emgu;
namespace BebopSharp
{
    internal class Bebop
    {
        private const string HandshakeMessage = "{\"controller_type\":\"computer\", \"controller_name\":\"halley\", \"d2c_port\":\"43210\", \"arstream2_client_stream_port\":\"55004\", \"arstream2_client_control_port\":\"55005\"}";
        private readonly int[] _seq = new int[256];
        private Command _cmd;
        private PCMD _pcmd;

        private int _updateRate;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationToken _cancelToken;

        private Mutex _pcmdMtx = new Mutex();

        private UdpClient _arstreamClient;
        private IPEndPoint _remoteIpEndPoint;

        private UdpClient _droneUdpClient;

        private byte[] _receivedData;
        private static readonly object ThisLock = new object();

        /// <summary>
        /// Initializing the bebop object
        /// </summary>
        /// <param name="updateRate">Number of updates per second</param>
        public Bebop(int updateRate = 30)
        {
            if (updateRate <= 0) throw new ArgumentOutOfRangeException(nameof(updateRate));
            _updateRate = 1000/updateRate;
        }

        public ConnectionStatus Connect()
        {
            Log("Attempting to connect to drone...");

            //Initialize the drone udp client
            _droneUdpClient = new UdpClient(CommandSet.IP, 54321);

            //make handshake with TCP_client, and the port is set to be 4444
            var tcpClient = new TcpClient(CommandSet.IP, CommandSet.DISCOVERY_PORT);
            var stream = new NetworkStream(tcpClient.Client);

            //initialize reader and writer
            var streamWriter = new StreamWriter(stream);
            var streamReader = new StreamReader(stream);

            //when the drone receive the message bellow, it will return the confirmation
            streamWriter.WriteLine(HandshakeMessage);
            streamWriter.Flush();

            var receiveMessage = streamReader.ReadLine();

            if (receiveMessage == null)
            {
                Log("Connection failed");
                return ConnectionStatus.Failed;
            }

            Log("The message from the drone shows: " + receiveMessage);

            //initialize
            _cmd = default(Command);
            _pcmd = default(PCMD);

            //All State setting
            GenerateAllStates();
            GenerateAllSettings();

            //enable video streaming
            VideoEnable();

            //init ARStream
            InitArStream();
            //initARStream();

            //init CancellationToken
            _cancelToken = _cts.Token;


            PcmdThreadActive();
            //arStreamThreadActive();
            return ConnectionStatus.Success;
        }

        /// <summary>
        /// Sends a single takeoff command to the bebop
        /// </summary>
        public void Takeoff()
        {
            Log("Performing takeoff...");
            _cmd = default(Command);
            _cmd.size = 4;
            _cmd.cmd = new byte[4];

            _cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
            _cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_PILOTING;
            _cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_ARDRONE3_PILOTING_CMD_TAKEOFF;
            _cmd.cmd[3] = 0;

            SendCommand(ref _cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        public void Landing()
        {
            Log("Landing...");
            _cmd = default(Command);
            _cmd.size = 4;
            _cmd.cmd = new byte[4];

            _cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
            _cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_PILOTING;
            _cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_ARDRONE3_PILOTING_CMD_LANDING;
            _cmd.cmd[3] = 0;

            SendCommand(ref _cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        /// <summary>
        /// Moves the drone
        /// </summary>
        /// <param name="flag">1 if the roll and pitch values should be taken in consideration. 0 otherwise</param>
        /// <param name="roll">roll angle percentage (from -100 to 100). Negative values go left, positive go right.</param>
        /// <param name="pitch">pitch angle percentage (from -100 to 100). Negative values go backward, positive go forward.</param>
        /// <param name="yaw">yaw speed percentage (calculated on the max rotation speed)(from -100 to 100). Negative values go left, positive go right.</param>
        /// <param name="gaz">gaz speed percentage (calculated on the max vertical speed)(from -100 to 100). Negative values go down, positive go up.</param>
        public void Move(int flag, int roll, int pitch, int yaw, int gaz)
        {
            _pcmd.flag = flag;
            _pcmd.roll = roll;
            _pcmd.pitch = pitch;
            _pcmd.yaw = yaw;
            _pcmd.gaz = gaz;
            Log(_pcmd.ToString());
        }

        private void GeneratePcmd()
        {
            lock (ThisLock)
            {
                _cmd = default(Command);
                _cmd.size = 13;
                _cmd.cmd = new byte[13];

                _cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
                _cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_PILOTING;
                _cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_ARDRONE3_PILOTING_CMD_PCMD;
                _cmd.cmd[3] = 0;

                //pcmdMtx.WaitOne();
                _cmd.cmd[4] = (byte) _pcmd.flag; // flag
                _cmd.cmd[5] = _pcmd.roll >= 0 ? (byte) _pcmd.roll : (byte) (256 + _pcmd.roll); // roll: fly left or right [-100 ~ 100]
                _cmd.cmd[6] = _pcmd.pitch >= 0 ? (byte) _pcmd.pitch : (byte) (256 + _pcmd.pitch); // pitch: backward or forward [-100 ~ 100]
                _cmd.cmd[7] = _pcmd.yaw >= 0 ? (byte) _pcmd.yaw : (byte) (256 + _pcmd.yaw); // yaw: rotate left or right [-100 ~ 100]
                _cmd.cmd[8] = _pcmd.gaz >= 0 ? (byte) _pcmd.gaz : (byte) (256 + _pcmd.gaz); // gaze: down or up [-100 ~ 100]

                // for Debug Mode
                _cmd.cmd[9] = 0;
                _cmd.cmd[10] = 0;
                _cmd.cmd[11] = 0;
                _cmd.cmd[12] = 0;

                SendCommand(ref _cmd);
            }
        }

        private void SendCommand(ref Command cmd, int type = CommandSet.ARNETWORKAL_FRAME_TYPE_DATA, int id = CommandSet.BD_NET_CD_NONACK_ID)
        {
            var bufSize = cmd.size + 7;
            var buf = new byte[bufSize];

            _seq[id]++;
            if (_seq[id] > 255) _seq[id] = 0;

            buf[0] = (byte)type;
            buf[1] = (byte)id;
            buf[2] = (byte)_seq[id];
            buf[3] = (byte)(bufSize & 0xff);
            buf[4] = (byte)((bufSize & 0xff00) >> 8);
            buf[5] = (byte)((bufSize & 0xff0000) >> 16);
            buf[6] = (byte)((bufSize & 0xff000000) >> 24);

            cmd.cmd.CopyTo(buf, 7);


            _droneUdpClient.Send(buf, buf.Length);
        }

        private void PcmdThreadActive()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    GeneratePcmd();
                    Thread.Sleep(_updateRate);
                }
            }, _cancelToken);
        }

        public void CancelAllTasks()
        {
            _cts.Cancel();
        }

        private void GenerateAllStates()
        {
            Console.WriteLine("Generate All State");
            _cmd = default(Command);
            _cmd.size = 4;
            _cmd.cmd = new byte[4];

            _cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_COMMON;
            _cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_COMMON_CLASS_COMMON;
            _cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_COMMON_COMMON_CMD_ALLSTATES & 0xff;
            _cmd.cmd[3] = CommandSet.ARCOMMANDS_ID_COMMON_COMMON_CMD_ALLSTATES & (0xff00 >> 8);

            SendCommand(ref _cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        private void GenerateAllSettings()
        {
            _cmd = default(Command);
            _cmd.size = 4;
            _cmd.cmd = new byte[4];

            _cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_COMMON;
            _cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_COMMON_CLASS_SETTINGS;
            _cmd.cmd[2] = 0 & 0xff; // ARCOMMANDS_ID_COMMON_CLASS_SETTINGS_CMD_ALLSETTINGS = 0
            _cmd.cmd[3] = 0 & (0xff00 >> 8);

            SendCommand(ref _cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        public void VideoEnable()
        {
            _cmd = default(Command);
            _cmd.size = 5;
            _cmd.cmd = new byte[5];

            _cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
            _cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_MEDIASTREAMING;
            _cmd.cmd[2] = 0 & 0xff; // ARCOMMANDS_ID_COMMON_CLASS_SETTINGS_CMD_VIDEOENABLE = 0
            _cmd.cmd[3] = 0 & (0xff00 >> 8);
            _cmd.cmd[4] = 1; //arg: Enable

            SendCommand(ref _cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        public void InitArStream()
        {
            _arstreamClient = new UdpClient(55004);
            _remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        public void GetImageData()
        {
            _receivedData = _arstreamClient.Receive(ref _remoteIpEndPoint);
            Console.WriteLine("Receive Data: " + BitConverter.ToString(_receivedData));
        }


        public void ArStreamThreadActive()
        {
            Console.WriteLine("The ARStream thread is starting");

            Task.Factory.StartNew(() =>
            {
                while (true)
                    //Thread.Sleep(1000);
                    GetImageData();
            }, _cancelToken);
        }

        private void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }
    }

    internal enum ConnectionStatus
    {
        Success = 1,
        Failed = -1
    }
}