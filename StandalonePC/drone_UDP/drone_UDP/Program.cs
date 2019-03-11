using System;

namespace drone_UDP
{
    class Program
    {
        static void Main(string[] args)
        {

            //This is a sample about using the pilotting command.

            BebopCommand bebop = new BebopCommand();
            if (bebop.Discover() == -1)
            {
                Console.ReadLine();
                return;
            }
            else
            {
                while (true)
                {
                    string input = Console.ReadLine();
                    switch (input)
                    {
                        //takeoff
                        case "t":
                            bebop.takeoff();
                            break;
                        //landing
                        //moving command: -100% ~ 100%
                        case "l":
                            bebop.landing();
                            break;
                        //left
                        case "a":
                            bebop.move(1, -10, 0, 0, 0);
                            break;
                        //right
                        case "d":
                            bebop.move(1, 10, 0, 0, 0);
                            break;
                        //forward
                        case "w":
                            bebop.move(1, 0, 10, 0, 0);
                            break;
                        //backward
                        case "s":
                            bebop.move(1, 0, -10, 0, 0);
                            break;
                        //turn left
                        case "h":
                            bebop.move(0, 0, 0, -10, 0);
                            break;
                        //turn right
                        case "k":
                            bebop.move(0, 0, 0, 10, 0);
                            break;
                        //up
                        case "u":
                            bebop.move(0, 0, 0, 0, 10);
                            break;
                        //down
                        case "j":
                            bebop.move(0, 0, 0, 0, -10);
                            break;
                        //pause
                        case "p":
                            bebop.move(0, 0, 0, 0, 0);
                            break;
                        case "v":
                            bebop.videoEnable(); //enable RTP/.H264 videostreaming
                            break;
                        //quit
                        case "q":
                            bebop.cancleAllTask();
                            return;
                    }
                }
            }
        }
    }
}
